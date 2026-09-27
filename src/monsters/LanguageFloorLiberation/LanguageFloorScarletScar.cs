using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.LanguageFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.visuals.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

public sealed class LanguageFloorScarletScar :
    LibraryMonsterModel,
    ITargetedMonsterAttackProvider,
    ILiberationPrimaryPhaseBoss
{
    private const string CompositeMoveId = "LANGUAGE_FLOOR_SCARLET_COMPOSITE";
    private const string RouterMoveId = "LANGUAGE_FLOOR_SCARLET_ROUTER";
    public const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";
    private const string AttackSfxPath = "res://audio/sfx/little_red_mercenary/little_red_attack.ogg";
    private const string FireSfxPath = "res://audio/sfx/little_red_mercenary/little_red_fire.ogg";
    private const string RageSfxPath = "res://audio/sfx/little_red_mercenary/little_red_rage.ogg";
    private const string UnrelievedSfxPath = "res://audio/sfx/little_red_mercenary/little_red_unrelieved.ogg";

    [SavedProperty]
    public int PlannedMoveOne { get; private set; } = -1;

    [SavedProperty]
    public int PlannedMoveTwo { get; private set; } = -1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int EnemyTurnCount { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool UnrelievedAnger { get; private set; }

    private MoveState? _compositeState;
    private MoveState? _reviveAndEmpowerState;
    private AbstractIntent[]? _compositeIntents;

    public int LiberationPhase => 1;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 197, 190);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 194);

    public override int DefaultChaoResistance => 400;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };

    public bool IsRaging => Creature.GetPower<LanguageFloorRagePower>() != null;

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            return LanguageFloorScarletScarCreatureVisuals.Profile.AssetPaths
                .Concat(
                [
                    "res://images/powers/language_floor_anger_gauge_power.png",
                    "res://images/powers/language_floor_death_tracker_power.png",
                    AttackSfxPath,
                    FireSfxPath,
                    RageSfxPath,
                    UnrelievedSfxPath
                ])
                .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
                .Distinct();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        
            EnemyTurnCount = 0;
            PlannedMoveOne = -1;
            PlannedMoveTwo = -1;
            UnrelievedAnger = false;
            
        LanguageFloorDeathContext.Clear();
        
            await PowerCmdCompat.Apply<LanguageFloorAngerGaugePower>(
                Creature,
                1m,
                Creature,
                null,
                silent: true);
            await PowerCmdCompat.Apply<LanguageFloorRevengePassivePower>(
                Creature,
                1m,
                Creature,
                null,
                silent: true);
            
            await PowerCmdCompat.Apply<LanguageFloorDeathTrackerPower>(
                Creature,
                1m,
                Creature,
                null,
                silent: true);

        if (Creature.CombatState?.Encounter is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        LanguageFloorLiberationBackgroundController.SetRageBackground(UnrelievedAnger || IsRaging);
        EncounterBgmController.RegisterMonster(Creature);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (side != CombatSide.Enemy || Creature.IsDead)
        {
            return;
        }

        if (Creature.CurrentHp * 2 <= Creature.MaxHp
            && !IsRaging
            && !UnrelievedAnger)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                1m,
                0,
                IsPermanent: false,
                Creature,
                null);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _compositeIntents = new AbstractIntent[2];
        RefreshPlannedIntents();
        _compositeState = new MoveState(
            CompositeMoveId,
            PerformCompositeMove,
            _compositeIntents);
        _reviveAndEmpowerState = new LibraryPhaseTransitionMoveState(
            ReviveAndEmpowerMoveId,
            ReviveAndEmpowerMove,
            new HealIntent(),
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, rng) =>
            {
                PlanNextTurn(rng);
                return CompositeMoveId;
            });
        _compositeState.FollowUpState = router;
        _reviveAndEmpowerState.FollowUpState = router;
        return new MonsterMoveStateMachine(
            [_reviveAndEmpowerState, _compositeState, router],
            HasPlannedTurn ? _compositeState : router);
    }

    public bool UsesTargetedAttackContract(Creature owner) => true;

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        return LanguageFloorLiberationCombatHelper.GetPartnerThenPlayerTarget(owner) is { } target
            ? [target]
            : [];
    }

    public string GetTargetedAttackTargetName(Creature owner) =>
        GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "未知目标";

    public async Task TriggerReviveAndEmpowerState()
    {
        if (NCombatRoom.Instance?.GetCreatureNode(Creature) != null)
        {
            await CreatureCmd.TriggerAnim(Creature, "Hit", 0f);
        }

        ForceReviveAndEmpowerState();
    }

    public void ForceReviveAndEmpowerState()
    {
        if (_reviveAndEmpowerState != null)
        {
            SetMoveImmediate(
                _reviveAndEmpowerState,
                forceTransition: true);
        }
    }

    private async Task ReviveAndEmpowerMove(
        IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        await Cmd.CustomScaledWait(0.3f, 0.6f);
        if (Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseTransition(this);
        }
    }

    public async Task ChangeAnger(int delta)
    {
        await (Creature.GetPower<LanguageFloorAngerGaugePower>()?.ChangeAnger(delta)
            ?? Task.CompletedTask);
    }

    public async Task EnterRage()
    {
        if (Creature.IsDead || UnrelievedAnger || IsRaging)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(RageSfxPath);
        await LanguageFloorRagePower.ApplyWithDuration(Creature, Creature);
        await MultiplayerScalingPatchHelper.RescaleMonsterMaxHpAndRestoreDifference(
            Creature);
        if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals
            is LanguageFloorScarletScarCreatureVisuals visuals)
        {
            visuals.FacePlayers();
        }
        ForceSpecialFirstSlot();
        LanguageFloorLiberationBackgroundController.SetRageBackground(true);
        await RefreshIntents();
    }

    public void OnRageEnded()
    {
        if (!UnrelievedAnger)
        {
            LanguageFloorLiberationBackgroundController.SetRageBackground(false);
            if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals
                is LanguageFloorScarletScarCreatureVisuals visuals)
            {
                visuals.FacePartner();
            }
        }

        Creature.GetPower<LanguageFloorAngerGaugePower>()?.ResetAnger();
        TaskHelper.RunSafely(RefreshIntents());
    }

    public async Task EnterUnrelievedAnger()
    {
        if (Creature.IsDead || UnrelievedAnger)
        {
            return;
        }

        UnrelievedAnger = true;
        await MultiplayerScalingPatchHelper.RescaleMonsterMaxHpAndRestoreDifference(
            Creature);
        if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals
            is LanguageFloorScarletScarCreatureVisuals visuals)
        {
            visuals.FacePlayers();
        }
        LanguageFloorRevengePassivePower? passive =
            Creature.GetPower<LanguageFloorRevengePassivePower>();
        if (passive != null)
        {
            await PowerCmd.Remove(passive);
        }

        LanguageFloorRagePower? rage = Creature.GetPower<LanguageFloorRagePower>();
        if (rage != null)
        {
            await PowerCmd.Remove(rage);
        }

        Creature.GetPower<LanguageFloorAngerGaugePower>()?.ResetAnger();
        LocalOggOneShotPlayer.Play(UnrelievedSfxPath);
        await CreatureCmd.Heal(Creature, (int)Math.Ceiling(Creature.MaxHp * 0.5m));
        if (Creature is LibraryCreature libraryCreature)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(
                libraryCreature,
                libraryCreature.MaxChaoValue);
        }

        await PowerCmdCompat.Apply<LanguageFloorUnrelievedAngerPower>(Creature, 1m, Creature, null);
        ForceSpecialFirstSlot();
        LanguageFloorLiberationBackgroundController.SetRageBackground(true);
        await RefreshIntents();
    }

    private async Task PerformCompositeMove(IReadOnlyList<Creature> targets)
    {
        for (int slot = 0; slot < 2 && Creature.IsAlive; slot++)
        {
            await PerformMove(GetPlannedMove(slot));
            if (Creature.CombatState?.Encounter is LanguageFloorLiberationEncounter { PhaseComplete: true })
            {
                return;
            }
        }
    }

    private async Task PerformMove(LanguageFloorMoveKind move)
    {
        switch (move)
        {
            case LanguageFloorMoveKind.StableBreath:
                await ExecuteTargetedHits(GetMoveDamage(move), 2, "Attack", AttackSfxPath);
                await CreatureCmd.GainBlock(Creature, 11m, ValueProp.Move, null);
                break;
            case LanguageFloorMoveKind.HuntTarget:
                await ExecuteTargetedHits(GetMoveDamage(move), 2, "Attack", AttackSfxPath);
                await ApplyHuntMarkToCurrentTarget();
                break;
            case LanguageFloorMoveKind.HuntBeast:
                await ExecuteTargetedHits(GetMoveDamage(move), 2, "Attack", AttackSfxPath);
                Creature? flawTarget = LanguageFloorLiberationCombatHelper.GetPartnerThenPlayerTarget(Creature);
                if (flawTarget?.IsAlive == true)
                {
                    await LibraryPowerCmd.Apply<LibraryDisarmPower>(
                        flawTarget,
                        5,
                        1,
                        Creature,
                        null);
                }

                await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, 1m, Creature, null);
                break;
            case LanguageFloorMoveKind.ExplosiveShot:
                await ExecuteTargetedHits(GetMoveDamage(move), 2, "Fire", FireSfxPath);
                await ApplyHuntMarkToCurrentTarget();
                break;
            case LanguageFloorMoveKind.DecisiveStrike:
                await ExecuteGroupAttack(GetMoveDamage(move), 1, "Attack");
                await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, 3m, Creature, null);
                break;
            case LanguageFloorMoveKind.IndiscriminateShot:
                await ExecuteGroupAttack(GetMoveDamage(move), 3, "Shoot");
                break;
        }
    }

    private async Task ExecuteTargetedHits(int damage, int repeats, string animation, string sfxPath)
    {
        for (int i = 0; i < repeats && Creature.IsAlive; i++)
        {
            Creature? target = LanguageFloorLiberationCombatHelper.GetPartnerThenPlayerTarget(Creature);
            if (target == null)
            {
                return;
            }

            LocalOggOneShotPlayer.Play(sfxPath);
            using var forcedTargets = TargetedMonsterAttackHelper.ForceTargets(Creature, [target]);
            AttackCommand command = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim(animation, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
            if (target.Monster is LanguageFloorLostEverythingWolf
                && AttackCommandCompat.Results(command).Any(result => result.Receiver == target && result.UnblockedDamage > 0))
            {
                await ChangeAnger(-LanguageFloorAngerGaugePower.ScarletHitLoss);
            }
        }
    }

    private async Task ExecuteGroupAttack(int damage, int repeats, string animation)
    {
        IReadOnlyList<Creature> actualTargets =
            LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner(Creature);
        if (actualTargets.Count == 0)
        {
            return;
        }

        for (int i = 0; i < repeats && Creature.IsAlive; i++)
        {
            actualTargets = LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner(Creature);
            if (actualTargets.Count == 0)
            {
                return;
            }

            using var forcedTargets = TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets);
            string sfx = animation == "Shoot" ? FireSfxPath : AttackSfxPath;
            string trigger = animation == "Shoot" ? $"ShootS{i + 1}" : animation;
            LocalOggOneShotPlayer.Play(sfx);
            AttackCommand command = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim(trigger, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
            if (actualTargets.Any(target =>
                    target.Monster is LanguageFloorLostEverythingWolf
                    && AttackCommandCompat.Results(command)
                        .Any(result => result.Receiver == target && result.UnblockedDamage > 0)))
            {
                await ChangeAnger(-LanguageFloorAngerGaugePower.ScarletHitLoss);
            }
        }
    }

    private async Task ApplyHuntMarkToCurrentTarget()
    {
        Creature? target = LanguageFloorLiberationCombatHelper.GetPartnerThenPlayerTarget(Creature);
        if (target?.IsAlive == true)
        {
            await LibraryDurationPowerModel.ApplyWithDuration<LanguageFloorHuntMarkPower>(
                target,
                1,
                LanguageFloorHuntMarkPower.DefaultTurns,
                Creature,
                null);
        }
    }

    private static int GetMoveDamage(LanguageFloorMoveKind move)
    {
        int fallbackDamage = move switch
        {
            LanguageFloorMoveKind.StableBreath => 8,
            LanguageFloorMoveKind.HuntTarget => 7,
            LanguageFloorMoveKind.HuntBeast => 9,
            LanguageFloorMoveKind.ExplosiveShot => 11,
            LanguageFloorMoveKind.DecisiveStrike => 12,
            LanguageFloorMoveKind.IndiscriminateShot => 7,
            _ => 0
        };
        int ascendedDamage = move switch
        {
            LanguageFloorMoveKind.StableBreath => 9,
            LanguageFloorMoveKind.HuntTarget => 8,
            LanguageFloorMoveKind.HuntBeast => 10,
            LanguageFloorMoveKind.ExplosiveShot => 12,
            LanguageFloorMoveKind.DecisiveStrike => 13,
            LanguageFloorMoveKind.IndiscriminateShot => 8,
            _ => fallbackDamage
        };

        return AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            ascendedDamage,
            fallbackDamage);
    }

    private static AbstractIntent CreateIntent(LanguageFloorMoveKind move)
    {
        int damage = GetMoveDamage(move);
        return move switch
        {
            LanguageFloorMoveKind.StableBreath => new CombinedTargetedAttackDefendIntent(
                damage,
                2,
                "LANGUAGE_FLOOR_SCARLET_STABLE_BREATH.description",
                null,
                false,
                blockAmount: 11),
            LanguageFloorMoveKind.HuntTarget => new CombinedTargetedAttackDebuffIntent(
                damage,
                2,
                "LANGUAGE_FLOOR_SCARLET_HUNT_TARGET.description",
                null,
                false,
                IntentBadge.FromPower<LanguageFloorHuntMarkPower>(1, "1", "1")),
            LanguageFloorMoveKind.HuntBeast => new CombinedTargetedAttackDebuffIntent(
                damage,
                2,
                "LANGUAGE_FLOOR_SCARLET_HUNT_BEAST.description",
                null,
                false,
                IntentBadge.Flaw(5, 1),
                IntentBadge.NextTurnStrength(1)),
            LanguageFloorMoveKind.ExplosiveShot => new CombinedTargetedAttackDebuffIntent(
                damage,
                2,
                "LANGUAGE_FLOOR_SCARLET_EXPLOSIVE_SHOT.description",
                null,
                false,
                IntentBadge.FromPower<LanguageFloorHuntMarkPower>(1, "1", "1")),
            LanguageFloorMoveKind.DecisiveStrike => new IndiscriminateAttackIntent(
                damage,
                1,
                "LANGUAGE_FLOOR_SCARLET_DECISIVE_STRIKE.description",
                LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner,
                IntentBadge.NextTurnStrength(3)),
            LanguageFloorMoveKind.IndiscriminateShot => new IndiscriminateAttackIntent(
                damage,
                3,
                "LANGUAGE_FLOOR_SCARLET_INDISCRIMINATE_SHOT.description",
                LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner),
            _ => throw new ArgumentOutOfRangeException(nameof(move), move, null)
        };
    }

    private void RefreshPlannedIntents()
    {
        if (_compositeIntents == null)
        {
            return;
        }

        for (int slot = 0; slot < _compositeIntents.Length; slot++)
        {
            _compositeIntents[slot] = CreateIntent(GetPlannedMove(slot));
        }
    }

    private static IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return CreateIntent(LanguageFloorMoveKind.StableBreath);
        yield return CreateIntent(LanguageFloorMoveKind.HuntTarget);
        yield return CreateIntent(LanguageFloorMoveKind.HuntBeast);
        yield return CreateIntent(LanguageFloorMoveKind.ExplosiveShot);
        yield return CreateIntent(LanguageFloorMoveKind.DecisiveStrike);
        yield return CreateIntent(LanguageFloorMoveKind.IndiscriminateShot);
    }

    internal LanguageFloorMoveKind GetPlannedMove(int slot)
    {
        int value = slot == 0 ? PlannedMoveOne : PlannedMoveTwo;
        return Enum.IsDefined(typeof(LanguageFloorMoveKind), value)
            ? (LanguageFloorMoveKind)value
            : LanguageFloorMoveKind.StableBreath;
    }

    private void PlanNextTurn(Rng rng)
    {
        EnemyTurnCount++;
        if (UnrelievedAnger || IsRaging)
        {
            PlannedMoveOne = (int)LanguageFloorMoveKind.IndiscriminateShot;
        }
        else
        {
            IReadOnlyList<LanguageFloorMoveKind> candidates = GetNormalCandidates(EnemyTurnCount);
            PlannedMoveOne = (int)candidates[rng.NextInt(candidates.Count)];
        }

        IReadOnlyList<LanguageFloorMoveKind> secondCandidates = GetNormalCandidates(EnemyTurnCount);
        PlannedMoveTwo = (int)secondCandidates[rng.NextInt(secondCandidates.Count)];
        RefreshPlannedIntents();
    }

    internal static IReadOnlyList<LanguageFloorMoveKind> GetNormalCandidates(int enemyTurnCount) =>
        enemyTurnCount <= 2
            ?
            [
                LanguageFloorMoveKind.StableBreath,
                LanguageFloorMoveKind.HuntTarget,
                LanguageFloorMoveKind.HuntBeast,
                LanguageFloorMoveKind.ExplosiveShot
            ]
            :
            [
                LanguageFloorMoveKind.StableBreath,
                LanguageFloorMoveKind.HuntTarget,
                LanguageFloorMoveKind.HuntBeast,
                LanguageFloorMoveKind.ExplosiveShot,
                LanguageFloorMoveKind.DecisiveStrike
            ];

    internal IReadOnlyList<LanguageFloorMoveKind> PlannedMoves =>
        [GetPlannedMove(0), GetPlannedMove(1)];

    internal void DebugSetPlan(
        int enemyTurnCount,
        LanguageFloorMoveKind first,
        LanguageFloorMoveKind second,
        bool unrelievedAnger = false)
    {
        EnemyTurnCount = enemyTurnCount;
        PlannedMoveOne = (int)first;
        PlannedMoveTwo = (int)second;
        UnrelievedAnger = unrelievedAnger;
        RefreshPlannedIntents();
    }

    private bool HasPlannedTurn => PlannedMoveOne >= 0 && PlannedMoveTwo >= 0;

    private void ForceSpecialFirstSlot()
    {
        PlannedMoveOne = (int)LanguageFloorMoveKind.IndiscriminateShot;
        RefreshPlannedIntents();
        if (_compositeState != null)
        {
            SetMoveImmediate(_compositeState, forceTransition: true);
        }
    }

    private Task RefreshIntents()
    {
        return NCombatRoom.Instance?.GetCreatureNode(Creature)?.RefreshIntents() ?? Task.CompletedTask;
    }

}

using LibraryLib.Models;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.TechnologyFloorLiberation;
using LibraryOfRuina.cards.TechnologyFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.TechnologyFloorLiberation;
using LibraryOfRuina.visuals.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.TechnologyFloorLiberation;

public sealed class TechnologyFloorGrinderMk4Boss : LibraryMonsterModel, ILiberationPrimaryPhaseBoss
{
    private const int Phase = 2;

    public override int DefaultChaoResistance => 50;

    private const int NormalMinHp = 161;
    private const int NormalMaxHp = 163;
    private const int HighAscensionMinHp = 164;
    private const int HighAscensionMaxHp = 166;

    private const int SleepBlock = 15;
    private const int SleepEndureAmount = 2;
    private const int SleepEndureTurns = 1;

    private const int ChargeBaseDamage = 2;
    private const int ChargeHighAscensionDamage = 3;
    private const int ChargeHits = 3;
    private const int ChargeStrengthGain = 1;

    private const int CleanBaseDamage = 7;
    private const int CleanHighAscensionDamage = 8;
    private const int CleanVulnerable = 1;

    private const int EgoHitBaseDamage = 6;
    private const int EgoHitHighAscensionDamage = 7;
    private const int EgoHitCount = 3;
    private const int EgoBleedPerHit = 1;

    private const string SleepMoveId = "SLEEP_MK2";
    private const string ChargeMoveId = "CHARGE_MK2";
    private const string CleanMoveId = "CLEAN_MK2";
    private const string EgoMoveId = "EGO_LIMITER_RELEASE";
    private const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";

    internal const string BackgroundTextScope = "technology_floor_liberation_phase_2";
    private const float BackgroundTextIntervalSeconds = 5f;

    public const string IdleTexturePath = "res://images/monsters/technology_floor/grinder_mk4_idle.png";
    public const string HitTexturePath = "res://images/monsters/technology_floor/grinder_mk4_hit.png";
    public const string DodgeTexturePath = "res://images/monsters/technology_floor/grinder_mk4_dodge.png";
    public const string SlashTexturePath = "res://images/monsters/technology_floor/grinder_mk4_slash.png";
    public const string ThrustTexturePath = "res://images/monsters/technology_floor/grinder_mk4_thrust.png";
    public const string EgoS1TexturePath = "res://images/monsters/technology_floor/grinder_mk4_ego_s1.png";
    public const string EgoS2TexturePath = "res://images/monsters/technology_floor/grinder_mk4_ego_s2.png";
    public const string EgoS3TexturePath = "res://images/monsters/technology_floor/grinder_mk4_ego_s3.png";

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 190f, 980f, 470f);

    private static readonly string[] BackgroundTextLineKeys =
    [
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.0",
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.1",
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.2",
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.3",
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.4",
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.5",
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.6",
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.7",
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.8",
        "TECHNOLOGY_FLOOR_GRINDER_MK4_BOSS.backgroundText.normal.9"
    ];

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    private int _baseCadenceIndex;
    private bool _egoQueued;
    private bool _backgroundMoonTextLoopStarted;
    private MoveState? _egoState;
    private MoveState? _reviveAndEmpowerState;

    public int LiberationPhase => Phase;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMinHp, NormalMinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMaxHp, NormalMaxHp);

    public bool HasQueuedEgoSequence => _egoQueued;

    private int ChargeDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, ChargeHighAscensionDamage, ChargeBaseDamage);

    private int CleanDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, CleanHighAscensionDamage, CleanBaseDamage);

    private int EgoHitDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, EgoHitHighAscensionDamage, EgoHitBaseDamage);

    private bool ShouldShowUpgradedEgo =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 1, 0) == 1;

    public override IEnumerable<string> AssetPaths =>
        TechnologyFloorGrinderMk4BossCreatureVisuals.Profile.AssetPaths
            .Concat(new[] { TechnologyFloorRegretBoss.AttackSfxPath })
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _baseCadenceIndex = 0;
        _egoQueued = false;
        _backgroundMoonTextLoopStarted = false;

        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<LimiterReleaseEgoCard>());

        if (CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        TechnologyFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<TechnologyFloorErosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<TechnologyFloorMk4MaxChargePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<TechnologyFloorMk4IdentificationMk2Power>(Creature, 1m, Creature, null, silent: true);

        StartBackgroundMoonTextLoop();
    }

    public override Task BeforeCombatStart()
    {
        TechnologyFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        StartBackgroundMoonTextLoop();
        return Task.CompletedTask;
    }

    private void StartBackgroundMoonTextLoop()
    {
        if (!_backgroundMoonTextLoopStarted && Creature.IsAlive)
        {
            _backgroundMoonTextLoopStarted = true;
            MoonTextService.StartRandomLoop(
                Creature,
                BackgroundTextScope,
                BackgroundTextLineKeys.Select(L10NMonsterLookup).ToArray(),
                BackgroundTextIntervalSeconds,
                BackgroundTextSpawnArea);
        }
    }

    internal void StopBackgroundMoonTextLoop()
    {
        if (Creature != null)
        {
            MoonTextService.StopRandomLoop(Creature, BackgroundTextScope);
        }
    }

    public override void BeforeRemovedFromRoom()
    {
        StopBackgroundMoonTextLoop();
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Creature
            || Creature.CombatState?.Encounter is not TechnologyFloorLiberationEncounter encounter)
        {
            return Task.CompletedTask;
        }

        return encounter.OnPhaseBossDeath(this, wasRemovalPrevented, deathAnimLength);
    }

    public async Task QueueLimiterReleaseSequence()
    {
        if (_egoQueued || Creature.IsDead || _egoState == null)
        {
            return;
        }

        _egoQueued = true;
        SetMoveImmediate(_egoState, forceTransition: true);

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

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
            SetMoveImmediate(_reviveAndEmpowerState, forceTransition: true);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _reviveAndEmpowerState = new LibraryPhaseTransitionMoveState(
            ReviveAndEmpowerMoveId,
            ReviveAndEmpowerMove,
            new HealIntent(),
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        var sleep = new MoveState(
            SleepMoveId,
            SleepMove,
            new DefendIntent(),
            new BuffIntent());

        var charge = new MoveState(
            ChargeMoveId,
            ChargeMove,
            new MultiAttackIntent(ChargeDamage, ChargeHits),
            new BuffIntent());

        var clean = new MoveState(
            CleanMoveId,
            CleanMove,
            new SingleAttackIntent(CleanDamage),
            new DebuffIntent());

        var ego = new MoveState(
            EgoMoveId,
            EgoMove,
            new PlayCardAttackIntent<LimiterReleaseEgoCard>(
                "LIMITER_RELEASE_EGO_CARD",
                () => EgoHitDamage,
                static (card, damages) =>
                {
                    card.UpgradePreview();
                    card.SetPreviewDamage(damages[0]);
                },
                () => EgoHitCount),
            new DebuffIntent());
        _egoState = ego;

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(ego, () => _egoQueued);
        chooser.AddState(sleep, () => _baseCadenceIndex == 0);
        chooser.AddState(charge, () => _baseCadenceIndex == 1);
        chooser.AddState(clean, () => true);

        sleep.FollowUpState = chooser;
        charge.FollowUpState = chooser;
        clean.FollowUpState = chooser;
        ego.FollowUpState = chooser;
        _reviveAndEmpowerState.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[] { _reviveAndEmpowerState, sleep, charge, clean, ego, chooser },
            chooser);
    }

    private async Task SleepMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(TechnologyFloorRegretBoss.AttackSfxPath, -4f);
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);
        await CreatureCmd.GainBlock(Creature, SleepBlock, ValueProp.Move, null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            Creature,
            SleepEndureAmount,
            SleepEndureTurns,
            Creature,
            null);
        AdvanceBaseCadence();
    }

    private async Task ChargeMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < ChargeHits; i++)
        {
            await ExecuteAttackSegment("AttackThrust", ChargeDamage);
        }

        await PowerCmdCompat.Apply<StrengthPower>(Creature, ChargeStrengthGain, Creature, null);
        AdvanceBaseCadence();
    }

    private async Task CleanMove(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await ExecuteAttackSegment("AttackSlash", CleanDamage);

        IReadOnlyList<Creature> hitTargets = AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsAlive && result.Receiver.IsPlayer)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();

        if (hitTargets.Count == 0)
        {
            Creature? fallback = targets.FirstOrDefault(static t => t.IsAlive && t.IsPlayer);
            if (fallback != null)
            {
                hitTargets = new[] { fallback };
            }
        }

        if (hitTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<VulnerablePower>(hitTargets, CleanVulnerable, Creature, null);
        }

        AdvanceBaseCadence();
    }

    private async Task EgoMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < EgoHitCount; i++)
        {
            string animation = i switch
            {
                0 => "EgoS1",
                1 => "EgoS2",
                _ => "EgoS3"
            };

            AttackCommand attack = await ExecuteAttackSegment(animation, EgoHitDamage);

            IReadOnlyList<Creature> bleedTargets = AttackCommandCompat.Results(attack)
                .Where(static result => result.Receiver.IsAlive && result.Receiver.IsPlayer && !result.WasFullyBlocked)
                .Select(static result => result.Receiver)
                .Distinct()
                .ToArray();

            if (bleedTargets.Count > 0)
            {
                await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, EgoBleedPerHit, Creature, null);
            }
        }

        await PowerCmdCompat.Apply<TechnologyFloorMk4LimiterReleasedPower>(Creature, 1m, Creature, null);

        _egoQueued = false;
        AdvanceBaseCadence();
    }

    private async Task ReviveAndEmpowerMove(IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);
        await Cmd.CustomScaledWait(0.3f, 0.6f);

        if (Creature.CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseTransition(this);
        }
    }

    private Task<AttackCommand> ExecuteAttackSegment(string animation, int damage)
    {
        LocalOggOneShotPlayer.Play(TechnologyFloorRegretBoss.AttackSfxPath, -2f);
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(animation, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private void AdvanceBaseCadence()
    {
        _baseCadenceIndex = (_baseCadenceIndex + 1) % 3;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new DefendIntent();
        yield return new BuffIntent();
        yield return new MultiAttackIntent(ChargeDamage, ChargeHits);
        yield return new SingleAttackIntent(CleanDamage);
        yield return new DebuffIntent();
        yield return new HealIntent();
        yield return new PlayCardAttackIntent<LimiterReleaseEgoCard>(
            "LIMITER_RELEASE_EGO_CARD",
            () => EgoHitDamage,
            (card, damages) =>
            {
                if (ShouldShowUpgradedEgo)
                {
                    card.UpgradePreview();
                }
                card.SetPreviewDamage(damages[0]);
            },
            () => EgoHitCount);
    }
}

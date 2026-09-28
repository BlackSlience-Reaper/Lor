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
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.TechnologyFloorLiberation;
using LibraryOfRuina.visuals.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.monsters.TechnologyFloorLiberation;

public sealed class TechnologyFloorRegretBoss : LorMonsterModel, ILiberationPrimaryPhaseBoss
{
    private const int Phase = 1;

    public override int DefaultChaoResistance => 40;

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Resist
    };

    private const int NormalMinHp = 195;
    private const int NormalMaxHp = 199;
    private const int HighAscensionMinHp = 201;
    private const int HighAscensionMaxHp = 204;

    private const int BoundRageBaseDamage = 12;
    private const int BoundRageHighAscensionDamage = 13;

    private const int IronEchoBaseDamage = 6;
    private const int IronEchoHighAscensionDamage = 7;
    private const int IronEchoHits = 3;

    private const int EndBeginEndArtifactBase = 2;
    private const int EndBeginEndArtifactHighAscension = 3;

    private const string BoundRageMoveId = "BOUND_RAGE";
    private const string IronEchoMoveId = "IRON_ECHO";
    private const string EndBeginEndMoveId = "END_BEGIN_END";
    private const string EgoRegretMoveId = "EGO_REGRET";
    private const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";

    internal const string BackgroundTextScope = "technology_floor_liberation_phase_1";
    private const float BackgroundTextIntervalSeconds = 5f;

    public const string IdleTexturePath = "res://images/monsters/technology_floor/regret_idle.png";
    public const string AttackRightTexturePath = "res://images/monsters/technology_floor/regret_attack_right.png";
    public const string AttackLeftTexturePath = "res://images/monsters/technology_floor/regret_attack_left.png";
    public const string AttackSlashTexturePath = "res://images/monsters/technology_floor/regret_attack_slash.png";
    public const string HitTexturePath = "res://images/monsters/technology_floor/regret_hit.png";
    public const string ParryTexturePath = "res://images/monsters/technology_floor/regret_parry.png";
    public const string EgoTexturePath = "res://images/monsters/technology_floor/regret_ego_s1.png";
    public const string AttackSfxPath = "res://audio/sfx/technology_floor/regret/regret_attack.ogg";

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 190f, 980f, 470f);

    private static readonly string[] BackgroundTextLineKeys =
    [
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.0",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.1",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.2",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.3",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.4",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.5",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.6",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.7",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.8",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.9",
        "TECHNOLOGY_FLOOR_REGRET_BOSS.backgroundText.normal.10"
    ];

    private static readonly string[] SfxAssetPaths =
    [
        AttackSfxPath
    ];

    private int _baseCadenceIndex;
    private bool _endBeginEndQueued;
    private bool _egoRegretQueued;
    private bool _backgroundMoonTextLoopStarted;
    private MoveState? _endBeginEndState;
    private MoveState? _egoRegretState;
    private MoveState? _reviveAndEmpowerState;

    public int LiberationPhase => Phase;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMinHp, NormalMinHp);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, HighAscensionMaxHp, NormalMaxHp);

    private int BoundRageDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, BoundRageHighAscensionDamage, BoundRageBaseDamage);

    private int IronEchoDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, IronEchoHighAscensionDamage, IronEchoBaseDamage);

    private int EndBeginEndArtifactAmount =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, EndBeginEndArtifactHighAscension, EndBeginEndArtifactBase);

    private int EgoMultiHitDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, RegretEgoCard.MultiHitUpgradedDamage, RegretEgoCard.MultiHitDamage);

    private int EgoFinalDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, RegretEgoCard.FinalUpgradedDamage, RegretEgoCard.FinalDamage);

    public bool HasQueuedEndBeginEndSequence => _endBeginEndQueued || _egoRegretQueued;

    public override IEnumerable<string> AssetPaths =>
        TechnologyFloorRegretBossCreatureVisuals.Profile.AssetPaths
            .Concat(SfxAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _baseCadenceIndex = 0;
        _endBeginEndQueued = false;
        _egoRegretQueued = false;
        _backgroundMoonTextLoopStarted = false;

        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<RegretEgoCard>());

        if (CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        TechnologyFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<TechnologyFloorErosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<RegretIronEchoPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<RegretExtremeViolencePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<RegretFearPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<RegretEndBeginEndPower>(Creature, 1m, Creature, null, silent: true);

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

    public async Task QueueEndBeginEndSequence()
    {
        if (_endBeginEndQueued || _egoRegretQueued || Creature.IsDead || _endBeginEndState == null)
        {
            return;
        }

        _endBeginEndQueued = true;
        SetMoveImmediate(_endBeginEndState, forceTransition: true);

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

        var boundRage = new MoveState(
            BoundRageMoveId,
            BoundRageMove,
            new SingleAttackIntent(BoundRageDamage));

        var ironEcho = new MoveState(
            IronEchoMoveId,
            IronEchoMove,
            new MultiAttackIntent(IronEchoDamage, IronEchoHits),
            new DebuffIntent());

        var endBeginEnd = new MoveState(
            EndBeginEndMoveId,
            EndBeginEndMove,
            new BuffIntent());
        _endBeginEndState = endBeginEnd;

        var egoRegret = new MoveState(
            EgoRegretMoveId,
            EgoRegretMove,
            new PlayCardAttackIntent<RegretEgoCard>(
                "REGRET_EGO_CARD",
                () => EgoMultiHitDamage,
                (card, damages) =>
                {
                    card.SetPreviewDamage(damages[0], damages[1]);
                    card.UpgradePreview();
                },
                () => RegretEgoCard.MultiHitCount,
                () => EgoFinalDamage),
            new SingleAttackIntent(() => EgoFinalDamage),
            new DebuffIntent());
        _egoRegretState = egoRegret;

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(egoRegret, () => _egoRegretQueued);
        chooser.AddState(endBeginEnd, () => _endBeginEndQueued);
        chooser.AddState(boundRage, () => _baseCadenceIndex == 0);
        chooser.AddState(ironEcho, () => true);

        boundRage.FollowUpState = chooser;
        ironEcho.FollowUpState = chooser;
        endBeginEnd.FollowUpState = chooser;
        egoRegret.FollowUpState = chooser;
        _reviveAndEmpowerState.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[] { _reviveAndEmpowerState, boundRage, ironEcho, endBeginEnd, egoRegret, chooser },
            chooser);
    }

    private async Task BoundRageMove(IReadOnlyList<Creature> targets)
    {
        string animation = _baseCadenceIndex % 2 == 0 ? "AttackStrike" : "AttackSlash";
        await ExecuteAttackSegment(animation, BoundRageDamage);
        AdvanceBaseCadence();
    }

    private async Task IronEchoMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < IronEchoHits; i++)
        {
            string animation = (i % 3) switch
            {
                0 => "AttackStrike",
                1 => "AttackThrust",
                _ => "AttackSlash"
            };
            await ExecuteAttackSegment(animation, IronEchoDamage);
        }

        AdvanceBaseCadence();
    }

    private async Task EndBeginEndMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -4f);
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);
        await Cmd.CustomScaledWait(0.15f, 0.25f);

        IReadOnlyList<PowerModel> debuffs = Creature.Powers
            .Where(static p => p.Type == PowerType.Debuff)
            .ToArray();

        foreach (PowerModel debuff in debuffs)
        {
            await PowerCmd.Remove(debuff);
        }

        await PowerCmdCompat.Apply<ArtifactPower>(Creature, EndBeginEndArtifactAmount, Creature, null);

        _endBeginEndQueued = false;
        _egoRegretQueued = true;
    }

    private async Task EgoRegretMove(IReadOnlyList<Creature> targets)
    {
        Creature? primary = targets.FirstOrDefault(static t => t.IsAlive);

        for (int i = 0; i < RegretEgoCard.MultiHitCount; i++)
        {
            string animation = i % 2 == 0 ? "AttackStrike" : "AttackSlash";
            await ExecuteAttackSegment(animation, EgoMultiHitDamage);
        }

        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        AttackCommand finalAttack = await DamageCmd.Attack(EgoFinalDamage)
            .FromMonster(this)
            .WithAttackerAnim("EgoFinal", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        IReadOnlyList<Creature> confusionTargets = AttackCommandCompat.Results(finalAttack)
            .Where(static result => result.Receiver.IsAlive)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();

        if (confusionTargets.Count == 0 && primary != null && primary.IsAlive)
        {
            confusionTargets = new[] { primary };
        }

        if (confusionTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
                confusionTargets,
                RegretEgoCard.ConfusionAmount,
                Creature,
                null);
        }

        _egoRegretQueued = false;
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
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(animation, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private void AdvanceBaseCadence()
    {
        _baseCadenceIndex = (_baseCadenceIndex + 1) % 2;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new SingleAttackIntent(BoundRageDamage);
        yield return new MultiAttackIntent(IronEchoDamage, IronEchoHits);
        yield return new BuffIntent();
        yield return new HealIntent();
        yield return new DebuffIntent();
        yield return new PlayCardAttackIntent<RegretEgoCard>(
            "REGRET_EGO_CARD",
            () => EgoMultiHitDamage,
            (card, damages) =>
            {
                card.SetPreviewDamage(damages[0], damages[1]);
                card.UpgradePreview();
            },
            () => RegretEgoCard.MultiHitCount,
            () => EgoFinalDamage);
        yield return new SingleAttackIntent(EgoFinalDamage);
    }
}

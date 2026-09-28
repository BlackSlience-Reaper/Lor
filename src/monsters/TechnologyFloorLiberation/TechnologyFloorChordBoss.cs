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
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.TechnologyFloorLiberation;

public sealed class TechnologyFloorChordBoss : LorMonsterModel, ILiberationPrimaryPhaseBoss
{
    private const int Phase = 3;

    public override int DefaultChaoResistance => 55;

    private const int EgoMoveCountThreshold = 4;

    private const string EnsembleMoveId = "ENSEMBLE";
    private const string ShredMoveId = "SHRED";
    private const string CompositionMoveId = "COMPOSITION";
    private const string ChordEgoMoveId = "CHORD_EGO";
    private const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";

    internal const string BackgroundTextScope = "technology_floor_liberation_phase_3";
    private const float BackgroundTextIntervalSeconds = 5f;

    public const string Root = "res://images/monsters/technology_floor/chord/";
    public const string IdleTexturePath = Root + "chord_idle.png";
    public const string AttackFireTexturePath = Root + "chord_attack_fire.png";
    public const string AttackStrikeTexturePath = Root + "chord_attack_strike.png";
    public const string HitTexturePath = Root + "chord_hit.png";
    public const string ParryTexturePath = Root + "chord_parry.png";
    public const string EgoS1TexturePath = Root + "chord_ego_s1.png";
    public const string EgoS2TexturePath = Root + "chord_ego_s2.png";

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 190f, 980f, 470f);

    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.normal.0",
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.normal.1",
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.normal.2",
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.normal.3",
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.normal.4"
    ];

    private static readonly string[] CompositionBackgroundTextLineKeys =
    [
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.composition.0",
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.composition.1",
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.composition.2",
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.composition.3",
        "TECHNOLOGY_FLOOR_CHORD_BOSS.backgroundText.composition.4"
    ];

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Normal
    };

    private const int ShredBlock = 16;
    private const int CompositionStrength = 2;
    private const int EgoSelfStrength = 1;
    private const int EnsembleIntentCount = 2;
    private const int EgoEnsembleIntentCount = 5;

    private int _moveCount;
    private bool _egoQueued;
    private bool _backgroundMoonTextLoopStarted;
    private bool _usingCompositionText;
    private MoveState? _egoState;
    private MoveState? _reviveAndEmpowerState;

    public int LiberationPhase => Phase;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 234, 230);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 239, 235);

    public bool HasQueuedEgoSequence => _egoQueued;

    private static int EnsembleDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8);

    private static int ShredDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private static int EgoHitADamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private static int EgoHitBDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private static int EgoHitCDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    public override IEnumerable<string> AssetPaths =>
        TechnologyFloorChordBossCreatureVisuals.Profile.AssetPaths
            .Concat(base.AssetPaths.Skip(1))
            .Concat(new[] { TechnologyFloorRegretBoss.AttackSfxPath })
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _moveCount = 0;
        _egoQueued = false;
        _backgroundMoonTextLoopStarted = false;
        _usingCompositionText = false;

        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<ChordEgoCard>());

        if (CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(CombatState);
            encounter.MarkBossPhaseStarted(Phase);
        }

        TechnologyFloorLiberationBackgroundController.SetPhaseBackground(Phase);
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<TechnologyFloorErosionPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ChordInspiringPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ChordEnsemblePower>(Creature, 1m, Creature, null, silent: true);

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
            string[] keys = _usingCompositionText
                ? CompositionBackgroundTextLineKeys
                : NormalBackgroundTextLineKeys;
            MoonTextService.StartRandomLoop(
                Creature,
                BackgroundTextScope,
                keys.Select(L10NMonsterLookup).ToArray(),
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

    internal void OnStaffMelodyCravingTriggered()
    {
        if (_usingCompositionText)
        {
            return;
        }

        _usingCompositionText = true;
        StopBackgroundMoonTextLoop();
        _backgroundMoonTextLoopStarted = false;
        StartBackgroundMoonTextLoop();
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

        var ensemble = new MoveState(
            EnsembleMoveId,
            EnsembleMove,
            new SingleAttackIntent(() => EnsembleDamage),
            new DetailedBuffIntent<ChordEnsemblePower>(
                EnsembleIntentCount));

        var shred = new MoveState(
            ShredMoveId,
            ShredMove,
            new SingleAttackIntent(() => ShredDamage),
            new DefendIntent());

        var composition = new MoveState(
            CompositionMoveId,
            CompositionMove,
            new DetailedBuffIntent<StrengthPower>(CompositionStrength, DetailedBuffTargetScope.AllEnemies));

        var chordEgo = new MoveState(
            ChordEgoMoveId,
            ChordEgoMove,
            new BuffIntent(),
            new PlayCardAttackIntent<ChordEgoCard>(
                "CHORD_EGO_CARD",
                () => EgoHitADamage,
                static (card, damages) =>
                {
                    card.UpgradePreview();
                    card.SetPreviewDamage(
                        damages[0],
                        damages.Count > 1 ? damages[1] : 0,
                        damages.Count > 2 ? damages[2] : 0);
                }),
            new SingleAttackIntent(() => EgoHitBDamage),
            new SingleAttackIntent(() => EgoHitCDamage),
            new BuffIntent());
        _egoState = chordEgo;

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(chordEgo, () => _egoQueued);

        var rand = new RandomBranchState("RAND");
        rand.AddBranch(ensemble, MoveRepeatType.CannotRepeat);
        rand.AddBranch(shred, MoveRepeatType.CannotRepeat);
        rand.AddBranch(composition, MoveRepeatType.CannotRepeat);
        chooser.AddState(rand, () => true);

        ensemble.FollowUpState = chooser;
        shred.FollowUpState = chooser;
        composition.FollowUpState = chooser;
        chordEgo.FollowUpState = chooser;
        _reviveAndEmpowerState.FollowUpState = chooser;

        return new MonsterMoveStateMachine(
            new MonsterState[] { _reviveAndEmpowerState, ensemble, shred, composition, chordEgo, rand, chooser },
            chooser);
    }

    private async Task EnsembleMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment("AttackFire", EnsembleDamage);

        ChordEnsemblePower? ensemblePower = Creature.GetPower<ChordEnsemblePower>();
        if (ensemblePower != null)
        {
            await ensemblePower.TriggerEnsemble(EnsembleIntentCount);
        }

        AdvanceMoveCount();
    }

    private async Task ShredMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment("AttackStrike", ShredDamage);
        await CreatureCmd.GainBlock(Creature, ShredBlock, ValueProp.Move, null);

        AdvanceMoveCount();
    }

    private async Task CompositionMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.6f);

        if (Creature.CombatState is { } combatState)
        {
            IReadOnlyList<Creature> allies = combatState.Enemies
                .Where(e => e.IsAlive && e.Side == Creature.Side)
                .ToArray();

            if (allies.Count > 0)
            {
                await PowerCmdCompat.Apply<StrengthPower>(allies, CompositionStrength, Creature, null);
            }
        }

        AdvanceMoveCount();
    }

    private async Task ChordEgoMove(IReadOnlyList<Creature> targets)
    {
        ChordEnsemblePower? ensemblePower = Creature.GetPower<ChordEnsemblePower>();
        if (ensemblePower != null)
        {
            await ensemblePower.TriggerEnsemble(EgoEnsembleIntentCount);
        }

        AttackCommand attackA = await ExecuteAttackSegment("EgoS1", EgoHitADamage);
        IReadOnlyList<DamageResult> resultsA = AttackCommandCompat.Results(attackA);
        bool aFullyBlocked = resultsA.Count > 0 && resultsA.All(static r => r.WasFullyBlocked);

        if (aFullyBlocked)
        {
            AttackCommand attackB = await ExecuteAttackSegment("EgoS2", EgoHitBDamage);
            IReadOnlyList<DamageResult> resultsB = AttackCommandCompat.Results(attackB);
            bool bFullyBlocked = resultsB.Count > 0 && resultsB.All(static r => r.WasFullyBlocked);

            if (bFullyBlocked)
            {
                await ExecuteAttackSegment("EgoS2", EgoHitCDamage);
            }
        }

        await PowerCmdCompat.Apply<StrengthPower>(Creature, EgoSelfStrength, Creature, null);

        _egoQueued = false;
        _moveCount = 0;
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
            .Execute(null);
    }

    private void AdvanceMoveCount()
    {
        _moveCount++;
        if (_moveCount >= EgoMoveCountThreshold && !_egoQueued && _egoState != null)
        {
            _egoQueued = true;
            SetMoveImmediate(_egoState, forceTransition: true);

            NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
            creatureNode?.RefreshIntents();
        }
    }

}

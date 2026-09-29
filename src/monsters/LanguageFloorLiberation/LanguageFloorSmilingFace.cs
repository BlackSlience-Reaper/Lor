using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.backgrounds.LanguageFloorLiberation;
using LibraryOfRuina.content.abnormalities.SmilingBodies;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.intents;
using LibraryOfRuina.visuals.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

using BaseSmilingBodies =
    content.abnormalities.SmilingBodies.SmilingBodies;

public enum LanguageFloorSmilingFaceForm
{
    First,
    Second,
    Third
}

internal enum LanguageFloorSmilingFaceMove
{
    Devour,//吞噬
    Absorb,//吸收
    Sit,//瘫坐
    Scream,//惨叫
    Vomit//呕吐
}

// 按职责分部：本文件放战斗状态、状态机与回合钩子；.Forms 放假死与形态切换，.CorpseTrial 放尸体生成与尸体审判，
// .Plan 放每回合计划与目标，.Moves 放招式执行与意图，.Debug 放只给验证程序集用的入口。
// 战斗状态只在本场战斗内有效：原版不存怪物模型（中途退出重进会重开战斗），联机也只同步生命、格挡与能力层数，
// 所以这些属性不标 [SavedProperty]。
public sealed partial class LanguageFloorSmilingFace :
    LiberationPhaseBossMonster,
    ITargetedMonsterAttackProvider
{
    public const int MaxCorpseCount = 3;
    public const int MaxChaoResistance = 100;
    public const int FormOneEntryHp = 100;
    public const int FormTwoEntryHp = 200;
    public const int FormThreeEntryHp = 300;
    public const int CorpseSpawnThresholdPercent = 25;
    public const int EntryCorpseCount = 1;
    public const int TrialCorpseCount = 3;
    public const int TrialPlayerTurns = 2;
    public const int PermanentStrongPerPlayerTurn = 1;
    public const int DevourHits = 2;
    public const int DevourHealPercentPerHit = 5;
    public const int AbsorbNextTurnStrength = 1;
    public const int SitHits = 2;
    public const int SitVulnerable = 1;
    public const int ScreamHits = 2;
    public const int VomitDebuffAmount = 3;
    public const int VomitDebuffTurns = 2;
    public const int FormOneIntentCapacity = 3;
    public const int FormTwoIntentCapacity = 2;
    public const int FormThreeIntentCapacity = 1;
    private const int StoredIntentSlotCount = 4;

    public const string DevourMoveId = "DEVOUR";
    public const string AbsorbMoveId = "ABSORB";
    public const string SitMoveId = "SIT";
    public const string ScreamMoveId = "SCREAM";
    public const string VomitMoveId = "VOMIT";
    public const string ReviveMoveId = "REVIVE";
    private const string RouterMoveId = "LANGUAGE_FLOOR_SMILING_FACE_ROUTER";
    private const string FakeDeathHiddenMoveId = "FAKE_DEATH_HIDDEN";
    private const string FormOneCompositeMoveId = "FORM_ONE_COMPOSITE";
    private const string FormTwoCompositeMoveId = "FORM_TWO_COMPOSITE";
    private const string FormThreeCompositeMoveId = "FORM_THREE_COMPOSITE";

    public const string Root =
        "res://images/monsters/language_floor_liberation/smiling_face/";
    public const string IdleTexturePath = Root + "smiling_face_idle.png";
    public const string AttackThrustTexturePath =
        Root + "smiling_face_attack_thrust.png";
    public const string AttackSlashTexturePath =
        Root + "smiling_face_attack_slash.png";
    public const string HitTexturePath = Root + "smiling_face_hit.png";
    public const string ScreamTexturePath = Root + "smiling_face_scream.png";
    public const string VomitTexturePath = Root + "smiling_face_vomit.png";

    private const float SegmentDelaySeconds = 0.45f;

    public LanguageFloorSmilingFaceForm Form { get; private set; } =
        LanguageFloorSmilingFaceForm.Third;

    public bool Initialized { get; private set; }

    public int FormOneMaxHp { get; private set; }

    public int FormTwoMaxHp { get; private set; }

    public int FormThreeMaxHp { get; private set; }

    public int FormTurnCount { get; private set; }

    public int PreviousNormalMove { get; private set; } = -1;

    public int PendingCorpseSpawns { get; private set; }

    public int HpAtLastSpawnThreshold { get; private set; }

    public bool WaitingForDowngrade { get; private set; }

    public int FakeDeathPlayerTurnsRemaining { get; private set; }

    public int PendingFormTransition { get; private set; } = -1;

    public bool PendingFormTransitionIsPromotion { get; private set; }

    public bool CorpseTrialPending { get; private set; }

    public bool CorpseTrialActive { get; private set; }

    public int CorpseTrialPlayerTurnsRemaining { get; private set; }

    public bool ForceKillable { get; private set; }

    public int PlannedMoveOne { get; private set; } = -1;

    public int PlannedMoveTwo { get; private set; } = -1;

    public int PlannedMoveThree { get; private set; } = -1;

    public int PlannedMoveFour { get; private set; } = -1;

    public int PlannedTargetOne { get; private set; } = -1;

    public int PlannedTargetTwo { get; private set; } = -1;

    public int PlannedTargetThree { get; private set; } = -1;

    public int PlannedTargetFour { get; private set; } = -1;

    private bool _isApplyingFormStats;
    private bool _isTransitioning;
    private int _lastCorpseTrialTickPlayerTurn = -1;
    private MoveState? _devourState;
    private MoveState? _absorbState;
    private MoveState? _sitState;
    private MoveState? _screamState;
    private MoveState? _vomitState;
    private MoveState? _reviveState;
    private MoveState? _formOneCompositeState;
    private MoveState? _formTwoCompositeState;
    private MoveState? _formThreeCompositeState;
    private PlannedMoveController<LanguageFloorSmilingFaceMove>? _plan;

    public static readonly string[] PowerIconPaths =
    [
        "res://images/powers/language_floor_smiling_face_find_corpses_power.png",
        "res://images/powers/language_floor_smiling_face_form_one_split_power.png",
        "res://images/powers/language_floor_smiling_face_fusion_power.png",
        "res://images/powers/language_floor_smiling_face_split_and_fusion_power.png",
        "res://images/powers/language_floor_smiling_face_scream_power.png",
        "res://images/powers/language_floor_smiling_face_form_three_split_power.png",
        "res://images/powers/language_floor_smiling_face_vomit_power.png"
    ];

    public static readonly string[] AssetPathsStatic =
        LanguageFloorSmilingFaceCreatureVisuals.Profile.AssetPaths
            .Concat(
            [
                BaseSmilingBodies.AbsorbHitSfxPath,
                BaseSmilingBodies.Phase2ScreamSfxPath,
                BaseSmilingBodies.Phase3SitHitSfxPath,
                BaseSmilingBodies.Phase3VomitSfxPath,
                BaseSmilingBodies.PhaseDownSfxPath,
                BaseSmilingBodies.PhaseUpSfxPath,
                MeltingCorpse.SpawnSfxPath
            ])
            .Concat(PowerIconPaths)
            .ToArray();

    public bool IsFakeDead =>
        !ForceKillable
        && (WaitingForDowngrade || CorpseTrialPending || CorpseTrialActive);

    public override int LiberationPhase => 3;

    protected override bool ReviveTriggerPlaysHitAnimation => true;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            348,
            340);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            350,
            345);

    public override int DefaultChaoResistance => MaxChaoResistance;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Resist,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Normal
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Resist,
            Pierce = LibraryResistanceLevel.Endure,
            Blunt = LibraryResistanceLevel.Normal
        };

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(EnumerateIntentAssets().SelectMany(static intent =>
                intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (Creature.CombatState?.Encounter is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        if (!Initialized)
        {
            Initialized = true;
            await ApplyFormStats(Form, resetMaxHp: true);
            ResetSpawnThreshold();
        }
        await RefreshFormPowers();
        LanguageFloorLiberationBackgroundController.SetPhaseThreeBackground();
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            int playerTurn = GetCurrentPlayerTurn(combatState);
            if (playerTurn >= 0 && playerTurn == _lastCorpseTrialTickPlayerTurn)
            {
                Log.Info(
                    "[LanguageFloorSmilingFace] player-turn start already handled "
                    + $"by fake-death power turn={playerTurn}");
            }
            else
            {
                if (playerTurn >= 0)
                {
                    _lastCorpseTrialTickPlayerTurn = playerTurn;
                }

                await ResolvePlayerTurnStart(choiceContext, combatState);
            }
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);
        if (creature != Creature || _isApplyingFormStats || ForceKillable)
        {
            return;
        }

        if (delta < 0m)
        {
            QueueCorpseSpawnsForThresholds();
        }
        else if (delta > 0m)
        {
            await TryPromoteAtFullHealth();
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        // 计划控制器的委托捕获的是被克隆的实例，克隆体必须用自己的。
        _plan = null;
    }

    // 形态切换与审判失败时 ResetStateMachine 后重建：三个复合行动与假死隐藏行动用同一个 ID 重建，
    // 控制器里的旧意图数组随之被顶替。
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _devourState = new MoveState(
            DevourMoveId,
            DevourMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Devour));
        _absorbState = new MoveState(
            AbsorbMoveId,
            AbsorbMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Absorb));
        _sitState = new MoveState(
            SitMoveId,
            SitMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Sit));
        _screamState = new MoveState(
            ScreamMoveId,
            ScreamMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Scream));
        _vomitState = new MoveState(
            VomitMoveId,
            VomitMove,
            CreateIntent(LanguageFloorSmilingFaceMove.Vomit));
        _reviveState = new LibraryPhaseTransitionMoveState(
            ReviveMoveId,
            ReviveMove,
            new HealIntent(),
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();
        MoveState fakeDeathHiddenState = Plan.CreateHiddenState(
            FakeDeathHiddenMoveId,
            mustPerformOnce: true);
        fakeDeathHiddenState.FollowUpState = fakeDeathHiddenState;
        _formOneCompositeState = Plan.CreateCompositeState(
            FormOneCompositeMoveId,
            PerformCompositeMove,
            intentCount: FormOneIntentCapacity);
        _formTwoCompositeState = Plan.CreateCompositeState(
            FormTwoCompositeMoveId,
            PerformCompositeMove,
            intentCount: FormTwoIntentCapacity);
        _formThreeCompositeState = Plan.CreateCompositeState(
            FormThreeCompositeMoveId,
            PerformCompositeMove,
            intentCount: FormThreeIntentCapacity);

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, rng) =>
            {
                PlanTurn(rng);
                return GetCurrentCompositeState().Id;
            });
        foreach (MoveState move in new[]
                 {
                     _devourState,
                     _absorbState,
                     _sitState,
                     _screamState,
                     _vomitState,
                     _reviveState,
                     reviveAndEmpower,
                     _formOneCompositeState,
                     _formTwoCompositeState,
                     _formThreeCompositeState
                 })
        {
            move.FollowUpState = router;
        }

        return new MonsterMoveStateMachine(
            [
                _devourState,
                _absorbState,
                _sitState,
                _screamState,
                _vomitState,
                _reviveState,
                reviveAndEmpower,
                fakeDeathHiddenState,
                _formOneCompositeState,
                _formTwoCompositeState,
                _formThreeCompositeState,
                router
            ],
            IsFakeDead
                ? fakeDeathHiddenState
                : HasPlannedTurn
                    ? GetCurrentCompositeState()
                    : router);
    }

    private async Task ResolvePlayerTurnStart(
        PlayerChoiceContext choiceContext,
        CombatStateLike combatState)
    {
        await ResolvePendingFormTransition();

        if (await ResolveCorpseTrial(combatState))
        {
            return;
        }

        await ResolvePendingCorpseSpawns(combatState);

        if (Creature.IsAlive
            && Form is LanguageFloorSmilingFaceForm.Second
                or LanguageFloorSmilingFaceForm.Third)
        {
            await GainPermanentStrong(choiceContext);
        }

        if (!IsFakeDead && NextMove.Id == FakeDeathHiddenMoveId)
        {
            Log.Warn(
                "[LanguageFloorSmilingFace] stale FAKE_DEATH_HIDDEN after "
                + "trial resolution; resetting move state.");
            ResetMoveStateForCurrentForm();
            Creature.PrepareForNextTurn(combatState.PlayerCreatures);
        }
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;
}

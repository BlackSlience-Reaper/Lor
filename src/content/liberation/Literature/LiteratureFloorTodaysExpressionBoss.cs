using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Literature;

public sealed class LiteratureFloorTodaysExpressionBoss :
    LiberationPhaseBossMonster
{
    public const int Phase = 4;
    public const string JoyousFaceMoveId = "JOYOUS_FACE";
    public const string SmilingFaceMoveId = "SMILING_FACE";
    public const string RestingFaceMoveId = "RESTING_FACE";
    public const string SadFaceMoveId = "SAD_FACE";
    public const string AngryFaceMoveId = "ANGRY_FACE";
    public const string WaveringFeelingsMoveId = "WAVERING_FEELINGS";

    public const int JoyousFaceBlock = 33;
    public const int SmilingFaceBlock = 24;
    public const int SmilingFaceWeak = 3;
    public const int RestingFaceBlock = 20;
    public const int SadFaceVulnerable = 3;
    public const int AngryFaceHits = 3;
    public const int WaveringFeelingsConfusion = 1;

    private const string Root =
        "res://images/monsters/literature_floor_liberation/todays_expression/";
    public const string IdleTexturePath =
        Root + "todays_expression_idle.png";
    public const string HitTexturePath =
        Root + "todays_expression_hit.png";
    public const string ThrustTexturePath =
        Root + "todays_expression_thrust.png";
    public const string GuardTexturePath =
        Root + "todays_expression_guard.png";
    public const string S1TexturePath =
        Root + "todays_expression_s1.png";
    public const string S2TexturePath =
        Root + "todays_expression_s2.png";

    private const string SfxRoot =
        "res://audio/sfx/literature_floor_liberation/todays_expression/";
    public const string SmileSfxPath = SfxRoot + "shy_smile.ogg";
    public const string AngrySfxPath = SfxRoot + "shy_angry.ogg";
    public const string AttackSfxPath = SfxRoot + "shy_attack.ogg";
    public const string StrongAttackSfxPath =
        SfxRoot + "shy_strong_attack.ogg";
    public const string StrongGuardSfxPath =
        SfxRoot + "shy_strong_guard.ogg";

    internal const string BackgroundTextScope =
        "literature_floor_todays_expression_phase_4";
    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea =
        new(150f, 200f, 900f, 450f);
    private static readonly string[] BackgroundTextLineKeys =
    [
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal.0",
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal.1",
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal.2",
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal.3",
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal.4",
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal.5",
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal.6",
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal.7"
    ];

    private Dictionary<int, MoveState> _normalStates = [];
    private MoveState? _waveringFeelingsState;
    private bool _backgroundTextStarted;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _normalStates = [];
        _waveringFeelingsState = null;
        ClearReviveAndEmpowerState();
    }

    public override int LiberationPhase => Phase;

    public int CurrentExpression =>
        ExpressionFromMoveId(NextMove.StateId);

    internal bool IsWaveringFeelingsQueued =>
        NextMove.StateId == WaveringFeelingsMoveId;

    private int JoyousFaceEndurance =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            2,
            1);

    private int RestingFaceDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            19,
            16);

    private int SadFaceDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            24,
            21);

    private int AngryFaceDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            11,
            9);

    private int WaveringFeelingsDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            28,
            23);

    internal static (int Min, int Max) DebugHpRange(bool toughEnemies) =>
        toughEnemies ? (192, 200) : (180, 188);

    internal static int DebugJoyousFaceEndurance(bool deadlyEnemies) =>
        deadlyEnemies ? 2 : 1;

    internal static int DebugRestingFaceDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 19 : 16;

    internal static int DebugSadFaceDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 24 : 21;

    internal static int DebugAngryFaceDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 11 : 9;

    internal static int DebugWaveringFeelingsDamage(
        bool deadlyEnemies) => deadlyEnemies ? 28 : 23;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            192,
            180);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            200,
            188);

    public override int DefaultChaoResistance => 100;

    public override bool ShouldDisappearFromDoom => false;

    public override bool HasDeathSfx => false;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => UniformResistance(
            LibraryResistanceLevel.Normal);

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => UniformResistance(
            LibraryResistanceLevel.Endure);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                LiteratureFloorTodaysExpressionCreatureVisuals.ScenePath,
                IdleTexturePath,
                HitTexturePath,
                ThrustTexturePath,
                GuardTexturePath,
                S1TexturePath,
                S2TexturePath,
                SmileSfxPath,
                AngrySfxPath,
                AttackSfxPath,
                StrongAttackSfxPath,
                StrongGuardSfxPath,
                "res://images/powers/library_passive_green.png",
                "res://images/powers/history_floor_corrosion_power.png"
            };
            paths.AddRange(
                LiteratureFloorTodaysExpressionCreatureVisuals
                    .FaceTexturePaths);
            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _backgroundTextStarted = false;
        LiteratureFloorLiberationBackgroundController.SetPhaseBackground(
            Phase);
        EncounterBgmController.RegisterMonster(Creature);

        if (Creature.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        await PowerCmdCompat.Apply<HistoryFloorCorrosionPower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<LiteratureFloorExpressionPassivePower>(
            Creature,
            LiteratureFloorExpressionPassivePower
                .ResolveCardsRequiredForCombatState(Creature.CombatState),
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<
            LiteratureFloorWaveringFeelingsPassivePower>(
            Creature,
            LiteratureFloorWaveringFeelingsPassivePower.Interval,
            Creature,
            null,
            silent: true);

        await SelectRandomNormalExpression(
            excludeCurrent: false,
            playFeedback: false);
        StartBackgroundText();
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead)
        {
            ApplyExpressionPresentation(
                CurrentExpression,
                playFeedback: false);
            StartBackgroundText();
        }

        return Task.CompletedTask;
    }

    public override void BeforeRemovedFromRoom()
    {
        StopBackgroundText();
        base.BeforeRemovedFromRoom();
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return;
        }

        StopBackgroundText();
        if (Creature.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter)
        {
            await encounter.OnPhaseBossDeath(
                this,
                wasRemovalPrevented,
                deathAnimLength);
        }
    }

    internal async Task<bool> RandomizeExpressionFromUnblockedHit()
    {
        if (Creature.IsDead
            || Creature.IsStunned
            || IsWaveringFeelingsQueued
            || NextMove.StateId == ReviveAndEmpowerMoveId
            || Creature.CombatState?.Encounter
                is not LiteratureFloorLiberationEncounter
                {
                    CurrentPhase: Phase,
                    PhaseComplete: false,
                    TransitionPending: false
                })
        {
            return false;
        }

        await SelectRandomNormalExpression(
            excludeCurrent: true,
            playFeedback: true);
        return true;
    }

    internal async Task<bool> TryQueueWaveringFeelings()
    {
        if (_waveringFeelingsState == null
            || Creature.IsDead
            || Creature.IsStunned
            || IsWaveringFeelingsQueued
            || NextMove.StateId == ReviveAndEmpowerMoveId
            || NextMove.StateId == stunnedMoveId
            || Creature.CombatState?.Encounter
                is not LiteratureFloorLiberationEncounter
                {
                    CurrentPhase: Phase,
                    PhaseComplete: false,
                    TransitionPending: false
                })
        {
            return false;
        }

        SetMoveImmediate(
            _waveringFeelingsState,
            forceTransition: true);
        if (CombatQueries.CreatureNodeOf(this) is { } node)
        {
            await node.RefreshIntents();
        }

        return true;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _normalStates.Clear();

        MoveState joyousFace = new(
            JoyousFaceMoveId,
            JoyousFaceMove,
            new CombinedDefendBuffIntent(
                JoyousFaceBlock,
                "LITERATURE_FLOOR_TODAYS_EXPRESSION_JOYOUS_FACE.description",
                IntentBadge.Guard(JoyousFaceEndurance)));
        MoveState smilingFace = new(
            SmilingFaceMoveId,
            SmilingFaceMove,
            new CombinedDefendDebuffIntent(
                SmilingFaceBlock,
                "LITERATURE_FLOOR_TODAYS_EXPRESSION_SMILING_FACE.description",
                IntentBadge.Weak(SmilingFaceWeak)));
        MoveState restingFace = new(
            RestingFaceMoveId,
            RestingFaceMove,
            new CombinedAttackDefendIntent(
                () => RestingFaceDamage,
                () => 1,
                "LITERATURE_FLOOR_TODAYS_EXPRESSION_RESTING_FACE.description",
                RestingFaceBlock));
        MoveState sadFace = new(
            SadFaceMoveId,
            SadFaceMove,
            new CombinedAttackDebuffIntent(
                () => SadFaceDamage,
                () => 1,
                "LITERATURE_FLOOR_TODAYS_EXPRESSION_SAD_FACE.description",
                IntentBadge.Vulnerable(SadFaceVulnerable)));
        MoveState angryFace = new(
            AngryFaceMoveId,
            AngryFaceMove,
            new MultiAttackIntent(AngryFaceDamage, AngryFaceHits));
        _waveringFeelingsState = new MoveState(
            WaveringFeelingsMoveId,
            WaveringFeelingsMove,
            new CombinedAttackDebuffIntent(
                () => WaveringFeelingsDamage,
                () => 1,
                "LITERATURE_FLOOR_TODAYS_EXPRESSION_WAVERING_FEELINGS.description",
                IntentBadge.Confusion(WaveringFeelingsConfusion)))
        {
            MustPerformOnceBeforeTransitioning = true
        };
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        _normalStates[1] = joyousFace;
        _normalStates[2] = smilingFace;
        _normalStates[3] = restingFace;
        _normalStates[4] = sadFace;
        _normalStates[5] = angryFace;
        foreach (MoveState state in _normalStates.Values)
        {
            state.FollowUpState = state;
        }

        _waveringFeelingsState.FollowUpState = restingFace;
        reviveAndEmpower.FollowUpState = restingFace;

        return new MonsterMoveStateMachine(
        [
            joyousFace,
            smilingFace,
            restingFace,
            sadFace,
            angryFace,
            _waveringFeelingsState,
            reviveAndEmpower
        ], restingFace);
    }

    private async Task JoyousFaceMove(IReadOnlyList<Creature> targets)
    {
        await PlayGuardAnimation();
        await CreatureCmd.GainBlock(
            Creature,
            JoyousFaceBlock,
            ValueProp.Move,
            null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            Creature,
            JoyousFaceEndurance,
            turns: -1,
            Creature,
            null);
        await SelectRandomNormalExpression(
            excludeCurrent: true,
            playFeedback: true);
    }

    private async Task SmilingFaceMove(IReadOnlyList<Creature> targets)
    {
        await PlayGuardAnimation();
        await CreatureCmd.GainBlock(
            Creature,
            SmilingFaceBlock,
            ValueProp.Move,
            null);
        await PowerCmdCompat.ApplyDebuff<WeakPower>(
            LivingPlayers(),
            SmilingFaceWeak,
            Creature,
            null);
        await SelectRandomNormalExpression(
            excludeCurrent: true,
            playFeedback: true);
    }

    private async Task RestingFaceMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteTimedAttack(
            RestingFaceDamage,
            1,
            "Attack",
            LiteratureFloorTodaysExpressionAnimationContract
                .AttackHitFrameTimesSeconds,
            LiteratureFloorTodaysExpressionAnimationContract
                .AttackDurationSeconds,
            AttackSfxPath);
        await CreatureCmd.GainBlock(
            Creature,
            RestingFaceBlock,
            ValueProp.Move,
            null);
        await SelectRandomNormalExpression(
            excludeCurrent: true,
            playFeedback: true);
    }

    private async Task SadFaceMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteTimedAttack(
            SadFaceDamage,
            1,
            "Attack",
            LiteratureFloorTodaysExpressionAnimationContract
                .AttackHitFrameTimesSeconds,
            LiteratureFloorTodaysExpressionAnimationContract
                .AttackDurationSeconds,
            AttackSfxPath);
        await PowerCmdCompat.ApplyDebuff<VulnerablePower>(
            LivingPlayers(),
            SadFaceVulnerable,
            Creature,
            null);
        await SelectRandomNormalExpression(
            excludeCurrent: true,
            playFeedback: true);
    }

    private async Task AngryFaceMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteTimedAttack(
            AngryFaceDamage,
            AngryFaceHits,
            "Angry",
            LiteratureFloorTodaysExpressionAnimationContract
                .AngryHitFrameTimesSeconds,
            LiteratureFloorTodaysExpressionAnimationContract
                .AngryDurationSeconds,
            AttackSfxPath);
        await SelectRandomNormalExpression(
            excludeCurrent: true,
            playFeedback: true);
    }

    private async Task WaveringFeelingsMove(
        IReadOnlyList<Creature> targets)
    {
        Creature.GetPower<
                LiteratureFloorWaveringFeelingsPassivePower>()
            ?.MarkSpecialUsed();
        await ExecuteTimedAttack(
            WaveringFeelingsDamage,
            1,
            "WaveringFeelings",
            LiteratureFloorTodaysExpressionAnimationContract
                .WaveringFeelingsHitFrameTimesSeconds,
            LiteratureFloorTodaysExpressionAnimationContract
                .WaveringFeelingsDurationSeconds,
            StrongAttackSfxPath);
        await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaConfusionPower>(
            LivingPlayers(),
            WaveringFeelingsConfusion,
            Creature,
            null);
        await SelectRandomNormalExpression(
            excludeCurrent: false,
            playFeedback: true);
    }

    protected override async Task PlayReviveAndEmpowerAnimation()
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0f);
        await Cmd.Wait(
            LiteratureFloorTodaysExpressionAnimationContract
                .CastDurationSeconds);
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private async Task PlayGuardAnimation()
    {
        LocalOggOneShotPlayer.Play(StrongGuardSfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0f);
        await Cmd.Wait(
            LiteratureFloorTodaysExpressionAnimationContract
                .GuardDurationSeconds);
    }

    private async Task ExecuteTimedAttack(
        int damage,
        int hitCount,
        string animation,
        IReadOnlyList<float> hitTimes,
        float totalDuration,
        string sfxPath)
    {
        LocalOggOneShotPlayer.Play(sfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(Creature, animation, 0f);
        int hitIndex = 0;
        float previousHitTime = 0f;
        await DamageCmd.Attack(damage)
            .WithHitCount(hitCount)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx("vfx/vfx_attack_slash")
            .BeforeDamage(async () =>
            {
                if (hitIndex >= hitTimes.Count)
                {
                    return;
                }

                float hitTime = hitTimes[hitIndex];
                await Cmd.Wait(Math.Max(0f, hitTime - previousHitTime));
                previousHitTime = hitTime;
                hitIndex++;
            })
            .Execute(null);
        await Cmd.Wait(Math.Max(0f, totalDuration - previousHitTime));
    }

    private async Task SelectRandomNormalExpression(
        bool excludeCurrent,
        bool playFeedback)
    {
        if (_normalStates.Count != 5)
        {
            return;
        }

        if (Creature.IsDead
            || Creature.CombatState?.Encounter
                is LiteratureFloorLiberationEncounter
                {
                    TransitionPending: true
                })
        {
            return;
        }

        int current = excludeCurrent ? CurrentExpression : 0;
        int[] candidates = _normalStates.Keys
            .Where(expression => expression != current)
            .OrderBy(static expression => expression)
            .ToArray();
        int selected = candidates[
            RunRng.MonsterAi.NextInt(0, candidates.Length)];
        SetMoveImmediate(
            _normalStates[selected],
            forceTransition: true);
        ApplyExpressionPresentation(selected, playFeedback);
        if (CombatQueries.CreatureNodeOf(this) is { } node)
        {
            await node.RefreshIntents();
        }
    }

    private void ApplyExpressionPresentation(
        int expression,
        bool playFeedback)
    {
        if (expression is < 1 or > 5)
        {
            return;
        }

        if (CombatQueries.CreatureNodeOf(this)?.Visuals
            is LiteratureFloorTodaysExpressionCreatureVisuals visuals)
        {
            visuals.SetExpression(expression, playFeedback);
        }

        if (playFeedback)
        {
            LocalOggOneShotPlayer.Play(
                expression <= 2 ? SmileSfxPath : AngrySfxPath,
                -2f);
        }
    }

    private void StartBackgroundText()
    {
        if (_backgroundTextStarted || Creature.IsDead)
        {
            return;
        }

        _backgroundTextStarted = true;
        MoonTextService.StartRandomLoop(
            Creature,
            BackgroundTextScope,
            BackgroundTextLineKeys
                .Select(L10NMonsterLookup)
                .ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    internal void StopBackgroundText()
    {
        if (!_backgroundTextStarted)
        {
            return;
        }

        MoonTextService.StopRandomLoop(Creature, BackgroundTextScope);
        _backgroundTextStarted = false;
    }

    private Creature[] LivingPlayers() =>
        Creature.CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .OrderBy(static player => player.Player?.NetId ?? 0UL)
            .ToArray() ?? [];

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine machine = GenerateMoveStateMachine();
        foreach (MoveState move in machine.States.Values.OfType<MoveState>())
        {
            foreach (AbstractIntent intent in move.Intents)
            {
                yield return intent;
            }
        }
    }

    private static int ExpressionFromMoveId(string moveId) => moveId switch
    {
        JoyousFaceMoveId => 1,
        SmilingFaceMoveId => 2,
        RestingFaceMoveId => 3,
        SadFaceMoveId => 4,
        AngryFaceMoveId => 5,
        _ => 0
    };

    private static LibraryCreatureResistanceData.Resistance
        UniformResistance(LibraryResistanceLevel level) => new()
        {
            Slash = level,
            Pierce = level,
            Blunt = level
        };
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;
using FileAccess = Godot.FileAccess;

namespace LibraryOfRuinaVerification;

internal static class LiteratureFloorLiberationPhaseFourVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-literature-floor-phase4";
    private const string LogPrefix =
        "[LibraryOfRuina.LiteratureFloor.Phase4.Verify] ";
    private static string LocalProjectRoot => VerificationPaths.ProjectRoot;

    private static readonly string[] NormalMoveIds =
    [
        LiteratureFloorTodaysExpressionBoss.JoyousFaceMoveId,
        LiteratureFloorTodaysExpressionBoss.SmilingFaceMoveId,
        LiteratureFloorTodaysExpressionBoss.RestingFaceMoveId,
        LiteratureFloorTodaysExpressionBoss.SadFaceMoveId,
        LiteratureFloorTodaysExpressionBoss.AngryFaceMoveId
    ];

    private static bool _started;

    private sealed record PhaseFourContext(
        CombatState State,
        LiteratureFloorLiberationEncounter Encounter,
        LiteratureFloorTodaysExpressionBoss Boss,
        Creature BossCreature,
        Creature Player);

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyArg,
                StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            VerifyStaticContract();
            VerifyResourceAndLocalizationContract();
            await VerifyPhaseFourRuntime();
            Log.Info(LogPrefix + "LITERATURE_FLOOR_PHASE4_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(
                LogPrefix + "LITERATURE_FLOOR_PHASE4_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyStaticContract()
    {
        MonsterVisualCatalog.Validate();
        Require(MonsterVisualCatalog.Contains(
                    "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS"),
            "Today's Expression visual was omitted from the catalog baseline.");
        Require(LiteratureFloorLiberationEncounter.ImplementedMaxPhase == 5,
            "Literature floor implemented max phase is not five.");
        Require(LiteratureFloorTodaysExpressionBoss.DebugHpRange(false)
                    == (180, 188)
                && LiteratureFloorTodaysExpressionBoss
                    .DebugHpRange(true) == (192, 200)
                && LiteratureFloorTodaysExpressionBoss
                    .DebugJoyousFaceEndurance(false) == 1
                && LiteratureFloorTodaysExpressionBoss
                    .DebugJoyousFaceEndurance(true) == 2
                && LiteratureFloorTodaysExpressionBoss
                    .DebugRestingFaceDamage(false) == 16
                && LiteratureFloorTodaysExpressionBoss
                    .DebugRestingFaceDamage(true) == 19
                && LiteratureFloorTodaysExpressionBoss
                    .DebugSadFaceDamage(false) == 21
                && LiteratureFloorTodaysExpressionBoss
                    .DebugSadFaceDamage(true) == 24
                && LiteratureFloorTodaysExpressionBoss
                    .DebugAngryFaceDamage(false) == 9
                && LiteratureFloorTodaysExpressionBoss
                    .DebugAngryFaceDamage(true) == 11
                && LiteratureFloorTodaysExpressionBoss
                    .DebugWaveringFeelingsDamage(false) == 23
                && LiteratureFloorTodaysExpressionBoss
                    .DebugWaveringFeelingsDamage(true) == 28,
            "Today's Expression normal/ascended values changed.");

        LiteratureFloorTodaysExpressionBoss canonical =
            ModelDb.Monster<LiteratureFloorTodaysExpressionBoss>();
        Require(canonical.DefaultChaoResistance == 100,
            "Today's Expression Chao maximum changed.");
        RequireResistance(
            canonical.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Normal,
            "Today's Expression physical");
        RequireResistance(
            canonical.DefaultChaoResistanceData,
            LibraryResistanceLevel.Endure,
            "Today's Expression Chao");

        MonsterMoveStateMachine moves = GenerateStateMachine(
            canonical.ToMutable());
        RequireIntentTypes(
            moves,
            LiteratureFloorTodaysExpressionBoss.JoyousFaceMoveId,
            typeof(CombinedDefendBuffIntent));
        RequireIntentTypes(
            moves,
            LiteratureFloorTodaysExpressionBoss.SmilingFaceMoveId,
            typeof(CombinedDefendDebuffIntent));
        RequireIntentTypes(
            moves,
            LiteratureFloorTodaysExpressionBoss.RestingFaceMoveId,
            typeof(CombinedAttackDefendIntent));
        RequireIntentTypes(
            moves,
            LiteratureFloorTodaysExpressionBoss.SadFaceMoveId,
            typeof(CombinedAttackDebuffIntent));
        RequireIntentTypes(
            moves,
            LiteratureFloorTodaysExpressionBoss.AngryFaceMoveId,
            typeof(MultiAttackIntent));
        RequireIntentTypes(
            moves,
            LiteratureFloorTodaysExpressionBoss.WaveringFeelingsMoveId,
            typeof(CombinedAttackDebuffIntent));
        Require(
            ((MoveState)moves.States[
                LiteratureFloorTodaysExpressionBoss.WaveringFeelingsMoveId])
            .MustPerformOnceBeforeTransitioning,
            "Wavering Feelings can be overwritten by the normal pre-turn move roll.");
        RequireIntentTypes(
            moves,
            LiteratureFloorTodaysExpressionBoss.ReviveAndEmpowerMoveId,
            typeof(HealIntent),
            typeof(BuffIntent));
        foreach (string moveId in NormalMoveIds)
        {
            MoveState move = (MoveState)moves.States[moveId];
            Require(move.FollowUpState?.Id == moveId,
                moveId + " no longer persists until the expression changes.");
        }

        var joyous = (CombinedDefendBuffIntent)((MoveState)moves.States[
            LiteratureFloorTodaysExpressionBoss.JoyousFaceMoveId])
            .Intents.Single();
        var smiling = (CombinedDefendDebuffIntent)((MoveState)moves.States[
            LiteratureFloorTodaysExpressionBoss.SmilingFaceMoveId])
            .Intents.Single();
        var resting = (CombinedAttackDefendIntent)((MoveState)moves.States[
            LiteratureFloorTodaysExpressionBoss.RestingFaceMoveId])
            .Intents.Single();
        var sad = (CombinedAttackDebuffIntent)((MoveState)moves.States[
            LiteratureFloorTodaysExpressionBoss.SadFaceMoveId])
            .Intents.Single();
        var wavering = (CombinedAttackDebuffIntent)((MoveState)moves.States[
            LiteratureFloorTodaysExpressionBoss.WaveringFeelingsMoveId])
            .Intents.Single();
        Require(joyous.BlockAmount
                    == LiteratureFloorTodaysExpressionBoss.JoyousFaceBlock
                && joyous.Effects.Single().Amount == 1
                && smiling.BlockAmount
                    == LiteratureFloorTodaysExpressionBoss.SmilingFaceBlock
                && smiling.Effects.Single().Amount
                    == LiteratureFloorTodaysExpressionBoss.SmilingFaceWeak
                && resting.BlockAmount
                    == LiteratureFloorTodaysExpressionBoss.RestingFaceBlock
                && sad.Effects.Single().Amount
                    == LiteratureFloorTodaysExpressionBoss
                        .SadFaceVulnerable
                && wavering.Effects.Single().Amount
                    == LiteratureFloorTodaysExpressionBoss
                        .WaveringFeelingsConfusion,
            "Today's Expression compound intent badges changed.");

        Require(
            LiteratureFloorExpressionPassivePower
                .ResolveCardsRequiredForPlayerCount(1) == 6
            && LiteratureFloorExpressionPassivePower
                .ResolveCardsRequiredForPlayerCount(2) == 9
            && LiteratureFloorExpressionPassivePower
                .ResolveCardsRequiredForPlayerCount(3) == 12
            && LiteratureFloorExpressionPassivePower
                .ResolveCardsRequiredForPlayerCount(4) == 15,
            "Expression multiplayer card thresholds changed.");
        RequireGreenCounterPassive<
            LiteratureFloorExpressionPassivePower>();
        RequireGreenCounterPassive<
            LiteratureFloorWaveringFeelingsPassivePower>();
        Require(LiteratureFloorWaveringFeelingsPassivePower.Interval == 4,
            "Wavering Feelings cadence changed from every fourth turn.");
        Require(EncounterBgmController
                    .ResolveLiberationPhaseTrackIndex(4) == 1,
            "Literature floor phase-four BGM route changed.");
        Require(ModelDb.Encounter<LiteratureFloorLiberationEncounter>()
                .AllPossibleMonsters.Any(static monster =>
                    monster
                        is LiteratureFloorTodaysExpressionBoss),
            "Today's Expression was omitted from AllPossibleMonsters.");
    }

    private static void VerifyResourceAndLocalizationContract()
    {
        string animationPath =
            "res://scenes/creature_visuals/literature_floor_todays_expression_boss_animations.tres";
        string[] resources =
        [
            LiteratureFloorLiberationEncounter
                .TodaysExpressionEncounterScenePath,
            LiteratureFloorTodaysExpressionCreatureVisuals.ScenePath,
            animationPath,
            LiteratureFloorTodaysExpressionBoss.IdleTexturePath,
            LiteratureFloorTodaysExpressionBoss.HitTexturePath,
            LiteratureFloorTodaysExpressionBoss.ThrustTexturePath,
            LiteratureFloorTodaysExpressionBoss.GuardTexturePath,
            LiteratureFloorTodaysExpressionBoss.S1TexturePath,
            LiteratureFloorTodaysExpressionBoss.S2TexturePath,
            LiteratureFloorTodaysExpressionBoss.SmileSfxPath,
            LiteratureFloorTodaysExpressionBoss.AngrySfxPath,
            LiteratureFloorTodaysExpressionBoss.AttackSfxPath,
            LiteratureFloorTodaysExpressionBoss.StrongAttackSfxPath,
            LiteratureFloorTodaysExpressionBoss.StrongGuardSfxPath,
            LiteratureFloorLiberationBackgroundController
                .PhaseFourTexturePath
        ];
        resources = resources
            .Concat(
                LiteratureFloorTodaysExpressionCreatureVisuals
                    .FaceTexturePaths)
            .ToArray();
        foreach (string resource in resources)
        {
            Require(ResourceLoader.Exists(resource),
                "Missing phase-four resource: " + resource);
        }

        VerifyScene(
            LiteratureFloorLiberationEncounter
                .TodaysExpressionEncounterScenePath,
            [LiteratureFloorLiberationEncounter.TodaysExpressionSlot]);
        VerifyScene(
            LiteratureFloorTodaysExpressionCreatureVisuals.ScenePath,
            [
                "MotionRoot/Visuals",
                "MotionRoot/AttackVisuals",
                "AnimationPlayer",
                "Bounds",
                "CenterPos",
                "IntentPos",
                "TalkPos"
            ]);
        VerifyExpressionFaceMaterial();
        VerifyAnimationLibrary(
            animationPath,
            new Dictionary<string, double>
            {
                ["Idle"] = 1d,
                ["Guard"] =
                    LiteratureFloorTodaysExpressionAnimationContract
                        .GuardDurationSeconds,
                ["Attack"] =
                    LiteratureFloorTodaysExpressionAnimationContract
                        .AttackDurationSeconds,
                ["Angry"] =
                    LiteratureFloorTodaysExpressionAnimationContract
                        .AngryDurationSeconds,
                ["WaveringFeelings"] =
                    LiteratureFloorTodaysExpressionAnimationContract
                        .WaveringFeelingsDurationSeconds,
                ["Cast"] =
                    LiteratureFloorTodaysExpressionAnimationContract
                        .CastDurationSeconds,
                ["Hit"] =
                    LiteratureFloorTodaysExpressionAnimationContract
                        .HitDurationSeconds
            });
        VerifyLocalizationKeys();
    }

    private static async Task VerifyPhaseFourRuntime()
    {
        PhaseFourContext fight = await StartPhaseFourFight();
        fight.State.CurrentSide = CombatSide.Enemy;

        Require(fight.Encounter.CurrentPhase == 4
                && fight.Encounter.KilledBossCount == 3
                && !fight.Encounter.PhaseComplete
                && !fight.Encounter.TransitionPending
                && fight.BossCreature.SlotName
                    == LiteratureFloorLiberationEncounter
                        .TodaysExpressionSlot,
            "Direct phase-four combat state was incorrect.");
        Require(fight.BossCreature.MaxHp is >= 180 and <= 188
                && ((LibraryCreature)fight.BossCreature)
                    .MaxChaoValue == 100,
            "Phase-four runtime HP/Chao values were incorrect.");
        Require(fight.BossCreature.HasPower<HistoryFloorCorrosionPower>()
                && fight.BossCreature.HasPower<
                    LiteratureFloorExpressionPassivePower>()
                && fight.BossCreature.HasPower<
                    LiteratureFloorWaveringFeelingsPassivePower>(),
            "Phase-four opening passives were incomplete.");
        Require(NormalMoveIds.Contains(
                    fight.Boss.NextMove.StateId,
                    StringComparer.Ordinal),
            "Phase four did not start with a normal expression intent.");
        Require(
            LiteratureFloorLiberationBackgroundController
                .GetCurrentBackgroundImage()?.Texture?.ResourcePath
            == LiteratureFloorLiberationBackgroundController
                .PhaseFourTexturePath,
            "Phase-four background was not Today's Shy Look.");

        LiteratureFloorExpressionPassivePower expressionPower =
            fight.BossCreature.GetPower<
                LiteratureFloorExpressionPassivePower>()
            ?? throw new InvalidOperationException(
                "Expression passive disappeared.");
        IEnumerable<CardModel> playerCards = fight.Player.Player
            ?.PlayerCombatState?.AllCards
            ?? throw new InvalidOperationException(
                "Verifier player cards were unavailable.");
        CardModel attackCard = playerCards.First(
            static card => card.Type == CardType.Attack);

        string initialMove = fight.Boss.NextMove.StateId;
        await expressionPower.AfterDamageReceived(
            new ThrowingPlayerChoiceContext(),
            fight.BossCreature,
            new DamageResult(fight.BossCreature, ValueProp.Move)
            {
                BlockedDamage = 10,
                UnblockedDamage = 0,
                WasFullyBlocked = true
            },
            ValueProp.Move,
            fight.Player,
            attackCard);
        Require(fight.Boss.NextMove.StateId == initialMove,
            "Fully blocked attack damage changed the expression.");

        await expressionPower.AfterDamageReceived(
            new ThrowingPlayerChoiceContext(),
            fight.BossCreature,
            new DamageResult(fight.BossCreature, ValueProp.Unpowered)
            {
                UnblockedDamage = 1
            },
            ValueProp.Unpowered,
            fight.Player,
            null);
        Require(fight.Boss.NextMove.StateId == initialMove,
            "Unpowered damage changed the expression.");

        for (int hit = 0; hit < 4; hit++)
        {
            string before = fight.Boss.NextMove.StateId;
            await expressionPower.AfterDamageReceived(
                new ThrowingPlayerChoiceContext(),
                fight.BossCreature,
                new DamageResult(fight.BossCreature, ValueProp.Move)
                {
                    UnblockedDamage = 1
                },
                ValueProp.Move,
                fight.Player,
                attackCard);
            Require(fight.Boss.NextMove.StateId != before
                    && NormalMoveIds.Contains(
                        fight.Boss.NextMove.StateId,
                        StringComparer.Ordinal),
                "An unblocked attack hit did not select a different normal expression.");
        }

        await VerifyNormalMoves(fight);
        await VerifyCardThreshold(fight, expressionPower, attackCard);
        await VerifyWaveringFeelings(fight, expressionPower, attackCard);

        await CreatureCmd.Kill(fight.BossCreature, force: true);
        Require(fight.Encounter.CurrentPhase == 5
                && fight.Encounter.KilledBossCount == 4
                && !fight.Encounter.PhaseComplete
                && fight.Encounter.TransitionPending
                && !fight.Encounter.IsFullyLiberated,
            "Today's Expression death did not enter the phase-five transition.");

        CleanupRun();
        await WaitForRunCleanup("phase-four verifier cleanup");
    }

    private static async Task VerifyNormalMoves(PhaseFourContext fight)
    {
        await RemovePower<LibraryEndurancePower>(fight.BossCreature);
        await ClearBlock(fight.BossCreature);
        ForceMove(
            fight.Boss,
            LiteratureFloorTodaysExpressionBoss.JoyousFaceMoveId);
        await fight.Boss.PerformMove();
        LibraryEndurancePower endurance = fight.BossCreature
            .GetPower<LibraryEndurancePower>()
            ?? throw new InvalidOperationException(
                "Joyous Face did not apply Endurance.");
        Require(fight.BossCreature.Block
                    == LiteratureFloorTodaysExpressionBoss.JoyousFaceBlock
                && endurance.Amount == 1
                && endurance.TurnsRemaining <= 0,
            "Joyous Face block/permanent Endurance was incorrect.");
        RequireDifferentNormalIntentSelected(
            fight,
            LiteratureFloorTodaysExpressionBoss.JoyousFaceMoveId);

        await RemovePower<WeakPower>(fight.Player);
        await ClearBlock(fight.BossCreature);
        ForceMove(
            fight.Boss,
            LiteratureFloorTodaysExpressionBoss.SmilingFaceMoveId);
        await fight.Boss.PerformMove();
        Require(fight.BossCreature.Block
                    == LiteratureFloorTodaysExpressionBoss.SmilingFaceBlock
                && fight.Player.GetPower<WeakPower>()?.Amount
                    == LiteratureFloorTodaysExpressionBoss
                        .SmilingFaceWeak,
            "Smiling Face block/Weak was incorrect.");
        RequireDifferentNormalIntentSelected(
            fight,
            LiteratureFloorTodaysExpressionBoss.SmilingFaceMoveId);

        await RemovePower<WeakPower>(fight.Player);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        await ClearBlock(fight.BossCreature);
        int hpBefore = fight.Player.CurrentHp;
        ForceMove(
            fight.Boss,
            LiteratureFloorTodaysExpressionBoss.RestingFaceMoveId);
        await fight.Boss.PerformMove();
        Require(hpBefore - fight.Player.CurrentHp == 16
                && fight.BossCreature.Block
                    == LiteratureFloorTodaysExpressionBoss.RestingFaceBlock,
            "Resting Face damage/block was incorrect.");
        RequireDifferentNormalIntentSelected(
            fight,
            LiteratureFloorTodaysExpressionBoss.RestingFaceMoveId);

        await RemovePower<VulnerablePower>(fight.Player);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        hpBefore = fight.Player.CurrentHp;
        ForceMove(
            fight.Boss,
            LiteratureFloorTodaysExpressionBoss.SadFaceMoveId);
        await fight.Boss.PerformMove();
        Require(hpBefore - fight.Player.CurrentHp == 21
                && fight.Player.GetPower<VulnerablePower>()?.Amount
                    == LiteratureFloorTodaysExpressionBoss
                        .SadFaceVulnerable,
            "Sad Face damage/Vulnerable was incorrect.");
        RequireDifferentNormalIntentSelected(
            fight,
            LiteratureFloorTodaysExpressionBoss.SadFaceMoveId);

        await RemovePower<VulnerablePower>(fight.Player);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        hpBefore = fight.Player.CurrentHp;
        ForceMove(
            fight.Boss,
            LiteratureFloorTodaysExpressionBoss.AngryFaceMoveId);
        await fight.Boss.PerformMove();
        Require(hpBefore - fight.Player.CurrentHp == 27,
            "Angry Face did not deal three 9-damage hits.");
        RequireDifferentNormalIntentSelected(
            fight,
            LiteratureFloorTodaysExpressionBoss.AngryFaceMoveId);
    }

    private static void RequireDifferentNormalIntentSelected(
        PhaseFourContext fight,
        string performedMoveId)
    {
        Require(NormalMoveIds.Contains(
                    fight.Boss.NextMove.StateId,
                    StringComparer.Ordinal)
                && !string.Equals(
                    fight.Boss.NextMove.StateId,
                    performedMoveId,
                    StringComparison.Ordinal),
            "A normal expression repeated immediately.");
    }

    private static async Task VerifyCardThreshold(
        PhaseFourContext fight,
        LiteratureFloorExpressionPassivePower expressionPower,
        CardModel attackCard)
    {
        await RemovePower<StrengthPower>(fight.BossCreature);
        CardPlay play = CreateCardPlay(attackCard);
        for (int card = 1; card <= 5; card++)
        {
            await expressionPower.AfterCardPlayed(
                new ThrowingPlayerChoiceContext(),
                play);
            Require(fight.BossCreature.GetPower<StrengthPower>() == null
                    && expressionPower.Amount == 6 - card,
                "Expression card countdown triggered early.");
        }

        await expressionPower.AfterCardPlayed(
            new ThrowingPlayerChoiceContext(),
            play);
        Require(fight.BossCreature.GetPower<StrengthPower>()?.Amount
                    == LiteratureFloorExpressionPassivePower.StrengthGain
                && expressionPower.Amount == 6,
            "Expression card countdown did not grant three Strength and reset.");
        await RemovePower<StrengthPower>(fight.BossCreature);
    }

    private static async Task VerifyWaveringFeelings(
        PhaseFourContext fight,
        LiteratureFloorExpressionPassivePower expressionPower,
        CardModel attackCard)
    {
        LiteratureFloorWaveringFeelingsPassivePower cadence =
            fight.BossCreature.GetPower<
                LiteratureFloorWaveringFeelingsPassivePower>()
            ?? throw new InvalidOperationException(
                "Wavering Feelings passive disappeared.");
        cadence.SetAmount(
            LiteratureFloorWaveringFeelingsPassivePower.Interval,
            silent: true);
        ForceMove(
            fight.Boss,
            LiteratureFloorTodaysExpressionBoss.RestingFaceMoveId);
        for (int turn = 1;
             turn < LiteratureFloorWaveringFeelingsPassivePower.Interval;
             turn++)
        {
            await cadence.BeforeSideTurnStart(
                new ThrowingPlayerChoiceContext(),
                CombatSide.Player,
                fight.State.PlayerCreatures,
                fight.State);
            Require(!fight.Boss.IsWaveringFeelingsQueued
                    && cadence.Amount
                        == LiteratureFloorWaveringFeelingsPassivePower.Interval
                        - turn,
                "Wavering Feelings queued before the fourth turn.");
        }

        await cadence.BeforeSideTurnStart(
            new ThrowingPlayerChoiceContext(),
            CombatSide.Player,
            fight.State.PlayerCreatures,
            fight.State);
        Require(fight.Boss.IsWaveringFeelingsQueued
                && cadence.Amount == 1,
            "Wavering Feelings was not queued on the fourth turn.");

        fight.Boss.RollMove(fight.State.PlayerCreatures);
        Require(fight.Boss.IsWaveringFeelingsQueued,
            "The normal pre-turn move roll overwrote Wavering Feelings.");

        await expressionPower.AfterDamageReceived(
            new ThrowingPlayerChoiceContext(),
            fight.BossCreature,
            new DamageResult(fight.BossCreature, ValueProp.Move)
            {
                UnblockedDamage = 1
            },
            ValueProp.Move,
            fight.Player,
            attackCard);
        Require(fight.Boss.IsWaveringFeelingsQueued,
            "An unblocked attack overwrote the queued special intent.");

        await RemovePower<VulnerablePower>(fight.Player);
        await RemovePower<LibraryOfRuinaConfusionPower>(fight.Player);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        int hpBefore = fight.Player.CurrentHp;
        await fight.Boss.PerformMove();
        Require(hpBefore - fight.Player.CurrentHp == 23
                && fight.Player.GetPower<
                    LibraryOfRuinaConfusionPower>()?.Amount
                    == LiteratureFloorTodaysExpressionBoss
                        .WaveringFeelingsConfusion
                && cadence.Amount
                    == LiteratureFloorWaveringFeelingsPassivePower
                        .Interval
                && NormalMoveIds.Contains(
                    fight.Boss.NextMove.StateId,
                    StringComparer.Ordinal),
            "Wavering Feelings damage, Confusion, reset, or follow-up was incorrect.");
    }

    private static async Task<PhaseFourContext> StartPhaseFourFight()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "LITERATUREFLOORVERIFY_PHASE4",
            GameMode.Standard,
            ascensionLevel: 0);
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);

        var encounter = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "4",
            ["KilledBossCount"] = "3",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False"
        });
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress,
            "Literature floor phase-four combat start");
        await WaitFrames(8);

        CombatState state = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var activeEncounter = state.Encounter
                as LiteratureFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "Literature floor encounter was not active.");
        LiteratureFloorTodaysExpressionBoss boss = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorTodaysExpressionBoss>()
            .Single();
        return new PhaseFourContext(
            state,
            activeEncounter,
            boss,
            boss.Creature,
            state.PlayerCreatures.Single());
    }

    private static void ForceMove(MonsterModel monster, string moveId)
    {
        MoveState move = monster.MoveStateMachine?.States[moveId]
            as MoveState
            ?? throw new InvalidOperationException(
                "Missing move state " + moveId + ".");
        monster.SetMoveImmediate(move, forceTransition: true);
    }

    private static async Task SetPlayerToFullHpAndClearBlock(
        Creature player)
    {
        await CreatureCmd.SetCurrentHp(player, player.MaxHp);
        await ClearBlock(player);
    }

    private static Task ClearBlock(Creature creature) =>
        creature.Block > 0
            ? CreatureCmd.LoseBlock(
                new ThrowingPlayerChoiceContext(),
                creature,
                creature.Block,
                null)
            : Task.CompletedTask;

    private static Task RemovePower<T>(Creature creature)
        where T : PowerModel =>
        creature.GetPower<T>() is { } power
            ? PowerCmd.Remove(power)
            : Task.CompletedTask;

    private static CardPlay CreateCardPlay(CardModel card) => new()
    {
        Card = card,
        Player = card.Owner,
        Target = null,
        ResultPile = PileType.Discard,
        Resources = new ResourceInfo
        {
            EnergySpent = 0,
            EnergyValue = 0,
            StarsSpent = 0,
            StarValue = 0
        },
        IsAutoPlay = false,
        PlayIndex = 0,
        PlayCount = 1
    };

    private static MonsterMoveStateMachine GenerateStateMachine(
        MonsterModel monster)
    {
        MethodInfo method = monster.GetType().GetMethod(
                "GenerateMoveStateMachine",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "GenerateMoveStateMachine missing for "
                + monster.GetType().Name + ".");
        return method.Invoke(monster, null) as MonsterMoveStateMachine
            ?? throw new InvalidOperationException(
                "GenerateMoveStateMachine returned null for "
                + monster.GetType().Name + ".");
    }

    private static void RequireIntentTypes(
        MonsterMoveStateMachine machine,
        string moveId,
        params Type[] expectedTypes)
    {
        Require(machine.States.TryGetValue(moveId, out MonsterState? state)
                && state is MoveState,
            "Missing move state " + moveId + ".");
        Type[] actual = ((MoveState)state!).Intents
            .Select(static intent => intent.GetType())
            .ToArray();
        Require(actual.SequenceEqual(expectedTypes),
            moveId + " intent types changed: "
            + string.Join(",", actual.Select(static type => type.Name)));
    }

    private static void RequireGreenCounterPassive<T>()
        where T : LiteratureFloorGreenPassivePower
    {
        T power = ModelDb.Power<T>();
        Require(power.Type == PowerType.None
                && power.StackType == PowerStackType.Counter
                && power.PackedIconPath.EndsWith(
                    "library_passive_green.png",
                    StringComparison.Ordinal),
            typeof(T).Name + " lost its green counter-passive contract.");
    }

    private static void VerifyScene(
        string path,
        IEnumerable<string> requiredNodes)
    {
        string text = FileAccess.GetFileAsString(path);
        Require(!text.Contains(
                "ext_resource type=\"Script\"",
                StringComparison.Ordinal),
            path + " contains an attached script.");
        PackedScene scene = ResourceLoader.Load<PackedScene>(path)
            ?? throw new InvalidOperationException(
                "Unable to load scene " + path + ".");
        Node root = scene.Instantiate();
        try
        {
            foreach (string node in requiredNodes)
            {
                Require(root.GetNodeOrNull(node) != null,
                    path + " is missing " + node + ".");
            }
        }
        finally
        {
            root.Free();
        }
    }

    private static void VerifyAnimationLibrary(
        string path,
        IReadOnlyDictionary<string, double> expectedLengths)
    {
        AnimationLibrary library =
            ResourceLoader.Load<AnimationLibrary>(path)
            ?? throw new InvalidOperationException(
                "Unable to load animation library " + path + ".");
        HashSet<string> names = library.GetAnimationList()
            .Select(static name => name.ToString())
            .ToHashSet(StringComparer.Ordinal);
        Require(names.SetEquals(expectedLengths.Keys),
            path + " animation names changed: "
            + string.Join(",", names));
        foreach ((string name, double expected) in expectedLengths)
        {
            Animation animation = library.GetAnimation(name)
                ?? throw new InvalidOperationException(
                    path + " is missing animation " + name + ".");
            Require(Math.Abs(animation.Length - expected) < 0.001d,
                path + " animation " + name + " length changed.");
        }
    }

    private static void VerifyExpressionFaceMaterial()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>(
                LiteratureFloorTodaysExpressionCreatureVisuals.ScenePath)
            ?? throw new InvalidOperationException(
                "Unable to load Today's Expression visual scene.");
        Node root = scene.Instantiate();
        try
        {
            Sprite2D? face = root.GetNodeOrNull<Sprite2D>(
                "ExpressionFace");
            if (face == null)
            {
                return;
            }

            Require(face.Material is CanvasItemMaterial
                    {
                        BlendMode: CanvasItemMaterial.BlendModeEnum.Add
                    },
                "Expression face textures require additive blending.");
        }
        finally
        {
            root.Free();
        }
    }

    private static void VerifyLocalizationKeys()
    {
        string[] monsterKeys =
        [
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.name",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.moves.JOYOUS_FACE.title",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.moves.SMILING_FACE.title",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.moves.RESTING_FACE.title",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.moves.SAD_FACE.title",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.moves.ANGRY_FACE.title",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.moves.WAVERING_FEELINGS.title",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.moves.REVIVE_AND_EMPOWER.title"
        ];
        monsterKeys = monsterKeys.Concat(
            Enumerable.Range(0, 8).Select(index =>
                "LITERATURE_FLOOR_TODAYS_EXPRESSION_BOSS.backgroundText.normal."
                + index)).ToArray();
        string[] powerKeys =
        [
            "LITERATURE_FLOOR_EXPRESSION_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_EXPRESSION_PASSIVE_POWER.description",
            "LITERATURE_FLOOR_EXPRESSION_PASSIVE_POWER.smartDescription",
            "LITERATURE_FLOOR_WAVERING_FEELINGS_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_WAVERING_FEELINGS_PASSIVE_POWER.description",
            "LITERATURE_FLOOR_WAVERING_FEELINGS_PASSIVE_POWER.smartDescription"
        ];
        string[] intentKeys =
        [
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_JOYOUS_FACE.description",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_SMILING_FACE.description",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_RESTING_FACE.description",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_SAD_FACE.description",
            "LITERATURE_FLOOR_TODAYS_EXPRESSION_WAVERING_FEELINGS.description"
        ];

        foreach (string language in new[] { "zhs", "eng", "jpn", "kor" })
        {
            VerifyJsonKeys(language, "monsters.json", monsterKeys);
            VerifyJsonKeys(language, "powers.json", powerKeys);
            VerifyJsonKeys(language, "intents.json", intentKeys);
        }
    }

    private static void VerifyJsonKeys(
        string language,
        string table,
        IEnumerable<string> requiredKeys)
    {
        string path = Path.Combine(
            LocalProjectRoot,
            "LibraryOfRuina",
            "localization",
            language,
            table);
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(path));
        JsonElement root = document.RootElement;
        foreach (string key in requiredKeys)
        {
            Require(root.TryGetProperty(key, out _),
                language + "/" + table + " is missing " + key + ".");
        }
    }

    private static void RequireResistance(
        LibraryCreatureResistanceData.Resistance? resistance,
        LibraryResistanceLevel expected,
        string label)
    {
        Require(resistance != null
                && resistance.Slash == expected
                && resistance.Pierce == expected
                && resistance.Blunt == expected,
            label + " resistance mismatch.");
    }

    private static void ArmActLikeIt2OneShotVanillaEntry()
    {
        Type? gateType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(
                "ActLikeIt2.Runtime.ActSelectionGate",
                throwOnError: false))
            .FirstOrDefault(static type => type != null);
        PropertyInfo? skipNextFork = gateType?.GetProperty(
            "SkipNextFork",
            BindingFlags.Static | BindingFlags.Public
            | BindingFlags.NonPublic);
        if (skipNextFork?.CanWrite == true)
        {
            skipNextFork.SetValue(null, true);
        }
    }

    private static void CleanupRun()
    {
        RunManager.Instance.CleanUp(graceful: true);
    }

    private static Task WaitForRunCleanup(string description) =>
        WaitUntil(
            static () => !CombatManager.Instance.IsInProgress
                && CombatManager.Instance.DebugOnlyGetState() == null
                && RunManager.Instance.DebugOnlyGetState() == null,
            description);

    private static async Task WaitUntil(
        Func<bool> predicate,
        string description,
        int maxFrames = 900)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (predicate())
            {
                return;
            }

            await WaitFrame();
        }

        throw new TimeoutException(
            "Timed out waiting for " + description + ".");
    }

    private static async Task WaitFrames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            await WaitFrame();
        }
    }

    private static async Task WaitFrame()
    {
        SceneTree tree = NGame.Instance?.GetTree()
            ?? (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

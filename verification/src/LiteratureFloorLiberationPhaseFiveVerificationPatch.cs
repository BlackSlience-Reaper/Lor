using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
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

internal static class LiteratureFloorLiberationPhaseFiveVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-literature-floor-phase5";
    private const string LogPrefix =
        "[LibraryOfRuina.LiteratureFloor.Phase5.Verify] ";
    private static string LocalProjectRoot => VerificationPaths.ProjectRoot;

    private static readonly string[] BossNormalMoveIds =
    [
        LiteratureFloorBlackSwanBoss.StruggleMoveId,
        LiteratureFloorBlackSwanBoss.VomitMoveId,
        LiteratureFloorBlackSwanBoss.CurlUpMoveId,
        LiteratureFloorBlackSwanBoss.OldUmbrellaMoveId,
        LiteratureFloorBlackSwanBoss.VileRealityMoveId
    ];

    private static bool _started;

    private sealed record PhaseFiveContext(
        CombatState State,
        LiteratureFloorLiberationEncounter Encounter,
        LiteratureFloorBlackSwanBoss Boss,
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
            await VerifyRuntimeContract();
            Log.Info(LogPrefix + "LITERATURE_FLOOR_PHASE5_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(
                LogPrefix + "LITERATURE_FLOOR_PHASE5_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyStaticContract()
    {
        MonsterVisualCatalog.Validate();
        string[] visualIds =
        [
            "LITERATURE_FLOOR_BLACK_SWAN_BOSS",
            "LITERATURE_FLOOR_BLACK_SWAN_FIRST_BROTHER",
            "LITERATURE_FLOOR_BLACK_SWAN_SECOND_BROTHER",
            "LITERATURE_FLOOR_BLACK_SWAN_THIRD_BROTHER",
            "LITERATURE_FLOOR_BLACK_SWAN_FOURTH_BROTHER",
            "LITERATURE_FLOOR_BLACK_SWAN_FIFTH_BROTHER",
            "LITERATURE_FLOOR_BLACK_SWAN_SIXTH_BROTHER"
        ];
        Require(
            visualIds.All(MonsterVisualCatalog.Contains),
            "Black Swan visual registrations are incomplete.");
        Require(
            LiteratureFloorLiberationEncounter.ImplementedMaxPhase == 5,
            "Literature floor implemented max phase is not five.");
        Require(
            LiteratureFloorBlackSwanBoss.DebugHpRange(false)
                == (210, 220)
            && LiteratureFloorBlackSwanBoss.DebugHpRange(true)
                == (250, 260)
            && LiteratureFloorBlackSwanBrotherBase.DebugHpRange(false)
                == (60, 64)
            && LiteratureFloorBlackSwanBrotherBase.DebugHpRange(true)
                == (66, 70),
            "Black Swan phase HP ranges changed.");
        Require(
            LiteratureFloorBlackSwanBoss.DebugStruggleDamage(false) == 3
            && LiteratureFloorBlackSwanBoss.DebugStruggleDamage(true) == 4
            && LiteratureFloorBlackSwanBoss.DebugVomitDamage(false) == 2
            && LiteratureFloorBlackSwanBoss.DebugVomitDamage(true) == 3
            && LiteratureFloorBlackSwanBoss.DebugCurlUpDamage(false) == 9
            && LiteratureFloorBlackSwanBoss.DebugCurlUpDamage(true) == 10
            && LiteratureFloorBlackSwanBoss.DebugSwanSongDamage(false) == 18
            && LiteratureFloorBlackSwanBoss.DebugSwanSongDamage(true) == 21
            && LiteratureFloorBlackSwanBrotherBase
                .DebugGreenFilthDamage(false) == 5
            && LiteratureFloorBlackSwanBrotherBase
                .DebugGreenFilthDamage(true) == 6
            && LiteratureFloorBlackSwanBrotherBase
                .DebugBluffDamage(false) == 8
            && LiteratureFloorBlackSwanBrotherBase
                .DebugBluffDamage(true) == 10,
            "Black Swan phase ascension damage values changed.");

        LiteratureFloorBlackSwanBoss boss = ModelDb
            .Monster<LiteratureFloorBlackSwanBoss>();
        LiteratureFloorBlackSwanFirstBrother brother = ModelDb
            .Monster<LiteratureFloorBlackSwanFirstBrother>();
        Require(
            boss.DefaultChaoResistance == 260
            && brother.DefaultChaoResistance == 50,
            "Black Swan phase stagger-resistance maxima changed.");
        RequireUniformNormal(
            boss.DefaultPhysicalResistanceData,
            "Black Swan physical");
        RequireUniformNormal(
            boss.DefaultChaoResistanceData,
            "Black Swan stagger");
        RequireUniformNormal(
            brother.DefaultPhysicalResistanceData,
            "Brother physical");
        RequireUniformNormal(
            brother.DefaultChaoResistanceData,
            "Brother stagger");

        MonsterMoveStateMachine bossMoves = GenerateStateMachine(boss);
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBlackSwanBoss.StruggleMoveId,
            typeof(CombinedAttackDebuffIntent));
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBlackSwanBoss.VomitMoveId,
            typeof(MultiAttackIntent));
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBlackSwanBoss.CurlUpMoveId,
            typeof(CombinedAttackDefendIntent));
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBlackSwanBoss.OldUmbrellaMoveId,
            typeof(CombinedDefendBuffIntent));
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBlackSwanBoss.VileRealityMoveId,
            typeof(DebuffIntent));
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorBlackSwanBoss.SwanSongMoveId,
            typeof(IndiscriminateAttackIntent));
        RequirePrimaryBadgeAmount(
            (IndiscriminateAttackIntent)((MoveState)bossMoves.States[
                LiteratureFloorBlackSwanBoss.SwanSongMoveId]).Intents[0],
            LiteratureFloorBlackSwanBoss.SwanSongConfusion,
            "Black Swan's group intent read the group marker instead of Confusion.");
        Require(
            ((MoveState)bossMoves.States[
                LiteratureFloorBlackSwanBoss.SwanSongMoveId])
                .MustPerformOnceBeforeTransitioning,
            "Swan Song can be overwritten by the normal pre-turn move roll.");
        foreach (string moveId in BossNormalMoveIds)
        {
            Require(
                ((MoveState)bossMoves.States[moveId]).FollowUpState?.Id
                    == "BLACK_SWAN_CYCLE_ROUTER",
                moveId + " no longer returns to the cycle router.");
        }
        Require(
            ((MoveState)bossMoves.States[
                LiteratureFloorBlackSwanBoss.SwanSongMoveId])
                .FollowUpState?.Id == "BLACK_SWAN_CYCLE_ROUTER",
            "Swan Song no longer returns to the cycle router.");
        Require(
            CombatStateProperties.IsTransient(typeof(LiteratureFloorBlackSwanBoss))
            && CombatStateProperties.All.Any(static entry =>
                entry.DeclaringType == typeof(LiteratureFloorBlackSwanBoss)),
            "Black Swan cycle state is a SavedProperty again, or missing from the reload list.");

        MonsterMoveStateMachine brotherMoves =
            GenerateStateMachine(brother);
        RequireIntentTypes(
            brotherMoves,
            LiteratureFloorBlackSwanBrotherBase.GreenFilthMoveId,
            typeof(CombinedAttackDebuffIntent));
        RequireIntentTypes(
            brotherMoves,
            LiteratureFloorBlackSwanBrotherBase.BluffMoveId,
            typeof(CombinedAttackBuffIntent));

        Require(
            ModelDb.Power<
                    LiteratureFloorBlackSwanVanishingFamilyPower>()
                .PackedIconPath
                == LiteratureFloorBlackSwanVanishingFamilyPower
                    .CustomIconPath,
            "Vanishing Family lost its supplied icon.");
    }

    private static void VerifyResourceAndLocalizationContract()
    {
        string[] resources =
        [
            LiteratureFloorLiberationEncounter.BlackSwanEncounterScenePath,
            LiteratureFloorBlackSwanCreatureVisuals.ScenePath,
            LiteratureFloorBlackSwanBrotherCreatureVisuals.ScenePath,
            LiteratureFloorLiberationBackgroundController
                .PhaseFiveTexturePath,
            LiteratureFloorBlackSwanVanishingFamilyPower.CustomIconPath,
            LiteratureFloorBlackSwanBoss.IdleTexturePath,
            LiteratureFloorBlackSwanBoss.SpecialTexturePath,
            LiteratureFloorBlackSwanBrotherBase.SixthFireTexturePath
        ];
        foreach (string resource in resources)
        {
            Require(
                ResourceLoader.Exists(resource),
                "Missing Black Swan resource " + resource + ".");
        }

        VerifyScene(
            LiteratureFloorBlackSwanCreatureVisuals.ScenePath,
            ["MotionRoot", "MotionRoot/Visuals", "MotionRoot/AttackVisuals",
                "AnimationPlayer", "Bounds", "CenterPos", "IntentPos"]);
        VerifyScene(
            LiteratureFloorBlackSwanBrotherCreatureVisuals.ScenePath,
            ["MotionRoot", "MotionRoot/Visuals", "MotionRoot/AttackVisuals",
                "AnimationPlayer", "Bounds", "CenterPos", "IntentPos"]);
        VerifyEncounterScene();

        VerifyAnimationLibrary(
            "res://scenes/creature_visuals/"
            + "literature_floor_black_swan_boss_animations.tres",
            new Dictionary<string, double>
            {
                ["Idle"] = 1d,
                ["SlashOne"] = LiteratureFloorBlackSwanAnimationContract
                    .SlashDurationSeconds,
                ["SlashTwo"] = LiteratureFloorBlackSwanAnimationContract
                    .SlashDurationSeconds,
                ["Pierce"] = LiteratureFloorBlackSwanAnimationContract
                    .PierceDurationSeconds,
                ["Guard"] = LiteratureFloorBlackSwanAnimationContract
                    .GuardDurationSeconds,
                ["Special"] = LiteratureFloorBlackSwanAnimationContract
                    .SpecialDurationSeconds,
                ["Hit"] = LiteratureFloorBlackSwanAnimationContract
                    .HitDurationSeconds
            });
        for (int brother = 1; brother <= 6; brother++)
        {
            VerifyAnimationLibrary(
                "res://scenes/creature_visuals/"
                + "literature_floor_black_swan_brother_"
                + brother
                + "_animations.tres",
                new Dictionary<string, double>
                {
                    ["Idle"] = 1d,
                    ["Attack"] =
                        LiteratureFloorBlackSwanBrotherAnimationContract
                            .AttackDurationSeconds,
                    ["AttackAlt"] =
                        LiteratureFloorBlackSwanBrotherAnimationContract
                            .AttackDurationSeconds,
                    ["Hit"] =
                        LiteratureFloorBlackSwanBrotherAnimationContract
                            .HitDurationSeconds
                });
        }

        VerifyLocalizationKeys();
    }

    private static async Task VerifyRuntimeContract()
    {
        PhaseFiveContext fight = await StartPhaseFiveFight();
        Require(
            fight.Encounter.CurrentPhase == 5
            && fight.Encounter.KilledBossCount == 4
            && !fight.Encounter.PhaseComplete
            && !fight.Encounter.TransitionPending,
            "Phase-five encounter state was incorrect at combat start.");
        Require(
            LivingBrothers(fight.State).Select(static brother =>
                    brother.BrotherNumber)
                .SequenceEqual([1, 2]),
            "Phase five did not start with the eldest and second Brothers.");
        Require(
            fight.BossCreature.MaxHp is >= 210 and <= 220
            && fight.BossCreature is LibraryCreature
            {
                CurrentChaoValue: 260,
                MaxChaoValue: 260
            },
            "Runtime Black Swan HP or stagger resistance was incorrect.");

        await fight.Boss.OnPlayerSideTurnStart(fight.State);
        Require(
            fight.Encounter.BlackSwanRoundStarts == 1
            && fight.Encounter.NextBlackSwanBrother == 3
            && LivingBrothers(fight.State).Length == 2,
            "First Black Swan round summoned a Brother.");
        Require(
            fight.BossCreature.GetPower<IntangiblePower>()?.Amount == 1
            && fight.BossCreature.GetPower<StrengthPower>()?.Amount == 1,
            "Eldest or second Brother did not apply round-one support.");

        await fight.Boss.OnPlayerSideTurnStart(fight.State);
        Require(
            fight.Encounter.BlackSwanRoundStarts == 2
            && fight.Encounter.NextBlackSwanBrother == 4
            && LivingBrothers(fight.State).Any(static brother =>
                brother.BrotherNumber == 3)
            && fight.BossCreature.GetPower<PlatingPower>()?.Amount == 10,
            "Second round did not summon Third Brother with Plating support.");

        await fight.Boss.OnPlayerSideTurnStart(fight.State);
        fight.BossCreature.PrepareForNextTurn(
            fight.State.PlayerCreatures);
        Require(
            fight.Encounter.NextBlackSwanBrother == 5
            && LivingBrothers(fight.State).Length == 4
            && !fight.Boss.IsSwanSongUnlocked
            && !fight.Boss.IsSwanSongQueued,
            "Swan Song entered the move pool before all Brothers died.");
        Require(
            LivingBrothers(fight.State)
                .Single(static brother => brother.BrotherNumber == 4)
                .Creature
                .HasPower<
                    LiteratureFloorBlackSwanFourthBrotherPassivePower>(),
            "Fourth Brother passive was missing.");
        LiteratureFloorBlackSwanFourthBrotherPassivePower fourthPower =
            LivingBrothers(fight.State)
                .Single(static brother => brother.BrotherNumber == 4)
                .Creature.GetPower<
                    LiteratureFloorBlackSwanFourthBrotherPassivePower>()
            ?? throw new InvalidOperationException(
                "Fourth Brother power could not be resolved.");
        Require(
            fourthPower.ModifyDamageMultiplicativeCompat(
                fight.Player,
                1m,
                ValueProp.Move,
                fight.BossCreature,
                null,
                null) == 1.5m,
            "Fourth Brother did not apply a 50% Black Swan attack multiplier.");

        await RemovePower<LibraryOfRuinaConfusionPower>(fight.Player);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        ForceMove(fight.Boss, LiteratureFloorBlackSwanBoss.SwanSongMoveId);
        decimal playerHp = fight.Player.CurrentHp;
        await fight.Boss.PerformMove();
        Require(
            fight.Player.CurrentHp < playerHp
            && fight.Player.GetPower<
                    LibraryOfRuinaConfusionPower>()?.Amount == 1
            && !fight.Boss.IsSwanSongQueued,
            "Swan Song damage, Confusion, or follow-up was incorrect.");

        await VerifyBossMoves(fight);
        await VerifyBrotherMoves(fight);
        await VerifyRemainingSummonsAndPassives(fight);

        await CreatureCmd.Kill(fight.BossCreature, force: true);
        Require(
            fight.Encounter.CurrentPhase == 6
            && fight.Encounter.KilledBossCount == 5
            && fight.Encounter.PhaseComplete
            && !fight.Encounter.TransitionPending
            && fight.Encounter.SettlementTriggered
            && !fight.Encounter.EndedByLethalDamage
            && LiteratureFloorLiberationSettlementStore.PendingSettlement
            && fight.Encounter.IsFullyLiberated,
            "Black Swan death did not complete Literature floor liberation.");
        LiteratureFloorLiberationSettlementStore.Consume();

        CleanupRun();
        await WaitForRunCleanup("phase-five verifier cleanup");
    }

    private static async Task VerifyBossMoves(PhaseFiveContext fight)
    {
        await RemovePower<LibraryWeakPower>(fight.Player);
        ForceMove(fight.Boss, LiteratureFloorBlackSwanBoss.StruggleMoveId);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        await fight.Boss.PerformMove();
        LibraryWeakPower? weak = fight.Player.GetPower<LibraryWeakPower>();
        Require(
            weak?.Amount == LiteratureFloorBlackSwanBoss.StruggleWeak
            && weak.TurnsRemaining
                == LiteratureFloorBlackSwanBoss.StruggleWeakTurns,
            "Struggle did not apply 2 Weak for 1 turn.");

        await ClearBlock(fight.BossCreature);
        ForceMove(fight.Boss, LiteratureFloorBlackSwanBoss.CurlUpMoveId);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        await fight.Boss.PerformMove();
        Require(
            fight.BossCreature.Block
                == LiteratureFloorBlackSwanBoss.CurlUpBlock,
            "Curl Up did not grant 12 Block.");

        await ClearBlock(fight.BossCreature);
        await RemovePower<ReflectPower>(fight.BossCreature);
        ForceMove(
            fight.Boss,
            LiteratureFloorBlackSwanBoss.OldUmbrellaMoveId);
        await fight.Boss.PerformMove();
        Require(
            fight.BossCreature.Block
                == LiteratureFloorBlackSwanBoss.OldUmbrellaBlock
            && fight.BossCreature.GetPower<ReflectPower>()?.Amount
                == LiteratureFloorBlackSwanBoss.OldUmbrellaReflect,
            "Old Umbrella did not grant 30 Block and 1 Reflect.");

        await RemovePower<LibraryBleedingPower>(fight.Player);
        await RemovePower<LibraryVulnerablePower>(fight.Player);
        ForceMove(fight.Boss, LiteratureFloorBlackSwanBoss.VileRealityMoveId);
        await fight.Boss.PerformMove();
        LibraryVulnerablePower? vulnerable =
            fight.Player.GetPower<LibraryVulnerablePower>();
        Require(
            fight.Player.GetPower<LibraryBleedingPower>()?.Amount
                == LiteratureFloorBlackSwanBoss.VileRealityBleed
            && vulnerable?.Amount
                == LiteratureFloorBlackSwanBoss.VileRealityVulnerable
            && vulnerable.TurnsRemaining
                == LiteratureFloorBlackSwanBoss
                    .VileRealityVulnerableTurns,
            "Vile Reality debuffs were incorrect.");
    }

    private static async Task VerifyBrotherMoves(PhaseFiveContext fight)
    {
        LiteratureFloorBlackSwanBrotherBase brother =
            LivingBrothers(fight.State)
                .First(static candidate => candidate.BrotherNumber == 1);
        await RemovePower<LibraryWeakPower>(fight.Player);
        ForceMove(
            brother,
            LiteratureFloorBlackSwanBrotherBase.GreenFilthMoveId);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        await brother.PerformMove();
        LibraryWeakPower? weak = fight.Player.GetPower<LibraryWeakPower>();
        Require(
            weak?.Amount
                == LiteratureFloorBlackSwanBrotherBase.WeakAmount
            && weak.TurnsRemaining
                == LiteratureFloorBlackSwanBrotherBase.WeakTurns,
            "Green Filth did not apply 2 Weak for 1 turn.");

        await RemovePower<LibraryOfRuinaNextTurnStrength>(
            fight.BossCreature);
        ForceMove(
            brother,
            LiteratureFloorBlackSwanBrotherBase.BluffMoveId);
        await SetPlayerToFullHpAndClearBlock(fight.Player);
        await brother.PerformMove();
        Require(
            fight.BossCreature.GetPower<
                    LibraryOfRuinaNextTurnStrength>()?.Amount
                == LiteratureFloorBlackSwanBrotherBase
                    .BluffNextTurnStrength,
            "Bluff did not grant Black Swan 1 Next-Turn Strength.");
    }

    private static async Task VerifyRemainingSummonsAndPassives(
        PhaseFiveContext fight)
    {
        LiteratureFloorBlackSwanBrotherBase fourth =
            LivingBrothers(fight.State)
                .Single(static brother => brother.BrotherNumber == 4);
        await CreatureCmd.Kill(fourth.Creature, force: true);
        await WaitFrames(3);
        ForceMove(fight.Boss, LiteratureFloorBlackSwanBoss.StruggleMoveId);
        await fight.Boss.OnPlayerSideTurnStart(fight.State);
        Require(
            LivingBrothers(fight.State).Any(static brother =>
                brother.BrotherNumber == 5)
            && fight.Encounter.NextBlackSwanBrother == 6,
            "Fifth Brother was not summoned after an open slot appeared.");

        LiteratureFloorBlackSwanBrotherBase third =
            LivingBrothers(fight.State)
                .Single(static brother => brother.BrotherNumber == 3);
        await CreatureCmd.Kill(third.Creature, force: true);
        await WaitFrames(3);
        ForceMove(fight.Boss, LiteratureFloorBlackSwanBoss.StruggleMoveId);
        await fight.Boss.OnPlayerSideTurnStart(fight.State);
        LiteratureFloorBlackSwanBrotherBase sixth =
            LivingBrothers(fight.State)
                .Single(static brother => brother.BrotherNumber == 6);
        Require(
            fight.Encounter.NextBlackSwanBrother == 7,
            "Sixth Brother did not permanently close the summon sequence.");

        LiteratureFloorBlackSwanBrotherBase fifth =
            LivingBrothers(fight.State)
                .Single(static brother => brother.BrotherNumber == 5);
        await CreatureCmd.SetCurrentHp(
            fight.BossCreature,
            fight.BossCreature.MaxHp - 80m);
        decimal hpBeforeHeal = fight.BossCreature.CurrentHp;
        LiteratureFloorBlackSwanFifthBrotherPassivePower fifthPower =
            fifth.Creature.GetPower<
                LiteratureFloorBlackSwanFifthBrotherPassivePower>()
            ?? throw new InvalidOperationException(
                "Fifth Brother passive was missing.");
        await fifthPower.AfterSideTurnEnd(
            new ThrowingPlayerChoiceContext(),
            CombatSide.Enemy,
            [fifth.Creature]);
        decimal expectedHeal = Math.Ceiling(
            fight.BossCreature.MaxHp
            * LiteratureFloorBlackSwanBoss.FifthBrotherHealPercent
            / 100m);
        Require(
            fight.BossCreature.CurrentHp
                == Math.Min(
                    fight.BossCreature.MaxHp,
                    hpBeforeHeal + expectedHeal),
            "Fifth Brother did not heal 20% of Black Swan Max HP.");

        ForceMove(fight.Boss, LiteratureFloorBlackSwanBoss.StruggleMoveId);
        await CreatureCmd.Kill(sixth.Creature, force: true);
        await WaitFrames(3);
        Require(
            fight.BossCreature is LibraryCreature
            {
                CurrentChaoValue: 0
            },
            "Sixth Brother death did not Stagger Black Swan.");
        await CreatureCmd.Kill(fifth.Creature, force: true);
        await WaitFrames(3);
        LiteratureFloorBlackSwanVanishingFamilyPower counter =
            fight.BossCreature.GetPower<
                LiteratureFloorBlackSwanVanishingFamilyPower>()
            ?? throw new InvalidOperationException(
                "Vanishing Family counter was missing.");
        Require(
            counter.Amount == 4,
            "Four Brother deaths did not reach Vanishing Family 4.");

        await fight.Boss.OnPlayerSideTurnStart(fight.State);
        Require(
            counter.Amount == 4
            && !fight.Boss.IsSwanSongUnlocked
            && !fight.Boss.IsSwanSongQueued,
            "Swan Song unlocked before every Brother died.");

        foreach (LiteratureFloorBlackSwanBrotherBase brother
                 in LivingBrothers(fight.State).ToArray())
        {
            await CreatureCmd.Kill(brother.Creature, force: true);
        }
        await WaitFrames(3);
        Require(
            counter.Amount == LiteratureFloorBlackSwanBoss
                .DeadBrothersForSwanSong
            && fight.Boss.IsSwanSongUnlocked
            && LivingBrothers(fight.State).Length == 0,
            "All Brother deaths did not unlock Swan Song.");

        PropertyInfo cycleMaskProperty = typeof(
                LiteratureFloorBlackSwanBoss)
            .GetProperty(nameof(
                LiteratureFloorBlackSwanBoss.CompletedMoveCycleMask))
            ?? throw new InvalidOperationException(
                "Black Swan cycle mask property was missing.");
        cycleMaskProperty.SetValue(fight.Boss, 0);
        var cycleMoves = new HashSet<string>(StringComparer.Ordinal);
        for (int move = 0; move < 6; move++)
        {
            fight.BossCreature.PrepareForNextTurn(
                fight.State.PlayerCreatures);
            string moveId = fight.Boss.NextMove.StateId;
            Require(
                cycleMoves.Add(moveId),
                "Black Swan repeated " + moveId
                + " before completing the six-move cycle.");
            await SetPlayerToFullHpAndClearBlock(fight.Player);
            await fight.Boss.PerformMove();
        }

        Require(
            cycleMoves.SetEquals(
            [
                .. BossNormalMoveIds,
                LiteratureFloorBlackSwanBoss.SwanSongMoveId
            ])
            && fight.Boss.CompletedMoveCycleMask == 0,
            "Black Swan did not complete and reset one 1-6 move cycle.");
    }

    private static async Task<PhaseFiveContext> StartPhaseFiveFight()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "LITERATUREFLOORVERIFY_PHASE5",
            GameMode.Standard,
            ascensionLevel: 0);
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);

        var encounter = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "5",
            ["KilledBossCount"] = "4",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False",
            ["BlackSwanRoundStarts"] = "0",
            ["NextBlackSwanBrother"] = "3"
        });
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress,
            "Literature floor phase-five combat start");
        await WaitFrames(8);

        CombatState state = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var activeEncounter = state.Encounter
                as LiteratureFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "Literature floor encounter was not active.");
        LiteratureFloorBlackSwanBoss boss = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorBlackSwanBoss>()
            .Single();
        return new PhaseFiveContext(
            state,
            activeEncounter,
            boss,
            boss.Creature,
            state.PlayerCreatures.Single());
    }

    private static LiteratureFloorBlackSwanBrotherBase[]
        LivingBrothers(CombatState state) =>
        state.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorBlackSwanBrotherBase>()
            .OrderBy(static brother => brother.BrotherNumber)
            .ToArray();

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
            ? GameApi.LoseBlock(
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
        Require(
            machine.States.TryGetValue(moveId, out MonsterState? state)
            && state is MoveState,
            "Missing move state " + moveId + ".");
        Type[] actual = ((MoveState)state!).Intents
            .Select(static intent => intent.GetType())
            .ToArray();
        Require(
            actual.SequenceEqual(expectedTypes),
            moveId + " intent types changed: "
            + string.Join(",", actual.Select(static type => type.Name)));
    }

    private static void RequirePrimaryBadgeAmount(
        IndiscriminateAttackIntent intent,
        int expected,
        string message)
    {
        var description = new LocString("intents", "FORMAT_EMPTY");
        BadgedIntentDescription.AddBadgeVariables(
            description,
            intent.Badges);
        Require(
            description.Variables.TryGetValue(
                "BadgeAmount",
                out object? value)
            && Convert.ToInt32(value) == expected,
            message);
    }

    private static void VerifyScene(
        string path,
        IEnumerable<string> requiredNodes)
    {
        string text = FileAccess.GetFileAsString(path);
        Require(
            !text.Contains(
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
                Require(
                    root.GetNodeOrNull(node) != null,
                    path + " is missing " + node + ".");
            }

            Require(
                root.GetNodeOrNull("TalkPos") == null,
                path + " unexpectedly contains dialogue anchoring.");
        }
        finally
        {
            root.Free();
        }
    }

    private static void VerifyEncounterScene()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>(
                LiteratureFloorLiberationEncounter
                    .BlackSwanEncounterScenePath)
            ?? throw new InvalidOperationException(
                "Unable to load Black Swan encounter scene.");
        Node root = scene.Instantiate();
        try
        {
            foreach (string marker in new[]
                     {
                         LiteratureFloorLiberationEncounter
                             .BlackSwanBrotherSlotOne,
                         LiteratureFloorLiberationEncounter
                             .BlackSwanBrotherSlotTwo,
                         LiteratureFloorLiberationEncounter
                             .BlackSwanBrotherSlotThree,
                         LiteratureFloorLiberationEncounter
                             .BlackSwanBrotherSlotFour,
                         LiteratureFloorLiberationEncounter.BlackSwanSlot
                     })
            {
                Require(
                    root.GetNodeOrNull<Marker2D>(marker) != null,
                    "Black Swan encounter scene is missing "
                    + marker + ".");
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
        Require(
            names.SetEquals(expectedLengths.Keys),
            path + " animation names changed: "
            + string.Join(",", names));
        foreach ((string name, double expected) in expectedLengths)
        {
            Animation animation = library.GetAnimation(name)
                ?? throw new InvalidOperationException(
                    path + " is missing animation " + name + ".");
            Require(
                Math.Abs(animation.Length - expected) < 0.001d,
                path + " animation " + name + " length changed.");
        }
    }

    private static void VerifyLocalizationKeys()
    {
        string[] monsterKeys =
        [
            "LITERATURE_FLOOR_BLACK_SWAN_BOSS.name",
            .. BossNormalMoveIds.Select(move =>
                "LITERATURE_FLOOR_BLACK_SWAN_BOSS.moves."
                + move + ".title"),
            "LITERATURE_FLOOR_BLACK_SWAN_BOSS.moves.SWAN_SONG.title",
            "LITERATURE_FLOOR_BLACK_SWAN_FIRST_BROTHER.name",
            "LITERATURE_FLOOR_BLACK_SWAN_SECOND_BROTHER.name",
            "LITERATURE_FLOOR_BLACK_SWAN_THIRD_BROTHER.name",
            "LITERATURE_FLOOR_BLACK_SWAN_FOURTH_BROTHER.name",
            "LITERATURE_FLOOR_BLACK_SWAN_FIFTH_BROTHER.name",
            "LITERATURE_FLOOR_BLACK_SWAN_SIXTH_BROTHER.name"
        ];
        string[] powerKeys =
        [
            "LITERATURE_FLOOR_BLACK_SWAN_NETTLE_GARMENT_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_BLACK_SWAN_PROTECT_FAMILY_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_BLACK_SWAN_BROKEN_DREAM_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_BLACK_SWAN_VANISHING_FAMILY_POWER.title",
            "LITERATURE_FLOOR_BLACK_SWAN_FIRST_BROTHER_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_BLACK_SWAN_SECOND_BROTHER_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_BLACK_SWAN_THIRD_BROTHER_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_BLACK_SWAN_FOURTH_BROTHER_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_BLACK_SWAN_FIFTH_BROTHER_PASSIVE_POWER.title",
            "LITERATURE_FLOOR_BLACK_SWAN_SIXTH_BROTHER_PASSIVE_POWER.title"
        ];
        string[] intentKeys =
        [
            "LITERATURE_FLOOR_BLACK_SWAN_STRUGGLE.description",
            "LITERATURE_FLOOR_BLACK_SWAN_CURL_UP.description",
            "LITERATURE_FLOOR_BLACK_SWAN_OLD_UMBRELLA.description",
            "LITERATURE_FLOOR_BLACK_SWAN_SWAN_SONG.description",
            "LITERATURE_FLOOR_BLACK_SWAN_BROTHER_GREEN_FILTH.description",
            "LITERATURE_FLOOR_BLACK_SWAN_BROTHER_BLUFF.description"
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
            Require(
                root.TryGetProperty(key, out _),
                language + "/" + table + " is missing " + key + ".");
        }
    }

    private static void RequireUniformNormal(
        LibraryCreatureResistanceData.Resistance? resistance,
        string label)
    {
        Require(
            resistance != null
            && resistance.Slash == LibraryResistanceLevel.Normal
            && resistance.Pierce == LibraryResistanceLevel.Normal
            && resistance.Blunt == LibraryResistanceLevel.Normal,
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

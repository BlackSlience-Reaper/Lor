using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.KuroKumo;
using LibraryOfRuina.encounters.ScorchedGirl;
using LibraryOfRuina.events.SongMachine;
using LibraryOfRuina.features.secondascension;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using LibraryOfRuina.relics.BookShadow;
using LibraryOfRuina.scene_transitions;
using LibraryOfRuina.specialguests;
using LibraryOfRuina.specialguests.Kali;
using LibraryOfRuina.specialguests.Xiao;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// Development-only executable contract verifier for the reusable special-guest
/// framework and Xiao's pure encounter data. It deliberately avoids entering a
/// room, so failures isolate registration/save/AI-data drift from scene timing.
/// </summary>
internal static class SpecialGuestXiaoContractVerificationPatch
{
    private const string VerifyArg = "lor-verify-special-guests";
    private const string TurnTimingVerifyArg = "lor-verify-xiao-turn-timing";
    private const string AnimationVerifyArg =
        "lor-verify-xiao-tscn-animation";
    private const string MoveDataVerifyArg = "lor-verify-xiao-move-data";
    private const string ReverseScaleCounterVerifyArg = "lor-verify-xiao-reverse-scale-counter";
    private const string ProgressVerifyArg = "lor-verify-special-guest-progress";
    private const string SaveResumeVerifyArg = "lor-verify-special-guest-save-resume";
    private const string LogPrefix = "[LibraryOfRuina.SpecialGuests.Verify] ";
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly OpCode[] SingleByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] DoubleByteOpCodes = new OpCode[0x100];

    private static bool _started;

    static SpecialGuestXiaoContractVerificationPatch()
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(
                     BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            ushort value = unchecked((ushort)opCode.Value);
            if (value < 0x100)
            {
                SingleByteOpCodes[value] = opCode;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
                DoubleByteOpCodes[value & 0xff] = opCode;
            }
        }
    }

    internal static void Start()
    {
        if (_started
            || (!HasVerifyArg()
                && !HasTurnTimingVerifyArg()
                && !HasAnimationVerifyArg()
                && !HasMoveDataVerifyArg()
                && !HasReverseScaleCounterVerifyArg()
                && !HasProgressVerifyArg()
                && !HasSaveResumeVerifyArg()))
        {
            return;
        }

        _started = true;
        Callable.From(Run).CallDeferred();
    }

    private static bool HasVerifyArg() => HasArg(VerifyArg);

    private static bool HasTurnTimingVerifyArg() => HasArg(TurnTimingVerifyArg);

    private static bool HasAnimationVerifyArg() => HasArg(AnimationVerifyArg);

    private static bool HasMoveDataVerifyArg() => HasArg(MoveDataVerifyArg);

    private static bool HasReverseScaleCounterVerifyArg() =>
        HasArg(ReverseScaleCounterVerifyArg);

    private static bool HasProgressVerifyArg() => HasArg(ProgressVerifyArg);

    private static bool HasSaveResumeVerifyArg() => HasArg(SaveResumeVerifyArg);

    private static bool HasArg(string argument) =>
        CommandLineHelper.HasArg(argument)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            argument,
            StringComparison.OrdinalIgnoreCase));

    private static void Run()
    {
        try
        {
            if (HasAnimationVerifyArg())
            {
                VerifyXiaoSceneAnimationContracts();
                Log.Info(LogPrefix + "XIAO_TSCN_ANIMATION_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasSaveResumeVerifyArg())
            {
                VerifySpecialGuestSaveResumeContract();
                Log.Info(LogPrefix + "SPECIAL_GUEST_SAVE_RESUME_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasMoveDataVerifyArg())
            {
                VerifyXiaoIntentContracts();
                Log.Info(LogPrefix + "XIAO_MOVE_DATA_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasReverseScaleCounterVerifyArg())
            {
                VerifyReverseScalePlayerCounters();
                Log.Info(LogPrefix + "XIAO_REVERSE_SCALE_COUNTER_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasProgressVerifyArg())
            {
                VerifyPublicProgressContracts();
                Log.Info(LogPrefix + "SPECIAL_GUEST_PROGRESS_STATE_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasTurnTimingVerifyArg())
            {
                VerifyXiaoTurnTimingContracts();
                Log.Info(LogPrefix + "XIAO_TURN_TIMING_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            VerifyStableReplacementMath();
            VerifyReplacementStateMachine();
            VerifyPublicProgressContracts();
            VerifyNeowInternalModifierFiltering();
            VerifyXiaoRegistration();
            VerifyXiaoPresentationContracts();
            VerifyXiaoVisualLayouts();
            VerifyXiaoStatsAndPersistence();
            VerifyXiaoPatternsAndCapacity();
            VerifyXiaoIntentContracts();
            VerifyXiaoPassiveAndNullifyContracts();
            VerifyXiaoTurnTimingContracts();
            VerifyEmotionAndScalingContracts();
            VerifySpecialGuestEscapePenaltyContract();
            VerifySpecialGuestSaveResumeContract();
            VerifySpecialGuestStorySkipContract();
            VerifySupportLibraryAttackTargets();

            Log.Info(LogPrefix + "SPECIAL_GUEST_XIAO_CONTRACT_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "SPECIAL_GUEST_XIAO_CONTRACT_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyStableReplacementMath()
    {
        int[] guestCounts = new int[3];
        for (int index = 0; index < 8192; index++)
        {
            string key = "special-guest-contract|" + index;
            ulong actualHash = SpecialGuestRegistry.StableHash64(key);
            Require(actualHash == IndependentStableHash64(key),
                "StableHash64 diverged from its platform-independent contract.");
            Require(actualHash == SpecialGuestRegistry.StableHash64(key),
                "StableHash64 returned a different result for the same key.");

            int guestIndex = (int)(SpecialGuestRegistry.StableHash64(key + "|guest") % 3UL);
            guestCounts[guestIndex]++;
        }

        foreach (int count in guestCounts)
        {
            decimal guestRatio = count / 8192m;
            Require(guestRatio is >= 0.30m and <= 0.37m,
                $"Three-guest selection is not near-uniform: {string.Join(',', guestCounts)}.");
        }
    }

    private static void VerifyNeowInternalModifierFiltering()
    {
        SpecialGuestRunStateModifier carrier = (SpecialGuestRunStateModifier)
            ModelDb.Modifier<SpecialGuestRunStateModifier>().ToMutable();
        ModifierModel visible = ModelDb.GoodModifiers.First().ToMutable();
        IReadOnlyList<ModifierModel> filtered =
            LibrarySecondAscensionNeowModifierFilter.FilterForNeow(
                [carrier, visible]);

        Require(filtered.Count == 1 && ReferenceEquals(filtered[0], visible),
            "The hidden special-guest run-state carrier changed Neow's relic options.");
    }

    private static void VerifyReplacementStateMachine()
    {
        bool unlockEnabled = true;
        string[] guestIds =
        [
            "ZZ_SPECIAL_GUEST_VERIFY_A",
            "ZZ_SPECIAL_GUEST_VERIFY_B",
            "ZZ_SPECIAL_GUEST_VERIFY_C",
        ];
        var stage = new SpecialGuestStageDefinition(
            () => ModelDb.Encounter<SlimesWeak>());

        foreach (string guestId in guestIds)
        {
            SpecialGuestRegistry.Register(new SpecialGuestDefinition(
                guestId,
                new LocString("events", "ABYSSAL_BATHS.title"),
                _ => unlockEnabled,
                () => ModelDb.Event<XiaoSpecialGuestEvent>(),
                [stage]));
        }

        EventModel firstEvent = ModelDb.Event<SingingMachineEvent>();
        string seed = "SPECIALGUESTVERIFY100PERCENT";

        RunState firstRun = CreateContractRun(seed, out SpecialGuestRunStateModifier firstState);
        SpecialGuestRegistry.EnsureUnlocks(firstRun, firstState);
        Require(guestIds.All(firstState.IsUnlocked),
            "Synthetic guests did not unlock when their predicate became true.");

        unlockEnabled = false;
        SpecialGuestRegistry.EnsureUnlocks(firstRun, firstState);
        Require(guestIds.All(firstState.IsUnlocked),
            "An unlocked guest was cleared after its predicate became false.");

        FieldInfo visitedCoordsField = typeof(RunState).GetField(
                "_visitedMapCoords",
                InstanceFlags)
            ?? throw new MissingFieldException(typeof(RunState).FullName, "_visitedMapCoords");
        var visitedCoords = (List<MapCoord>)(visitedCoordsField.GetValue(firstRun)
            ?? throw new InvalidOperationException("RunState visited coordinates are null."));
        visitedCoords.Add(new MapCoord(4, 7));
        string coordinateKey = SpecialGuestRegistry.BuildRollKey(firstRun, firstEvent);
        Require(coordinateKey.Contains("|coord=4,7|", StringComparison.Ordinal),
            "A concrete map coordinate was omitted from the deterministic roll key.");
        visitedCoords.Clear();
        firstRun.CurrentActIndex = 1;
        Require(SpecialGuestRegistry.BuildRollKey(firstRun, firstEvent)
                .Contains("|act=1|", StringComparison.Ordinal),
            "The current chapter was omitted from the deterministic roll key.");
        firstRun.CurrentActIndex = 0;

        string firstKey = SpecialGuestRegistry.BuildRollKey(firstRun, firstEvent);
        Require(firstKey == ExpectedRollKey(firstRun, firstEvent),
            "Roll key omitted seed, act, coordinate, or original event ID.");

        Require(BookShadowEncounterReplacement.CanReplaceRoomType(RoomType.Monster)
                && !BookShadowEncounterReplacement.CanReplaceRoomType(RoomType.Elite),
            "Book Shadow is not limited to weak/strong normal encounters.");
        Require(LibraryEncounterWeighting.IsNormalEncounterPoolCandidate(
                    ModelDb.Encounter<ScorchedGirl>())
                && !LibraryEncounterWeighting.IsNormalEncounterPoolCandidate(
                    ModelDb.Encounter<KuroKumoNormal>()),
            "Normal encounter weighting still includes regular guest receptions.");
        MethodInfo specialGuestHook = GetMethod(
            typeof(SpecialGuestModifyNextEventPatch),
            "Postfix");
        Require(CallsMethod(
                    specialGuestHook,
                    typeof(SpecialGuestRegistry),
                    nameof(SpecialGuestRegistry.TryReplaceNextEvent))
                && !GetCalledMethods(specialGuestHook).Any(static method =>
                    method.DeclaringType == typeof(BookShadowEncounterReplacement)
                    || method.DeclaringType == typeof(BookShadowRelic)),
            "Special-guest question-mark replacement is controlled by Book Shadow.");

        firstRun.ActFloor = SpecialGuestDefinition.MinimumActFloor - 1;
        EventModel belowWindow = SpecialGuestRegistry.TryReplaceNextEvent(firstRun, firstEvent);
        Require(ReferenceEquals(belowWindow, firstEvent)
                && !firstState.HasAttemptedRoll(firstKey)
                && guestIds.All(id => !firstState.IsConsumed(id)),
            "A special guest appeared before ActFloor 4 or consumed its pending replacement.");

        int lastActFloor = firstRun.Act.GetNumberOfRooms(firstRun.Players.Count > 1) + 1;
        firstRun.ActFloor = lastActFloor + 1;
        EventModel aboveWindow = SpecialGuestRegistry.TryReplaceNextEvent(firstRun, firstEvent);
        Require(ReferenceEquals(aboveWindow, firstEvent)
                && !firstState.HasAttemptedRoll(firstKey)
                && guestIds.All(id => !firstState.IsConsumed(id)),
            "A special guest appeared after the final normal ActFloor.");

        firstRun.ActFloor = SpecialGuestDefinition.MinimumActFloor;
        EventModel replacement = SpecialGuestRegistry.TryReplaceNextEvent(firstRun, firstEvent);
        string expectedGuest = guestIds
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ElementAt((int)(SpecialGuestRegistry.StableHash64(firstKey + "|guest")
                             % (ulong)guestIds.Length));
        Require(replacement is XiaoSpecialGuestEvent,
            "An eligible special guest did not replace the next question-mark event.");
        Require(firstState.IsConsumed(expectedGuest),
            "The selected guest was not consumed immediately.");
        Require(firstState.HasAttemptedRoll(firstKey),
            "The guaranteed replacement was not persisted as attempted.");
        Require(firstState.GetValue("replacement." + firstKey) == expectedGuest,
            "The selected guest ID was not persisted with its roll key.");
        EventModel repeatedReplacement =
            SpecialGuestRegistry.TryReplaceNextEvent(firstRun, firstEvent);
        Require(repeatedReplacement is XiaoSpecialGuestEvent
                && repeatedReplacement.GetType().Assembly
                == typeof(SpecialGuestRegistry).Assembly,
            "A repeated successful roll did not restore the same canonical special event.");
        repeatedReplacement.AssertCanonical();
        Require(firstState.GetValue("replacement." + firstKey) == expectedGuest,
            "A repeated successful roll changed the persisted selected guest.");

        firstState.BeginGuest(expectedGuest);
        firstState.ClearActiveGuest();
        Require(firstState.IsConsumed(expectedGuest),
            "Leaving the event cleared its consumed state, allowing an escape reroll.");

        SpecialGuestRunStateModifier restored =
            (SpecialGuestRunStateModifier)ModifierModel.FromSerializable(firstState.ToSerializable());
        Require(restored.IsUnlocked(expectedGuest)
                && restored.IsConsumed(expectedGuest)
                && restored.HasAttemptedRoll(firstKey)
                && restored.GetValue("replacement." + firstKey) == expectedGuest,
            "Special-guest unlock/attempt/consumption state did not survive serialization.");

        unlockEnabled = true;
        RunState repeatedRun = CreateContractRun(seed, out SpecialGuestRunStateModifier repeatedState);
        SpecialGuestRegistry.EnsureUnlocks(repeatedRun, repeatedState);
        repeatedRun.ActFloor = SpecialGuestDefinition.MinimumActFloor;
        _ = SpecialGuestRegistry.TryReplaceNextEvent(repeatedRun, firstEvent);
        Require(repeatedState.GetValue("replacement." + firstKey) == expectedGuest,
            "The same seed/act/coordinate/event selected a different guest.");
    }

    private static void VerifyPublicProgressContracts()
    {
        RunState runState = CreateContractRun(
            "SPECIALGUESTPROGRESSVERIFY",
            out SpecialGuestRunStateModifier state);

        Require(!FloorLiberationProgress.IsAnyFullyLiberated(
                    runState,
                    LiberationFloorIds.FirstAct)
                && !SpecialGuestReceptionProgress.WasSuccessfullyReceived(
                    runState,
                    KaliSpecialGuestIds.Guest),
            "A fresh run already exposes liberation or guest-victory progress.");

        FloorLiberationProgress.MarkFullyLiberated(
            runState,
            LiberationFloorIds.Technology);
        SpecialGuestReceptionProgress.MarkSuccessfullyReceived(
            runState,
            KaliSpecialGuestIds.Guest);

        Require(FloorLiberationProgress.IsFullyLiberated(
                    runState,
                    LiberationFloorIds.Technology)
                && FloorLiberationProgress.GetFullyLiberatedFloorIds(runState)
                    .Contains(LiberationFloorIds.Technology)
                && SpecialGuestReceptionProgress.WasSuccessfullyReceived(
                    runState,
                    KaliSpecialGuestIds.Guest)
                && SpecialGuestReceptionProgress
                    .GetSuccessfullyReceivedGuestIds(runState)
                    .Contains(KaliSpecialGuestIds.Guest),
            "The public liberation or successful-reception query did not expose recorded progress.");

        state.SetValue(
            "kali.history-floor-liberation-complete",
            bool.TrueString);
        state.SetValue(
            "resolution." + XiaoSpecialGuestIds.Guest,
            "completed");
        Require(FloorLiberationProgress.IsFullyLiberated(
                    runState,
                    LiberationFloorIds.History)
                && SpecialGuestReceptionProgress.WasSuccessfullyReceived(
                    runState,
                    XiaoSpecialGuestIds.Guest),
            "Legacy History or completed-resolution progress is not readable.");

        SpecialGuestRunStateModifier restored =
            (SpecialGuestRunStateModifier)ModifierModel.FromSerializable(
                state.ToSerializable());
        Require(restored.IsFloorFullyLiberated(
                    LiberationFloorIds.Technology)
                && restored.WasSuccessfullyReceived(
                    KaliSpecialGuestIds.Guest),
            "Public liberation or guest-victory progress did not survive serialization.");

        Require(XiaoSpecialGuestRegistration.MeetsUnlockContract(
                    2,
                    [10],
                    kaliSuccessfullyReceived: true)
                && !XiaoSpecialGuestRegistration.MeetsUnlockContract(
                    2,
                    [10],
                    kaliSuccessfullyReceived: false)
                && !XiaoSpecialGuestRegistration.MeetsUnlockContract(
                    2,
                    [9],
                    kaliSuccessfullyReceived: true)
                && !XiaoSpecialGuestRegistration.MeetsUnlockContract(
                    1,
                    [10],
                    kaliSuccessfullyReceived: true),
            "Xiao's unlock contract does not require Act 3, ten pages, and a successful Kali reception.");
    }

    private static void VerifyXiaoRegistration()
    {
        MethodInfo registration = typeof(XiaoSpecialGuestRegistration).GetMethod(
                nameof(XiaoSpecialGuestRegistration.Register),
                StaticFlags)
            ?? throw new MissingMethodException(
                typeof(XiaoSpecialGuestRegistration).FullName,
                nameof(XiaoSpecialGuestRegistration.Register));
        Require(registration.GetCustomAttribute<SpecialGuestRegistrationAttribute>() != null,
            "Xiao registration is not discoverable by SpecialGuestAutoRegistrar.");
        Require(SpecialGuestRegistry.TryGet(
                XiaoSpecialGuestIds.Guest,
                out SpecialGuestDefinition definition),
            "Xiao was not registered in SpecialGuestRegistry.");
        Require(definition.Stages.Count == 2,
            $"Xiao registered {definition.Stages.Count} stages instead of two.");
        EventModel eventModel = definition.EventFactory();
        Require(eventModel.GetType().Assembly
                == typeof(SpecialGuestRegistry).Assembly,
            "Xiao's event factory does not return this mod's hidden event.");
        eventModel.AssertCanonical();
        Require(SpecialGuestEventConsoleProcessPatch.TryFindEvent(
                    XiaoSpecialGuestIds.Event,
                    out EventModel consoleEvent)
                && ReferenceEquals(consoleEvent, eventModel),
            "The native event console command cannot resolve Xiao's hidden event.");

        SpecialGuestStageDefinition stageOne = definition.Stages[0];
        SpecialGuestStageDefinition stageTwo = definition.Stages[1];
        Require(stageOne.BeforeCombatStory == null
                && stageOne.AfterVictoryStory is { Lines.Count: 26, HasBackgroundMusic: false },
            "Stage one must own the 26-line post-victory story without story BGM.");
        Require(stageTwo.BeforeCombatStory is { Lines.Count: 4, HasBackgroundMusic: false }
                && stageTwo.AfterVictoryStory == null,
            "Stage two must own the four-line pre-combat story without story BGM.");
        Require(stageTwo.RewardAugmenter != null,
            "Stage two did not register the empty reward extension point.");
        Require(stageOne.AfterVictoryStory!.Lines.All(static line =>
                    !string.IsNullOrWhiteSpace(line.VoicePath))
                && stageTwo.BeforeCombatStory!.Lines.All(static line =>
                    !string.IsNullOrWhiteSpace(line.VoicePath)),
            "One or more Xiao story lines has no voice resource.");
        Require(stageTwo.BeforeCombatStory!.Lines.All(static line =>
                    line.ArtLayout is { } layout
                    && Math.Abs(layout.Scale - 1.42f) < 0.001f
                    && Math.Abs(layout.OffsetX - 180f) < 0.001f
                    && Math.Abs(layout.OffsetY + 270f) < 0.001f),
            "Stage two Xiao story art must be larger, right-shifted, and raised.");

        RunState assetRun = CreateContractRun(
            "SPECIALGUESTASSETVERIFY",
            out _);
        string[] eventAssetPaths = eventModel.GetAssetPaths(assetRun).ToArray();
        string[] expectedStageAssets = definition.Stages
            .SelectMany(static stage => stage.GetAssetPaths())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Require(expectedStageAssets.All(path =>
                eventAssetPaths.Contains(path, StringComparer.Ordinal)),
            "Xiao event preload omitted one or more story/stage assets.");
        _ = PreloadManager.Cache.GetTexture2D(
            XiaoSpecialGuestIds.StageTwoBackground);

        AssertEncounterStage(
            stageOne.EncounterFactory(),
            expectedIndex: 0,
            expectedMonsterCount: 2);
        AssertEncounterStage(
            stageTwo.EncounterFactory(),
            expectedIndex: 1,
            expectedMonsterCount: 1);
    }

    private static void AssertEncounterStage(
        EncounterModel encounter,
        int expectedIndex,
        int expectedMonsterCount)
    {
        Require(encounter is ISpecialGuestEncounterStage stage
                && stage.SpecialGuestId == XiaoSpecialGuestIds.Guest
                && stage.SpecialGuestStageIndex == expectedIndex,
            $"Stage {expectedIndex + 1} encounter metadata is incorrect.");
        Require(encounter.RoomType == RoomType.Monster && encounter.ShouldGiveRewards,
            $"Stage {expectedIndex + 1} is not a reward-bearing monster room.");
        Require(encounter.AllPossibleMonsters.Count() == expectedMonsterCount,
            $"Stage {expectedIndex + 1} registered the wrong monster count.");
    }

    private static void VerifyXiaoPresentationContracts()
    {
        MethodInfo reveal = GetMethod(
            typeof(XiaoSpecialGuestBackgroundController),
            nameof(XiaoSpecialGuestBackgroundController.RevealStageTwoAsync));
        IReadOnlyList<MethodBase> revealCalls = GetCalledMethods(reveal);
        int bgmStartIndex = revealCalls.ToList().FindIndex(method =>
            method.DeclaringType == typeof(XiaoSpecialGuestBgmController)
            && method.Name == nameof(XiaoSpecialGuestBgmController.ForceStageTwo));
        int revealIndex = revealCalls.ToList().FindIndex(method =>
            method.DeclaringType == typeof(LorexSceneTransitionController)
            && method.Name == nameof(LorexSceneTransitionController.PlayRevealAsync));
        Require(bgmStartIndex >= 0 && revealIndex >= 0 && bgmStartIndex < revealIndex,
            "Stage-two BGM no longer starts immediately when the story releases.");

        MethodInfo combatEndPrefix = GetMethod(
            typeof(XiaoSpecialGuestBgmCombatEndPatch),
            "Prefix");
        Require(CallsMethod(
                combatEndPrefix,
                typeof(XiaoSpecialGuestBgmController),
                nameof(XiaoSpecialGuestBgmController.Stop)),
            "The immediate Xiao combat-victory BGM stop patch is missing.");

        MethodInfo combatLossPrefix = GetMethod(
            typeof(XiaoSpecialGuestBgmCombatLossPatch),
            "Prefix");
        Require(CallsMethod(
                combatLossPrefix,
                typeof(XiaoSpecialGuestBgmController),
                nameof(XiaoSpecialGuestBgmController.Stop)),
            "The immediate Xiao combat-loss BGM stop patch is missing.");

        MethodInfo combatResetPrefix = GetMethod(
            typeof(XiaoSpecialGuestBgmCombatResetPatch),
            "Prefix");
        Require(CallsMethod(
                combatResetPrefix,
                typeof(XiaoSpecialGuestBgmController),
                nameof(XiaoSpecialGuestBgmController.Stop)),
            "The console room-switch combat-reset BGM stop patch is missing.");

        MethodInfo modelCleanup = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            nameof(XiaoSpecialGuestMonsterBase.AfterCombatEnd));
        Require(CallsMethod(
                modelCleanup,
                typeof(XiaoSpecialGuestBgmController),
                nameof(XiaoSpecialGuestBgmController.Stop)),
            "Xiao's model-level combat cleanup no longer stops its BGM.");
    }

    private static void VerifyXiaoVisualLayouts()
    {
        VerifyXiaoSceneAnimationContracts();
    }

    private static void VerifyXiaoSceneAnimationContracts()
    {
        Require(Mathf.IsEqualApprox(
                    XiaoAnimationContract.AttackDurationSeconds,
                    0.8f)
                && Mathf.IsEqualApprox(
                    XiaoAnimationContract.AttackSettlementDelaySeconds,
                    0.9f)
                && XiaoAnimationContract.AttackSettlementDelaySeconds
                > XiaoAnimationContract.AttackDurationSeconds,
            "Xiao attacks must animate for 0.8s and settle at 0.9s.");

        VerifyXiaoScene(
            XiaoStageOneCreatureVisuals.ScenePath,
            XiaoAnimationContract.StageOneLibrary,
            XiaoAnimationContract.StageOneAnimations,
            "xiao_stage_one_animations.tres",
            expectedMotionRootPosition: new Vector2(0f, -120f),
            expectedMotionRootScale: new Vector2(1.2f, 1.2f),
            expectedSpritePosition: new Vector2(25.2101f, -50f),
            expectedSpriteScale: new Vector2(0.47f, 0.47f),
            expectedBounds: new Vector4(-128f, -325f, 164f, -29f),
            expectedBoundsScale: Vector2.One,
            expectedCenter: new Vector2(20f, -174f),
            expectedIntent: new Vector2(64.27f, -324f),
            expectedTalk: new Vector2(0f, -217f));
        VerifyXiaoScene(
            XiaoEgoCreatureVisuals.ScenePath,
            XiaoAnimationContract.StageTwoLibrary,
            XiaoAnimationContract.StageTwoAnimations,
            "xiao_ego_animations.tres",
            expectedMotionRootPosition: new Vector2(0f, -170f),
            expectedMotionRootScale: new Vector2(1.2f, 1.2f),
            expectedSpritePosition: new Vector2(10.1935f, -45f),
            expectedSpriteScale: new Vector2(0.45f, 0.45f),
            expectedBounds: new Vector4(
                -219f,
                -454f,
                219f,
                0f),
            expectedBoundsScale: Vector2.One,
            expectedCenter: new Vector2(0f, -184f),
            expectedIntent: new Vector2(11f, -352f),
            expectedTalk: new Vector2(-36f, -214f));

        Require(XiaoSpecialGuestAssets.StageOneXiao.Contains(
                    XiaoStageOneCreatureVisuals.ScenePath,
                    StringComparer.Ordinal)
                && XiaoSpecialGuestAssets.StageTwoXiao.Contains(
                    XiaoEgoCreatureVisuals.ScenePath,
                    StringComparer.Ordinal),
            "Xiao stage scenes are absent from encounter preloading.");

        var stageOne = XiaoGuestVisualFactory
            .CreateScene<XiaoStageOneCreatureVisuals>(
                "XIAO_STAGE_ONE_VERIFY",
                XiaoStageOneCreatureVisuals.ScenePath);
        var stageTwo = XiaoGuestVisualFactory
            .CreateScene<XiaoEgoCreatureVisuals>(
                "XIAO_EGO_VERIFY",
                XiaoEgoCreatureVisuals.ScenePath);
        try
        {
            foreach (var visuals in new[] { stageOne, stageTwo })
            {
                CreatureStateDisplayOffset offset =
                    visuals.GetNode<CreatureStateDisplayOffset>(
                        "StateDisplayOffset");
                Control bounds = visuals.GetNode<Control>("Bounds");
                Marker2D center = visuals.GetNode<Marker2D>("CenterPos");
                float boundsCenterX =
                    (bounds.OffsetLeft + bounds.OffsetRight) * 0.5f;
                Require(Mathf.IsEqualApprox(offset.LiftY, 40f),
                    "A Xiao scene lost its 40px state-display lift.");
                Require(bounds.Scale.IsEqualApprox(Vector2.One),
                    "A Xiao Bounds node uses scale ignored by NCreature.UpdateBounds.");
                Require(Mathf.Abs(boundsCenterX - center.Position.X) <= 2f,
                    "A Xiao Bounds node is no longer centered on Xiao.");
            }
        }
        finally
        {
            stageOne.Free();
            stageTwo.Free();
        }
    }

    private static void VerifyXiaoScene(
        string scenePath,
        string libraryName,
        IReadOnlyList<string> expectedAnimations,
        string expectedLibraryFile,
        Vector2 expectedMotionRootPosition,
        Vector2 expectedMotionRootScale,
        Vector2 expectedSpritePosition,
        Vector2 expectedSpriteScale,
        Vector4 expectedBounds,
        Vector2 expectedBoundsScale,
        Vector2 expectedCenter,
        Vector2 expectedIntent,
        Vector2 expectedTalk)
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException(
                $"Could not load Xiao visual scene '{scenePath}'.");
        Node2D root = scene.Instantiate<Node2D>();
        try
        {
            Require(root.GetScript().VariantType == Variant.Type.Nil
                    && root.Position.IsEqualApprox(Vector2.Zero)
                    && root.Scale.IsEqualApprox(Vector2.One)
                    && Mathf.IsZeroApprox(root.Rotation)
                    && Mathf.IsZeroApprox(root.Skew),
                $"Xiao scene '{scenePath}' must keep a scriptless identity root.");

            Node2D motionRoot = root.GetNode<Node2D>("MotionRoot");
            Sprite2D visuals = root.GetNode<Sprite2D>(
                "MotionRoot/Visuals");
            Sprite2D attackVisuals = root.GetNode<Sprite2D>(
                "MotionRoot/AttackVisuals");
            Control bounds = root.GetNode<Control>("Bounds");
            Marker2D center = root.GetNode<Marker2D>("CenterPos");
            Marker2D intent = root.GetNode<Marker2D>("IntentPos");
            Marker2D talk = root.GetNode<Marker2D>("TalkPos");
            AnimationPlayer player = root.GetNode<AnimationPlayer>(
                "AnimationPlayer");

            foreach (Node node in new Node[]
                     {
                         motionRoot,
                         visuals,
                         attackVisuals,
                         bounds,
                         center,
                         intent,
                         talk,
                         player,
                     })
            {
                Require(node.UniqueNameInOwner,
                    $"Xiao scene node '{node.Name}' lost unique_name_in_owner.");
            }

            Require(motionRoot.Position.IsEqualApprox(
                        expectedMotionRootPosition)
                    && motionRoot.Scale.IsEqualApprox(
                        expectedMotionRootScale)
                    && visuals.Position.IsEqualApprox(expectedSpritePosition)
                    && visuals.Scale.IsEqualApprox(expectedSpriteScale)
                    && !visuals.Centered
                    && bounds.OffsetLeft == expectedBounds.X
                    && bounds.OffsetTop == expectedBounds.Y
                    && bounds.OffsetRight == expectedBounds.Z
                    && bounds.OffsetBottom == expectedBounds.W
                    && bounds.Scale.IsEqualApprox(expectedBoundsScale)
                    && center.Position.IsEqualApprox(expectedCenter)
                    && intent.Position.IsEqualApprox(expectedIntent)
                    && talk.Position.IsEqualApprox(expectedTalk),
                $"Xiao scene '{scenePath}' layout/anchors changed.");
            Require(string.Equals(
                    player.Autoplay,
                    $"{libraryName}/Idle",
                    StringComparison.Ordinal),
                $"Xiao scene '{scenePath}' autoplay changed.");
            var libraryNames = player.GetAnimationLibraryList();
            Require(libraryNames.All(name => name.ToString() == libraryName
                        || name.ToString().Length == 0),
                $"Xiao scene '{scenePath}' owns an unexpected animation library.");
            if (player.HasAnimationLibrary(string.Empty))
            {
                AnimationLibrary resetLibrary = player.GetAnimationLibrary(
                    string.Empty);
                Require(resetLibrary.GetAnimationList()
                        .Select(static name => name.ToString())
                        .SequenceEqual(new[] { "RESET" }),
                    $"Xiao scene '{scenePath}' default library must contain only RESET.");
            }

            AnimationLibrary library = player.GetAnimationLibrary(libraryName)
                ?? throw new InvalidOperationException(
                    $"Missing Xiao animation library '{libraryName}'.");
            Require(library.ResourcePath.EndsWith(
                    expectedLibraryFile,
                    StringComparison.Ordinal),
                $"Xiao library '{libraryName}' is not an external resource.");
            Require(library.GetAnimationList()
                    .Select(static name => name.ToString())
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .SequenceEqual(expectedAnimations.OrderBy(
                        static name => name,
                        StringComparer.Ordinal)),
                $"Xiao library '{libraryName}' has a missing/extra action.");

            foreach (string trigger in expectedAnimations)
            {
                Animation animation = library.GetAnimation(trigger)
                    ?? throw new InvalidOperationException(
                        $"Missing Xiao animation '{libraryName}/{trigger}'.");
                if (trigger == "Idle")
                {
                    Require(animation.LoopMode
                            == Animation.LoopModeEnum.Linear,
                        $"Xiao '{libraryName}/Idle' must loop.");
                    VerifyXiaoTrackMode(
                        animation,
                        "MotionRoot/Visuals:texture",
                        Animation.UpdateMode.Discrete,
                        $"{libraryName}/{trigger}");
                    continue;
                }

                Require(animation.LoopMode == Animation.LoopModeEnum.None
                        && Mathf.IsEqualApprox(
                            animation.Length,
                            XiaoAnimationContract.DurationForTrigger(trigger)),
                    $"Xiao '{libraryName}/{trigger}' duration drifted.");
                VerifyXiaoActionCompletion(
                    animation,
                    $"{libraryName}/{trigger}");
            }
        }
        finally
        {
            root.Free();
        }
    }

    private static void VerifyXiaoActionCompletion(
        Animation animation,
        string qualifiedName)
    {
        int idleVisible = FindXiaoTrack(
            animation,
            "MotionRoot/Visuals:visible");
        int attackVisible = FindXiaoTrack(
            animation,
            "MotionRoot/AttackVisuals:visible");
        int idleLast = animation.TrackGetKeyCount(idleVisible) - 1;
        int attackLast = animation.TrackGetKeyCount(attackVisible) - 1;
        Require(idleLast >= 1
                && attackLast >= 1
                && animation.TrackGetKeyValue(idleVisible, idleLast).AsBool()
                && !animation.TrackGetKeyValue(
                    attackVisible,
                    attackLast).AsBool()
                && Mathf.IsEqualApprox(
                    (float)animation.TrackGetKeyTime(idleVisible, idleLast),
                    animation.Length)
                && Mathf.IsEqualApprox(
                    (float)animation.TrackGetKeyTime(
                        attackVisible,
                        attackLast),
                    animation.Length),
            $"Xiao '{qualifiedName}' does not restore idle on its final frame.");
        VerifyXiaoTrackMode(
            animation,
            "MotionRoot/AttackVisuals:texture",
            Animation.UpdateMode.Discrete,
            qualifiedName);
        VerifyXiaoTrackMode(
            animation,
            "MotionRoot/AttackVisuals:offset",
            Animation.UpdateMode.Discrete,
            qualifiedName);
        VerifyXiaoTrackMode(
            animation,
            "MotionRoot/AttackVisuals:position",
            Animation.UpdateMode.Continuous,
            qualifiedName);
        VerifyXiaoTrackMode(
            animation,
            "MotionRoot/AttackVisuals:scale",
            Animation.UpdateMode.Continuous,
            qualifiedName);
    }

    private static void VerifyXiaoTrackMode(
        Animation animation,
        string path,
        Animation.UpdateMode expected,
        string qualifiedName)
    {
        int track = FindXiaoTrack(animation, path);
        Require(animation.ValueTrackGetUpdateMode(track) == expected,
            $"Xiao '{qualifiedName}' track '{path}' has the wrong update mode.");
    }

    private static int FindXiaoTrack(Animation animation, string path)
    {
        for (int track = 0; track < animation.GetTrackCount(); track++)
        {
            if (string.Equals(
                    animation.TrackGetPath(track).ToString(),
                    path,
                    StringComparison.Ordinal))
            {
                return track;
            }
        }

        throw new InvalidOperationException(
            $"Animation '{animation.ResourceName}' is missing '{path}'.");
    }

    private static void VerifyXiaoStatsAndPersistence()
    {
        XiaoStageOne stageOne = MutableMonster<XiaoStageOne>();
        Miris miris = MutableMonster<Miris>();
        XiaoEgo stageTwo = MutableMonster<XiaoEgo>();

        AssertHpGetterConstants<XiaoStageOne>(nameof(MonsterModel.MinInitialHp), 392, 397);
        AssertHpGetterConstants<XiaoStageOne>(nameof(MonsterModel.MaxInitialHp), 394, 400);
        AssertHpGetterConstants<XiaoEgo>(nameof(MonsterModel.MinInitialHp), 690, 698);
        AssertHpGetterConstants<XiaoEgo>(nameof(MonsterModel.MaxInitialHp), 694, 700);
        Require(miris.MinInitialHp == 196 && miris.MaxInitialHp == 196,
            "Miris HP is not fixed at 196.");
        Require(stageOne.DefaultChaoResistance == 220
                && miris.DefaultChaoResistance == 143
                && stageTwo.DefaultChaoResistance == 350,
            "Xiao/Miris confusion resistance does not match the reception contract.");
        Require(stageTwo.ChaoRecoveryRatio == 0.5m,
            "Stage-two Xiao does not recover exactly half confusion resistance.");

        AssertResistance(
            stageOne.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Normal,
            "stage-one Xiao physical");
        AssertResistance(
            stageOne.DefaultChaoResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Endure,
            "stage-one Xiao confusion");
        AssertResistance(
            miris.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Normal,
            "Miris physical");
        AssertResistance(
            miris.DefaultChaoResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Endure,
            "Miris confusion");
        AssertResistance(
            stageTwo.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Normal,
            "stage-two Xiao physical");
        AssertResistance(
            stageTwo.DefaultChaoResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Endure,
            "stage-two Xiao confusion");

        string[] baseCombatStateProperties =
        [
            nameof(XiaoSpecialGuestMonsterBase.EmotionLevel),
            nameof(XiaoSpecialGuestMonsterBase.EmotionUnits),
            nameof(XiaoSpecialGuestMonsterBase.IntentCapacity),
            nameof(XiaoSpecialGuestMonsterBase.LevelFiveRoundCounter),
            nameof(XiaoSpecialGuestMonsterBase.UnblockedDamageDealtThisRound),
            nameof(XiaoSpecialGuestMonsterBase.PatternIndex),
            nameof(XiaoSpecialGuestMonsterBase.HasCompletedFirstTurn),
            nameof(XiaoSpecialGuestMonsterBase.PlannedMoveOne),
            nameof(XiaoSpecialGuestMonsterBase.PlannedMoveTwo),
            nameof(XiaoSpecialGuestMonsterBase.PlannedMoveThree),
            nameof(XiaoSpecialGuestMonsterBase.PlannedMoveFour),
            nameof(XiaoSpecialGuestMonsterBase.PlannedMoveFive),
        ];
        foreach (string propertyName in baseCombatStateProperties)
        {
            AssertCombatStateProperty(typeof(XiaoSpecialGuestMonsterBase), propertyName);
        }
        AssertCombatStateProperty(typeof(XiaoStageOne), nameof(XiaoStageOne.IsFakeDead));
        AssertCombatStateProperty(typeof(XiaoStageOne), nameof(XiaoStageOne.ForceTrueDeath));
        AssertCombatStateProperty(
            typeof(XiaoEgo),
            nameof(XiaoEgo.HadAnyAttackResultThisEnemyTurn));
        AssertCombatStateProperty(
            typeof(XiaoEgo),
            nameof(XiaoEgo.AllAttackResultsFullyBlocked));
        AssertCombatStateProperty(
            typeof(XiaoReverseScalePassivePower),
            nameof(XiaoReverseScalePassivePower.CardsPlayedByPlayerNetId));
    }

    private static void VerifyXiaoPatternsAndCapacity()
    {
        XiaoStageOne stageOne = MutableMonster<XiaoStageOne>();
        Miris miris = MutableMonster<Miris>();
        XiaoEgo stageTwo = MutableMonster<XiaoEgo>();

        Require(GetProperty<int>(stageOne, "InitialIntentCapacity") == 3
                && GetProperty<int>(miris, "InitialIntentCapacity") == 3
                && GetProperty<int>(stageTwo, "InitialIntentCapacity") == 4,
            "Initial intent capacities are not 3/3/4.");
        Require(GetProperty<int>(stageOne, "PatternLength") == 4
                && GetProperty<int>(miris, "PatternLength") == 4
                && GetProperty<int>(stageTwo, "PatternLength") == 6,
            "Pattern lengths are not 4/4/6.");

        AssertPlanSequence(
            stageOne,
            3,
            [
                [XiaoGuestMove.BlazingDance, XiaoGuestMove.FieryDragonSlash, XiaoGuestMove.FervidEmotion],
            ]);
        SetProperty(stageOne, nameof(XiaoSpecialGuestMonsterBase.PatternIndex), 2);
        SetProperty(stageOne, nameof(XiaoSpecialGuestMonsterBase.HasCompletedFirstTurn), true);
        AssertPlanSequence(
            stageOne,
            3,
            [
                [XiaoGuestMove.LongDrive, XiaoGuestMove.BlazingDance, XiaoGuestMove.HotBlood],
                [XiaoGuestMove.GreatFlame, XiaoGuestMove.HotBlood],
            ]);
        MethodInfo stageOnePattern = GetMethod(typeof(XiaoStageOne), "GetPattern");
        MethodInfo mirisAliveGetter = GetMethod(typeof(XiaoStageOne), "get_IsMirisAlive");
        Require(CountCalls(stageOnePattern, mirisAliveGetter) == 1,
            "Stage-one pattern two no longer branches on Miris being alive.");

        AssertPlanSequence(
            miris,
            3,
            [
                [XiaoGuestMove.BixueDanxin, XiaoGuestMove.DoubleFlank, XiaoGuestMove.FervidEmotion],
                [XiaoGuestMove.FlameDragonFist, XiaoGuestMove.BixueDanxin, XiaoGuestMove.HotBlood],
                [XiaoGuestMove.BixueDanxin, XiaoGuestMove.FervidEmotion, XiaoGuestMove.DoubleFlank],
                [XiaoGuestMove.BixueDanxin, XiaoGuestMove.FervidEmotion, XiaoGuestMove.FlameDragonFist],
            ]);
        SetProperty(miris, nameof(XiaoSpecialGuestMonsterBase.PatternIndex), 0);
        SetProperty(miris, nameof(XiaoSpecialGuestMonsterBase.HasCompletedFirstTurn), true);
        AssertPlanSequence(
            miris,
            3,
            [[XiaoGuestMove.HotBlood, XiaoGuestMove.DoubleFlank, XiaoGuestMove.FervidEmotion]]);

        AssertPlanSequence(
            stageTwo,
            4,
            [
                [XiaoGuestMove.FieryDragonSlash, XiaoGuestMove.FervidEmotion, XiaoGuestMove.JiaotuSuppressEvil],
                [XiaoGuestMove.FieryDragonSlash, XiaoGuestMove.ChiwenSwallowRidge, XiaoGuestMove.HotBlood],
                [XiaoGuestMove.SuanniSoaringCloud, XiaoGuestMove.YaziVengeance, XiaoGuestMove.JiaotuSuppressEvil],
                [XiaoGuestMove.FervidEmotion, XiaoGuestMove.HotBlood, XiaoGuestMove.SuanniSoaringCloud],
                [XiaoGuestMove.ChiwenSwallowRidge, XiaoGuestMove.JiaotuSuppressEvil, XiaoGuestMove.BianDispute],
                [XiaoGuestMove.TaotieFeast, XiaoGuestMove.SuanniSoaringCloud, XiaoGuestMove.JiaotuSuppressEvil],
                [XiaoGuestMove.SuanniSoaringCloud, XiaoGuestMove.ChiwenSwallowRidge, XiaoGuestMove.FervidEmotion, XiaoGuestMove.JiaotuSuppressEvil],
            ]);
    }

    private static void AssertPlanSequence(
        XiaoSpecialGuestMonsterBase monster,
        int capacity,
        IReadOnlyList<IReadOnlyList<XiaoGuestMove>> expectedPlans)
    {
        SetProperty(monster, nameof(XiaoSpecialGuestMonsterBase.IntentCapacity), capacity);
        MethodInfo planMethod = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "PlanNextTurn");
        var rng = new Rng(0x5849414fUL);
        foreach (IReadOnlyList<XiaoGuestMove> expected in expectedPlans)
        {
            planMethod.Invoke(monster, [rng]);
            XiaoGuestMove[] actual =
            [
                (XiaoGuestMove)monster.PlannedMoveOne,
                (XiaoGuestMove)monster.PlannedMoveTwo,
                (XiaoGuestMove)monster.PlannedMoveThree,
                (XiaoGuestMove)monster.PlannedMoveFour,
                (XiaoGuestMove)monster.PlannedMoveFive,
            ];
            XiaoGuestMove[] expectedWithEmptySlots = expected
                .Take(capacity)
                .Concat(Enumerable.Repeat(
                    XiaoGuestMove.None,
                    5 - Math.Min(capacity, expected.Count)))
                .ToArray();
            Require(actual.SequenceEqual(expectedWithEmptySlots),
                $"{monster.GetType().Name} planned [{string.Join(',', actual)}] "
                + $"instead of [{string.Join(',', expectedWithEmptySlots)}].");
        }
    }

    private static void VerifyXiaoIntentContracts()
    {
        XiaoStageOne stageOne = MutableMonster<XiaoStageOne>();
        XiaoEgo stageTwo = MutableMonster<XiaoEgo>();

        MoveIntentContract[] commonContracts =
        [
            new(XiaoGuestMove.LongDrive, typeof(SingleAttackIntent), 6, 8, 1),
            new(XiaoGuestMove.ThroatPierce, typeof(CombinedAttackDebuffIntent), 3, 6, 1),
            new(XiaoGuestMove.Duel, typeof(CombinedAttackDefendIntent), 9, 12, 1, 13),
            new(XiaoGuestMove.FervidEmotion, typeof(CombinedAttackDebuffIntent), 10, 14, 1),
            new(XiaoGuestMove.BlazingDance, typeof(CombinedAttackDebuffIntent), 4, 8, 2),
            new(XiaoGuestMove.DoubleFlank, typeof(CombinedDefendBuffIntent), null, null, 0, 12),
            new(XiaoGuestMove.HotBlood, typeof(CombinedAttackDebuffIntent), 5, 9, 2),
            new(XiaoGuestMove.GreatFlame, typeof(IndiscriminateAttackIntent), 16, 22, 1, 0, true),
            new(XiaoGuestMove.BixueDanxin, typeof(CombinedAttackDefendIntent), 4, 7, 1, 9),
            new(XiaoGuestMove.FlameDragonFist, typeof(CombinedAttackDebuffIntent), 17, 21, 1),
            new(XiaoGuestMove.SkywardFlame, typeof(CombinedAttackDebuffIntent), 9, 11, 1),
            new(XiaoGuestMove.BreakBamboo, typeof(CombinedAttackDebuffIntent), 5, 7, 2),
            new(XiaoGuestMove.JiaotuSuppressEvil, typeof(CombinedDefendDebuffIntent), null, null, 0, 20),
            new(XiaoGuestMove.BianDispute, typeof(CombinedDefendBuffIntent), null, null, 0, 40),
            new(XiaoGuestMove.ChiwenSwallowRidge, typeof(CombinedAttackDebuffIntent), 6, 8, 2),
            new(XiaoGuestMove.YaziVengeance, typeof(IndiscriminateAttackIntent), 11, 14, 1, 0, true),
            new(XiaoGuestMove.SuanniSoaringCloud, typeof(CombinedAttackDebuffIntent), 16, 19, 1),
            new(XiaoGuestMove.TaotieFeast, typeof(IndiscriminateAttackIntent), 19, 22, 1, 0, true),
        ];

        foreach (MoveIntentContract contract in commonContracts)
        {
            AssertIntent(stageOne, contract);
        }
        AssertIntent(
            stageOne,
            new MoveIntentContract(
                XiaoGuestMove.FieryDragonSlash,
                typeof(CombinedAttackDebuffIntent),
                9,
                12,
                1));
        AssertIntent(
            stageTwo,
            new MoveIntentContract(
                XiaoGuestMove.FieryDragonSlash,
                typeof(CombinedAttackDebuffIntent),
                2,
                5,
                3));

        MethodInfo performMove = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "PerformMove");
        MethodInfo attack = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "Attack");
        MethodInfo gainBlock = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "GainBlock");
        MethodInfo getMoveDefinition = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "GetMoveDefinition");
        MethodInfo createIntent = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "CreateIntent");
        MethodInfo resolveAttackDamage = GetMethod(
            typeof(XiaoAttackDefinition),
            nameof(XiaoAttackDefinition.ResolveDamage));
        Require(CountCalls(performMove, getMoveDefinition) == 1
                && CountCalls(createIntent, getMoveDefinition) == 1,
            "Move execution and intent preview do not share the same Xiao move definition.");
        Require(CountCalls(performMove, attack) == 1
                && CountCalls(performMove, gainBlock) == 1
                && CountCalls(performMove, resolveAttackDamage) == 1,
            "Xiao move execution bypasses its data-driven attack/block loops.");
        Require(!GetCalledMethods(attack).Any(static method =>
                    method.DeclaringType == typeof(Rng)
                    && method.Name == nameof(Rng.NextInt)),
            "Xiao damage still treats its ascension pair as an RNG range.");
        Require(typeof(XiaoMoveDefinition).GetProperty(
                    "IntentDamage",
                    InstanceFlags) == null,
            "Xiao still keeps a second independent intent-damage value.");

        XiaoGuestMove[] moves = Enum.GetValues<XiaoGuestMove>()
            .Where(static move => move != XiaoGuestMove.None)
            .ToArray();
        XiaoMoveDefinition[] stageOneDefinitions = moves
            .Select(static move => XiaoMoveDefinitions.Get(move, false))
            .ToArray();
        Require(stageOneDefinitions.Sum(static definition => definition.Attacks.Count)
                    + XiaoMoveDefinitions.Get(
                        XiaoGuestMove.FieryDragonSlash,
                        true).Attacks.Count == 23,
            "The Xiao move catalog no longer contains all 23 attack segments.");
        Require(stageOneDefinitions.Count(static definition => definition.BlockAmount > 0) == 5,
            "The Xiao move catalog no longer contains exactly five block moves.");

        MethodInfo stageTwoPattern = GetMethod(typeof(XiaoEgo), "GetPattern");
        var firstTurn = (IReadOnlyList<XiaoGuestMove>)(
            stageTwoPattern.Invoke(stageTwo, [0, true])
            ?? throw new InvalidOperationException("Stage-two first turn returned null."));
        int attackAnimations = firstTurn
            .Select(move => createIntent.Invoke(stageTwo, [move]))
            .OfType<AttackIntent>()
            .Sum(static intent => intent.Repeats);
        int blockAnimations = firstTurn.Count(static move =>
            move == XiaoGuestMove.JiaotuSuppressEvil);
        Require(attackAnimations == 4 && blockAnimations == 1,
            "Stage-two first turn must resolve four attack animations and one block animation.");

        IReadOnlyList<string> attackStrings = GetStringConstants(attack);
        Require(attackStrings.Contains("vfx/vfx_attack_slash", StringComparer.Ordinal)
                && attackStrings.Contains("vfx/vfx_attack_blunt", StringComparer.Ordinal)
                && !attackStrings.Contains("vfx/vfx_attack_pierce", StringComparer.Ordinal),
            "Xiao attack execution still references the missing base-game pierce VFX.");
    }

    private static void VerifyXiaoPassiveAndNullifyContracts()
    {
        string[] iconPaths = XiaoSpecialGuestAssets.PassiveIcons;
        Require(iconPaths.Contains(
                    "res://images/powers/library_passive_blue.png",
                    StringComparer.Ordinal)
                && iconPaths.Contains(
                    "res://images/powers/library_passive_purple.png",
                    StringComparer.Ordinal)
                && iconPaths.Contains(
                    "res://images/powers/nullify_power.png",
                    StringComparer.Ordinal),
            "Xiao's source-backed passive/status icon set is incomplete.");
        foreach (string path in iconPaths)
        {
            Texture2D? icon = ResourceLoader.Load<Texture2D>(
                path);
            Require(icon != null, "Xiao passive/status icon could not load: " + path);
        }

        AssertPowerIcon<XiaoStarfirePassivePower>("library_passive_orange.png");
        AssertPowerIcon<XiaoEmbraceFirePassivePower>("library_passive_purple.png");
        AssertPowerIcon<XiaoDragonBornPassivePower>("library_passive_orange.png");
        AssertPowerIcon<XiaoPulaoBellPassivePower>("library_passive_orange.png");
        AssertPowerIcon<XiaoReverseScalePassivePower>("library_passive_orange.png");
        AssertPowerIcon<XiaoAmphibiousPassivePower>("library_passive_orange.png");
        AssertPowerIcon<NullifyPower>("nullify_power.png");

        VerifyReverseScalePlayerCounters();

        MethodInfo xiaoResolution = GetMethod(
            typeof(XiaoEgo),
            nameof(XiaoEgo.GetCombatValueResolution));
        Require(xiaoResolution.DeclaringType != typeof(XiaoEgo),
            "Pulao's Power Nullification is still embedded directly in XiaoEgo.");
        MethodInfo pulaoTurnStart = GetMethod(
            typeof(XiaoPulaoBellPassivePower),
            nameof(XiaoPulaoBellPassivePower.BeforeSideTurnStart));
        Require(GetCalledMethods(pulaoTurnStart).Any(method =>
                method is MethodInfo { IsGenericMethod: true } genericMethod
                && genericMethod.GetGenericMethodDefinition().Name
                    == nameof(PowerCmdCompat.Ensure)
                && genericMethod.GetGenericArguments().SequenceEqual([typeof(NullifyPower)])),
            "Pulao no longer applies the reusable NullifyPower.");
        IReadOnlyList<MethodBase> pulaoCalls = GetCalledMethods(pulaoTurnStart);
        Require(pulaoCalls.Any(method => method.Name == "get_PlayerCreatures")
                && pulaoCalls.Any(method => method.Name == "get_Enemies")
                && pulaoCalls.Any(method => method.Name == nameof(Enumerable.Concat))
                && GetIntegerConstants(pulaoTurnStart).Contains(2),
            "Pulao no longer covers both sides on alternating odd rounds.");
        MethodInfo nullifyExpiry = GetMethod(
            typeof(NullifyPower),
            nameof(NullifyPower.AfterSideTurnEndLate));
        Require(GetCalledMethods(nullifyExpiry).Any(method =>
                method.DeclaringType == typeof(PowerCmd)
                && method.Name == nameof(PowerCmd.Remove)),
            "NullifyPower no longer expires at the end of its round.");

        RunState runState = CreateContractRun(
            "SPECIALGUESTNULLIFYVERIFY",
            out _);
        EncounterModel encounter = ModelDb
            .Encounter<XiaoSpecialGuestStageTwoEncounter>()
            .ToMutable();
        var combatState = new CombatState(encounter, runState);
        Player player = runState.Players[0];
        combatState.AddPlayer(player);

        XiaoEgo xiao = MutableMonster<XiaoEgo>();
        Creature xiaoCreature = combatState.CreateCreature(
            xiao,
            CombatSide.Enemy,
            "xiao-nullify");
        combatState.AddCreature(xiaoCreature);
        Creature playerCreature = combatState.PlayerCreatures[0];

        var nullify = (NullifyPower)ModelDb.Power<NullifyPower>().ToMutable();
        nullify.ApplyInternal(xiaoCreature, 1m, silent: true);
        var moveAttack = new LibraryCombatValueContext(
            combatState,
            LibraryCombatValueKind.PhysicalDamage,
            12m,
            playerCreature,
            xiaoCreature,
            ValueProp.Move,
            null,
            null,
            LibraryDamageType.Slash,
            CardPreviewMode.None);
        Require(nullify.GetCombatValueResolution(in moveAttack)
                == LibraryCombatValueResolution.BaseValueAndResistanceOnly,
            "NullifyPower does not preserve the holder's printed move value.");

        var poweredContribution = moveAttack with { Props = 0 };
        Require(nullify.GetCombatValueResolution(in poweredContribution)
                == LibraryCombatValueResolution.PreventValue,
            "NullifyPower does not suppress Power-added dice contributions.");
        var otherDealer = moveAttack with { Dealer = playerCreature };
        Require(nullify.GetCombatValueResolution(in otherDealer)
                == LibraryCombatValueResolution.Default,
            "NullifyPower incorrectly affects another character's attack dice.");
        var block = moveAttack with
        {
            Kind = LibraryCombatValueKind.Block,
            Target = xiaoCreature,
            Dealer = xiaoCreature,
        };
        Require(nullify.GetCombatValueResolution(in block)
                == LibraryCombatValueResolution.BaseValueAndResistanceOnly,
            "NullifyPower does not affect the holder's block dice.");
        var hpLoss = moveAttack with
        {
            Kind = LibraryCombatValueKind.HpLoss,
            Target = xiaoCreature,
        };
        Require(nullify.GetCombatValueResolution(in hpLoss)
                == LibraryCombatValueResolution.Default,
            "NullifyPower incorrectly nullifies HP loss.");
        nullify.RemoveInternal();
    }

    private static void VerifyReverseScalePlayerCounters()
    {
        var reverseScale = (XiaoReverseScalePassivePower)ModelDb
            .Power<XiaoReverseScalePassivePower>()
            .ToMutable();
        SetProperty(
            reverseScale,
            nameof(XiaoReverseScalePassivePower.CardsPlayedByPlayerNetId),
            "101:3;202:7");
        Require(reverseScale.StackType == PowerStackType.Counter
                && reverseScale.GetDisplayCount(101UL) == 3
                && reverseScale.GetDisplayCount(202UL) == 7
                && reverseScale.GetDisplayCount(303UL) == 0
                && reverseScale.GetDisplayCount(null) == 0,
            "Reverse Scale does not display the viewing player's independent card count.");

        int displayRefreshes = 0;
        reverseScale.DisplayAmountChanged += () => displayRefreshes++;
        GetMethod(typeof(XiaoReverseScalePassivePower), "SetCount")
            .Invoke(reverseScale, [101UL, 4]);
        Require(reverseScale.GetDisplayCount(101UL) == 4
                && reverseScale.GetDisplayCount(202UL) == 7
                && displayRefreshes == 1,
            "Reverse Scale did not refresh only the updated player's counter.");

        reverseScale.BeforeSideTurnStart(
                null!,
                CombatSide.Player,
                [],
                null!)
            .GetAwaiter()
            .GetResult();
        Require(reverseScale.GetDisplayCount(101UL) == 0
                && reverseScale.GetDisplayCount(202UL) == 0
                && displayRefreshes == 2,
            "Reverse Scale did not reset and refresh all player counters each round.");
    }

    private static void AssertPowerIcon<T>(string expectedFileName)
        where T : PowerModel
    {
        T power = (T)ModelDb.Power<T>().ToMutable();
        Require(PowerIconResolver.TryResolve(power, out ResolvedPowerIcon resolved),
            $"{typeof(T).Name} has no loadable passive/status icon.");
        string actualPath = resolved.Path;
        Require(actualPath.EndsWith(
                "/" + expectedFileName,
                StringComparison.Ordinal),
            $"{typeof(T).Name} uses '{actualPath}', expected wiki icon '{expectedFileName}'.");
    }

    private static void AssertIntent(
        XiaoSpecialGuestMonsterBase monster,
        MoveIntentContract contract)
    {
        MethodInfo createIntent = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "CreateIntent");
        object intent = createIntent.Invoke(monster, [contract.Move])
            ?? throw new InvalidOperationException($"{contract.Move} returned a null intent.");
        Require(intent.GetType() == contract.IntentType,
            $"{contract.Move} uses {intent.GetType().Name}, expected {contract.IntentType.Name}.");

        Require(contract.LowAscensionDamage.HasValue
                    == contract.HighAscensionDamage.HasValue,
            $"{contract.Move} has an incomplete ascension damage pair.");
        if (contract.LowAscensionDamage is int lowAscensionDamage
            && contract.HighAscensionDamage is int highAscensionDamage)
        {
            PropertyInfo damageCalcProperty = FindProperty(intent.GetType(), "DamageCalc");
            Delegate damageCalc = (Delegate)(damageCalcProperty.GetValue(intent)
                ?? throw new InvalidOperationException($"{contract.Move} has no DamageCalc."));
            decimal damage = Convert.ToDecimal(damageCalc.DynamicInvoke());
            Require(damage == lowAscensionDamage,
                $"{contract.Move} low-ascension preview is {damage}, expected {contract.LowAscensionDamage}.");
            Require(intent is AttackIntent attackIntent && attackIntent.Repeats == contract.Repeats,
                $"{contract.Move} has the wrong attack segment count.");

            XiaoMoveDefinition definition = XiaoMoveDefinitions.Get(
                contract.Move,
                monster is XiaoEgo);
            Require(definition.ResolveIntentDamageForAscension(deadlyEnemies: false)
                        == lowAscensionDamage
                    && definition.ResolveIntentDamageForAscension(deadlyEnemies: true)
                        == highAscensionDamage,
                $"{contract.Move} intent does not resolve its low/high ascension pair.");
            Require(definition.Attacks.All(attack =>
                        attack.ResolveDamageForAscension(deadlyEnemies: false)
                            == lowAscensionDamage
                        && attack.ResolveDamageForAscension(deadlyEnemies: true)
                            == highAscensionDamage),
                $"{contract.Move} execution segments do not match its intent ascension pair.");
        }

        PropertyInfo? blockProperty = intent.GetType().GetProperty(
            "BlockAmount",
            InstanceFlags);
        int actualBlock = blockProperty == null
            ? 0
            : Convert.ToInt32(blockProperty.GetValue(intent));
        Require(actualBlock == contract.Block,
            $"{contract.Move} block is {actualBlock}, expected {contract.Block}.");
        bool isIndiscriminate = intent is IndiscriminateAttackIntent;
        Require(isIndiscriminate == contract.IsIndiscriminate,
            $"{contract.Move} indiscriminate/group classification is incorrect.");
    }

    private static void VerifyEmotionAndScalingContracts()
    {
        FieldInfo thresholdsField = typeof(SpecialGuestMonsterBase).GetField(
                "DefaultEmotionThresholds",
                StaticFlags)
            ?? throw new MissingFieldException(
                typeof(SpecialGuestMonsterBase).FullName,
                "DefaultEmotionThresholds");
        int[] thresholds = (int[])(thresholdsField.GetValue(null)
            ?? throw new InvalidOperationException("EmotionThresholds is null."));
        Require(thresholds.SequenceEqual([3, 3, 5, 7, 9]),
            "Emotion thresholds are not 3/3/5/7/9.");

        MethodInfo scaleThreshold = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "ScaleThreshold");
        MethodInfo genericScale = GetMethod(
            typeof(SpecialGuestMonsterBase),
            "ScaleSpecialGuestAmount");
        Require(CountCalls(scaleThreshold, genericScale) == 1,
            "Xiao thresholds no longer delegate to the reusable special-guest scaler.");
        Require(CallsMethod(
                genericScale,
                typeof(MultiplayerScalingPatchHelper),
                nameof(MultiplayerScalingPatchHelper.ScaleHpAmount)),
            "Guest thresholds no longer use the standard monster HP multiplier helper.");
        Require(CallsMethod(genericScale, typeof(Math), nameof(Math.Ceiling)),
            "Guest thresholds no longer round multiplayer values upward.");

        MethodInfo emotionResolution = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "ResolveEmotionAtPlayerTurnStart");
        IReadOnlyList<int> emotionConstants = GetIntegerConstants(emotionResolution);
        Require(emotionConstants.Contains(6),
            "Emotion resolution no longer checks the six-damage dealt threshold.");
        Require(CountCalls(emotionResolution, genericScale) == 1,
            "Emotion resolution no longer scales the dealt-damage threshold.");
        MethodInfo grantUnits = GetMethod(
            typeof(SpecialGuestMonsterBase),
            "GrantEmotionUnits");
        MethodInfo rewardMethod = GetMethod(
            typeof(XiaoSpecialGuestMonsterBase),
            "ApplyEmotionLevelReward");
        MethodInfo levelUpResolver = GetMethod(
            typeof(SpecialGuestMonsterBase),
            "ResolvePendingEmotionLevelUps");
        Require(CountCalls(emotionResolution, levelUpResolver) == 1,
            "Emotion resolution no longer defers level-ups to player turn start.");
        Require(CountCalls(levelUpResolver, rewardMethod) == 1,
            "Deferred level-up resolution has more than one reward path.");
        Require(CountCalls(grantUnits, rewardMethod) == 0,
            "Real-time unit gains must not level up immediately.");

        MethodInfo damageReceived = GetMethod(
            typeof(SpecialGuestMonsterBase),
            nameof(SpecialGuestMonsterBase.AfterDamageReceived));
        Require(GetIntegerConstants(damageReceived).Contains(1)
                && CountCalls(damageReceived, grantUnits) == 1,
            "A powered player hit no longer grants one emotion unit in real time.");

        XiaoEgo levelFive = MutableMonster<XiaoEgo>();
        SetProperty(levelFive, nameof(XiaoSpecialGuestMonsterBase.EmotionLevel), 5);
        SetProperty(levelFive, nameof(XiaoSpecialGuestMonsterBase.EmotionUnits), 7);
        SetProperty(levelFive, nameof(XiaoSpecialGuestMonsterBase.UnblockedDamageDealtThisRound), 999);
        SetProperty(levelFive, nameof(XiaoSpecialGuestMonsterBase.LevelFiveRoundCounter), 1);
        ((Task)emotionResolution.Invoke(levelFive, null)!).GetAwaiter().GetResult();
        Require(levelFive.UnblockedDamageDealtThisRound == 0,
            "Emotion dealt-damage bucket was not cleared after the complete round.");
        Require(levelFive.EmotionLevel == 5
                && levelFive.EmotionUnits == 9
                && levelFive.LevelFiveRoundCounter == 2,
            "Level-V emotion did not remain locked at its full threshold.");

        MethodInfo fakeDeathClamp = typeof(XiaoStageOne).GetMethods(
                InstanceFlags | BindingFlags.DeclaredOnly)
            .Single(method =>
                method.Name == nameof(XiaoStageOne.ModifyHpLostAfterOsty)
                && method.GetParameters().Length == 6);
        Require(GetIntegerConstants(fakeDeathClamp).Contains(100)
                && CountCalls(fakeDeathClamp, scaleThreshold) == 1,
            "Stage-one fake death no longer uses the scaled 100-HP threshold.");
        MethodInfo reverseScale = GetMethod(
            typeof(XiaoEgo),
            nameof(XiaoEgo.AfterSideTurnEndLate));
        Require(GetIntegerConstants(reverseScale).Contains(140)
                && CountCalls(reverseScale, scaleThreshold) == 1,
            "Reverse Scale no longer removes a scaled 140 confusion resistance.");

        EncounterModel encounter = ModelDb.Encounter<XiaoSpecialGuestStageOneEncounter>();
        foreach (decimal baseAmount in new[] { 6m, 25m, 100m, 140m, 392m })
        {
            decimal previous = baseAmount;
            for (int playerCount = 1; playerCount <= 4; playerCount++)
            {
                decimal scaled = Creature.ScaleHpForMultiplayer(
                    baseAmount,
                    encounter,
                    playerCount,
                    actIndex: 0);
                decimal expected = playerCount == 1
                    ? baseAmount
                    : baseAmount * playerCount * 1.1m;
                Require(scaled == expected,
                    $"Standard scaling produced {scaled} instead of {expected} "
                    + $"for {baseAmount} with {playerCount} players.");
                Require(scaled >= previous,
                    $"Standard scaling decreased {baseAmount} at {playerCount} players.");
                Require(Math.Ceiling(scaled) == decimal.Ceiling(expected),
                    "Scaled threshold did not use exact upward rounding.");
                previous = scaled;
            }
        }
    }

    private static void VerifyXiaoTurnTimingContracts()
    {
        MethodInfo unlock = GetMethod(
            typeof(XiaoSpecialGuestRegistration),
            "IsUnlocked");
        Require(GetCalledMethods(unlock).Any(static method =>
                    method.DeclaringType
                        == typeof(SpecialGuestReceptionProgress)
                    && method.Name
                        == nameof(SpecialGuestReceptionProgress
                            .WasSuccessfullyReceived)),
            "Xiao's unlock predicate does not query successful Kali reception progress.");
        Require(XiaoSpecialGuestRegistration.MeetsUnlockContract(
                    2,
                    [10],
                    kaliSuccessfullyReceived: true)
                && !XiaoSpecialGuestRegistration.MeetsUnlockContract(
                    2,
                    [10],
                    kaliSuccessfullyReceived: false),
            "Xiao is not restricted to Act 3 after a successful Kali reception.");

        SpecialGuestDefinition definition = SpecialGuestRegistry.Get(
            XiaoSpecialGuestIds.Guest);
        RunState availabilityRun = CreateContractRun(
            "XIAOACTAVAILABILITYVERIFY",
            out _);
        availabilityRun.ActFloor = SpecialGuestDefinition.MinimumActFloor;
        Require(!definition.CanAppear(availabilityRun),
            "Xiao can still appear before act three.");
        availabilityRun.CurrentActIndex = 2;
        availabilityRun.ActFloor = SpecialGuestDefinition.MinimumActFloor - 1;
        Require(!definition.CanAppear(availabilityRun),
            "Xiao can appear before ActFloor 4.");
        availabilityRun.ActFloor = SpecialGuestDefinition.MinimumActFloor;
        Require(definition.CanAppear(availabilityRun),
            "Xiao cannot appear in act three.");
        availabilityRun.ActFloor = availabilityRun.Act.GetNumberOfRooms(
            availabilityRun.Players.Count > 1) + 2;
        Require(!definition.CanAppear(availabilityRun),
            "Xiao can appear after the final normal ActFloor.");
        MethodInfo replacement = GetMethod(
            typeof(SpecialGuestRegistry),
            nameof(SpecialGuestRegistry.TryReplaceNextEvent));
        MethodInfo availabilityFilter = GetMethod(
            typeof(SpecialGuestRegistry),
            "CanAppear");
        Require(CountCalls(replacement, availabilityFilter) == 2,
            "New or persisted special-guest replacements ignore runtime availability.");

        Require(XiaoStarfireStatusPower.CalculateSpreadStacks(9) == 0
                && XiaoStarfireStatusPower.CalculateSpreadStacks(10) == 1
                && XiaoStarfireStatusPower.CalculateSpreadStacks(59) == 5,
            "Starfire no longer spreads ten percent of the owner's Burn.");

        MethodInfo starfireTurnEnd = GetMethod(
            typeof(XiaoStarfireStatusPower),
            "AfterSideTurnEnd");
        Require(starfireTurnEnd.DeclaringType
                == typeof(XiaoStarfireStatusPower)
                && GetCalledMethods(starfireTurnEnd).Any(static method =>
                    method.Name == "set_SkipNextDurationTick"),
            "Starfire's normal duration tick is not deferred past Burn resolution.");
        MethodInfo starfireLateTurnEnd = GetMethod(
            typeof(XiaoStarfireStatusPower),
            nameof(XiaoStarfireStatusPower.AfterSideTurnEndLate));
        Require(starfireLateTurnEnd.DeclaringType
                == typeof(XiaoStarfireStatusPower),
            "Starfire duration is not deferred until after normal turn-end Burn resolution.");

        XiaoEgo emotionGuest = MutableMonster<XiaoEgo>();
        MethodInfo grantEmotionUnits = GetMethod(
            typeof(SpecialGuestMonsterBase),
            "GrantEmotionUnits");
        grantEmotionUnits.Invoke(emotionGuest, [99]);
        Require(emotionGuest.EmotionUnits == 3,
            "Enemy emotion units are not locked when the current level threshold is full.");

        MethodInfo advanceEmotion = GetMethod(
            typeof(SpecialGuestMonsterBase),
            "TryAdvanceOneEmotionLevel");
        SetProperty(emotionGuest, nameof(SpecialGuestMonsterBase.EmotionUnits), 99);
        object?[] advanceArgs = [0];
        bool advanced = (bool)(advanceEmotion.Invoke(emotionGuest, advanceArgs)
            ?? false);
        Require(advanced
                && emotionGuest.EmotionLevel == 1
                && emotionGuest.EmotionUnits == 0
                && advanceArgs[0] is 1,
            "A player-turn start did not advance exactly one enemy emotion level.");
        SetProperty(emotionGuest, nameof(SpecialGuestMonsterBase.EmotionUnits), 99);
        advanceArgs[0] = 0;
        advanced = (bool)(advanceEmotion.Invoke(emotionGuest, advanceArgs)
            ?? false);
        Require(advanced
                && emotionGuest.EmotionLevel == 2
                && emotionGuest.EmotionUnits == 0
                && advanceArgs[0] is 2,
            "Enemy emotion advancement skipped or chained levels.");
    }

    private static void VerifySpecialGuestEscapePenaltyContract()
    {
        MethodInfo resolvePenalty = GetMethod(
            typeof(SpecialGuestEventBase),
            "ResolveEscapeMaxHpLoss");
        IReadOnlyList<int> constants = GetIntegerConstants(resolvePenalty);
        Require(constants.Contains(8)
                && constants.Contains(100)
                && CallsMethod(resolvePenalty, typeof(decimal), nameof(decimal.Ceiling)),
            "Special-guest escape penalty is no longer 8% of max HP rounded up.");

        MethodInfo escape = GetMethod(typeof(SpecialGuestEventBase), "Escape");
        Require(CallsMethod(escape, typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp)),
            "Escaping a special guest no longer actually removes max HP.");
        MethodInfo options = GetMethod(
            typeof(SpecialGuestEventBase),
            "GenerateInitialOptions");
        Require(CallsMethod(
                options,
                typeof(EventOption),
                nameof(EventOption.ThatDecreasesMaxHp)),
            "The escape option no longer exposes its max-HP penalty warning.");
    }

    private static void VerifySpecialGuestSaveResumeContract()
    {
        EventRoom sourceRoom = new(ModelDb.Event<XiaoSpecialGuestEvent>());
        SerializableRoom serializedRoom = sourceRoom.ToSerializable();
        AbstractRoom? restoredRoom = AbstractRoom.FromSerializable(serializedRoom, null);
        Require(serializedRoom.RoomType == RoomType.Event
                && serializedRoom.EventId == sourceRoom.CanonicalEvent.Id
                && restoredRoom is EventRoom restoredEventRoom
                && restoredEventRoom.CanonicalEvent.Id == sourceRoom.CanonicalEvent.Id
                && !restoredEventRoom.IsPreFinished,
            "An active Xiao event room no longer survives the native room save round-trip.");

        MethodInfo afterEventStarted = GetMethod(
            typeof(SpecialGuestEventBase),
            nameof(SpecialGuestEventBase.AfterEventStarted));
        Require(CallsMethod(
                afterEventStarted,
                typeof(SpecialGuestRoomPersistence),
                "SaveCurrentEventRoomAsync"),
            "The post-entry special-guest checkpoint does not preserve the current EventRoom.");

        MethodInfo saveCurrentRoom = GetMethod(
            typeof(SpecialGuestRoomPersistence),
            "SaveCurrentEventRoomAsync");
        MethodInfo resolveCurrentRoom = GetMethod(
            typeof(SpecialGuestRoomPersistence),
            "TryGetCurrentEventRoom");
        Require(GetCalledMethods(resolveCurrentRoom).Any(static method =>
                    method.Name == "get_CurrentRoom")
                && CallsMethod(saveCurrentRoom, typeof(SaveManager), nameof(SaveManager.SaveRun)),
            "The special-guest checkpoint no longer serializes the exact current room.");

        MethodInfo saveFinishedRoom = GetMethod(
            typeof(SpecialGuestRoomPersistence),
            "SaveFinishedCurrentEventRoomAsync");
        Require(CallsMethod(
                    saveFinishedRoom,
                    typeof(EventRoom),
                    nameof(EventRoom.MarkPreFinished))
                && CallsMethod(
                    saveFinishedRoom,
                    typeof(SaveManager),
                    nameof(SaveManager.SaveRun)),
            "A completed special-guest event is not saved as a finished EventRoom.");

        MethodInfo replaceEvent = GetMethod(
            typeof(SpecialGuestRegistry),
            nameof(SpecialGuestRegistry.TryReplaceNextEvent));
        Require(!GetCalledMethods(replaceEvent).Any(static method =>
                    method.Name == "ScheduleCheckpointSave"),
            "Special-guest selection still races a pre-entry SaveRun(null) checkpoint.");

        var legacySave = new SerializableRun
        {
            CurrentActIndex = 0,
            VisitedMapCoords = [new MapCoord(3, 0)],
            MapPointHistory =
            [
                [
                    new MapPointHistoryEntry(),
                    new MapPointHistoryEntry
                    {
                        MapPointType = MapPointType.Unknown,
                        Rooms =
                        [
                            new MapPointRoomHistoryEntry
                            {
                                RoomType = RoomType.Event,
                                ModelId = sourceRoom.CanonicalEvent.Id,
                            },
                        ],
                    },
                ],
            ],
        };
        Require(SpecialGuestRoomPersistence.RepairMissingActiveEventRoom(legacySave)
                && legacySave.PreFinishedRoom is
                {
                    RoomType: RoomType.Event,
                    EventId: { } repairedEventId,
                    IsPreFinished: false,
                }
                && repairedEventId == sourceRoom.CanonicalEvent.Id,
            "A legacy post-entry save cannot recover its Xiao event from map history.");

        var nativePreEntrySave = new SerializableRun
        {
            CurrentActIndex = 0,
            VisitedMapCoords = [new MapCoord(3, 0), new MapCoord(4, 1)],
            MapPointHistory = legacySave.MapPointHistory,
        };
        Require(!SpecialGuestRoomPersistence.RepairMissingActiveEventRoom(nativePreEntrySave)
                && nativePreEntrySave.PreFinishedRoom == null,
            "Legacy recovery incorrectly revived a prior Xiao event at the next map coord.");
    }

    private static void VerifySpecialGuestStorySkipContract()
    {
        Require(Math.Abs(SpecialGuestStoryOverlay.LongPressSeconds - 2d) < 0.001d,
            "Special-guest story skip must require a two-second hold.");
        Require(SpecialGuestStoryOverlay.SkipHintPosition
                    == new Vector2(1090f, 1022f)
                && SpecialGuestStoryOverlay.SkipHintSize
                    == new Vector2(470f, 44f),
            "The story skip hint is no longer in the dialogue box's bottom-right corner.");

        MethodInfo buildUi = GetMethod(typeof(SpecialGuestStoryOverlay), "BuildUi");
        MethodInfo applyFont = GetMethod(typeof(SpecialGuestStoryOverlay), "ApplyFont");
        Require(CountCalls(buildUi, applyFont) == 3,
            "The story hint no longer shares the dialogue font setup.");

        MethodInfo isConfirmInput = GetMethod(
            typeof(SpecialGuestStoryOverlay),
            "IsConfirmInput");
        FieldInfo confirmInput = typeof(MegaInput).GetField(
                nameof(MegaInput.confirm),
                BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(MegaInput).FullName, nameof(MegaInput.confirm));
        Require(GetReferencedFields(isConfirmInput).Count(field =>
                    field.Module == confirmInput.Module
                    && field.MetadataToken == confirmInput.MetadataToken) == 2,
            "Keyboard/controller story confirm no longer uses MegaInput.confirm.");
        Require(!GetStringConstants(isConfirmInput).Contains("proceed", StringComparer.Ordinal),
            "Story confirm still queries the nonexistent proceed InputMap action.");
        object?[] leftMouseArgs =
        [
            new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
            },
            false,
            false,
            false,
        ];
        bool acceptsLeftMouse = (bool)(isConfirmInput.Invoke(null, leftMouseArgs)
            ?? false);
        Require(acceptsLeftMouse && leftMouseArgs[3] is true,
            "Holding the left mouse button no longer arms story skipping.");

        object?[] touchArgs =
        [
            new InputEventScreenTouch { Pressed = true },
            false,
            false,
            false,
        ];
        bool acceptsTouch = (bool)(isConfirmInput.Invoke(null, touchArgs) ?? false);
        Require(acceptsTouch && touchArgs[3] is true,
            "Holding a touch confirm no longer arms story skipping.");
    }

    private static void VerifySupportLibraryAttackTargets()
    {
        FieldInfo singleTargetField = typeof(LibraryAttackCommand).GetField(
                "_singleTarget",
                InstanceFlags)
            ?? throw new MissingFieldException(
                typeof(LibraryAttackCommand).FullName,
                "_singleTarget");
        Require(singleTargetField.FieldType == typeof(Creature),
            "LibraryAttackCommand._singleTarget cannot retain a base-game player Creature.");

        RunState runState = CreateContractRun(
            "SPECIALGUESTATTACKTARGETVERIFY",
            out _,
            playerCount: 2);
        EncounterModel encounter = ModelDb
            .Encounter<XiaoSpecialGuestStageOneEncounter>()
            .ToMutable();
        var combatState = new CombatState(encounter, runState);
        foreach (Player player in runState.Players)
        {
            combatState.AddPlayer(player);
        }

        XiaoStageOne xiao = MutableMonster<XiaoStageOne>();
        Creature xiaoCreature = combatState.CreateCreature(
            xiao,
            CombatSide.Enemy,
            "xiao");
        combatState.AddCreature(xiaoCreature);

        var singleTargetCommand = new LibraryAttackCommand(1m)
            .Targeting(combatState.PlayerCreatures[0]);
        Require(ReferenceEquals(
                singleTargetField.GetValue(singleTargetCommand),
                combatState.PlayerCreatures[0]),
            "LibraryAttackCommand.Targeting discarded a player Creature.");

        var groupCommand = new LibraryAttackCommand(1m).FromMonster(xiao);
        MethodInfo getPossibleTargets = GetMethod(
            typeof(LibraryAttackCommand),
            "GetPossibleTargets");
        var retainedTargets = (IReadOnlyList<Creature>)(
            getPossibleTargets.Invoke(groupCommand, null)
            ?? throw new InvalidOperationException(
                "LibraryAttackCommand.GetPossibleTargets returned null."));
        Require(retainedTargets.Count == 2
                && retainedTargets.All(static target => target is { IsPlayer: true })
                && retainedTargets.SequenceEqual(combatState.PlayerCreatures),
            "FromMonster multi-targeting did not retain every non-null player Creature.");
    }

    private static RunState CreateContractRun(
        string seed,
        out SpecialGuestRunStateModifier state,
        int playerCount = 1)
    {
        state = (SpecialGuestRunStateModifier)ModelDb
            .Modifier<SpecialGuestRunStateModifier>()
            .ToMutable();
        Player[] players = Enumerable.Range(0, playerCount)
            .Select(index => Player.CreateForNewRun(
                ModelDb.Character<Ironclad>(),
                SaveManager.Instance.GenerateUnlockStateFromProgress(),
                0x5849414fUL + (ulong)index))
            .ToArray();
        return RunState.CreateForNewRun(
            players,
            ActModel.GetDefaultList()
                .Select(static act => act.ToMutable())
                .ToList(),
            [state],
            GameMode.Standard,
            ascensionLevel: 0,
            seed);
    }

    private static string ExpectedRollKey(
        IRunState runState,
        EventModel originalEvent)
    {
        string coord = runState.CurrentMapCoord is { } mapCoord
            ? mapCoord.col + "," + mapCoord.row
            : "none";
        return runState.Rng.Seed
               + "|act=" + runState.CurrentActIndex
               + "|coord=" + coord
               + "|event=" + originalEvent.Id;
    }

    private static ulong IndependentStableHash64(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (char character in value)
        {
            hash = (hash ^ (byte)character) * prime;
            hash = (hash ^ (byte)(character >> 8)) * prime;
        }
        return hash;
    }

    private static T MutableMonster<T>() where T : MonsterModel =>
        (T)ModelDb.Monster<T>().ToMutable();

    private static void AssertResistance(
        LibraryCreatureResistanceData.Resistance? resistance,
        LibraryResistanceLevel slash,
        LibraryResistanceLevel pierce,
        LibraryResistanceLevel blunt,
        string label)
    {
        Require(resistance is not null
                && resistance.Slash == slash
                && resistance.Pierce == pierce
                && resistance.Blunt == blunt,
            label + " resistance is incorrect.");
    }

    // 怪物与能力状态不进存档也不同步；这里守住它们不再带 [SavedProperty]，并且在套件的读档模拟里。
    private static void AssertCombatStateProperty(Type declaringType, string propertyName)
    {
        _ = declaringType.GetProperty(
                propertyName,
                InstanceFlags)
            ?? throw new MissingMemberException(declaringType.FullName, propertyName);
        Require(CombatStateProperties.IsListed(declaringType, propertyName),
            declaringType.Name + "." + propertyName + " is a SavedProperty again, or missing from the reload list.");
    }

    private static void AssertHpGetterConstants<T>(
        string propertyName,
        int baseValue,
        int toughValue)
        where T : MonsterModel
    {
        MethodInfo getter = typeof(T).GetProperty(propertyName, InstanceFlags)?.GetMethod
            ?? throw new MissingMethodException(typeof(T).FullName, "get_" + propertyName);
        IReadOnlyList<int> constants = GetIntegerConstants(getter);
        Require(constants.Contains(baseValue) && constants.Contains(toughValue),
            $"{typeof(T).Name}.{propertyName} no longer contains {baseValue}/{toughValue}.");
    }

    private static T GetProperty<T>(object instance, string propertyName)
    {
        PropertyInfo property = FindProperty(instance.GetType(), propertyName);
        return (T)(property.GetValue(instance)
            ?? throw new InvalidOperationException(
                instance.GetType().Name + "." + propertyName + " returned null."));
    }

    private static void SetProperty<T>(object instance, string propertyName, T value)
    {
        PropertyInfo property = FindProperty(instance.GetType(), propertyName);
        MethodInfo setter = property.SetMethod
            ?? property.GetSetMethod(nonPublic: true)
            ?? throw new MissingMethodException(
                instance.GetType().FullName,
                "set_" + propertyName);
        setter.Invoke(instance, [value]);
    }

    private static PropertyInfo FindProperty(Type type, string propertyName)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            PropertyInfo? property = current.GetProperty(
                propertyName,
                InstanceFlags | BindingFlags.DeclaredOnly);
            if (property != null)
            {
                return property;
            }
        }
        throw new MissingMemberException(type.FullName, propertyName);
    }

    private static MethodInfo GetMethod(Type type, string methodName)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            MethodInfo? method = current.GetMethod(
                methodName,
                InstanceFlags | StaticFlags | BindingFlags.DeclaredOnly);
            if (method != null)
            {
                return method;
            }
        }
        throw new MissingMethodException(type.FullName, methodName);
    }

    private static bool CallsMethod(
        MethodInfo caller,
        Type declaringType,
        string methodName) =>
        GetCalledMethods(caller).Any(method =>
            method.DeclaringType == declaringType && method.Name == methodName);

    private static int CountCalls(MethodInfo caller, MethodInfo target) =>
        GetCalledMethods(caller).Count(method =>
            method.Module == target.Module
            && method.MetadataToken == target.MetadataToken);

    private static IReadOnlyList<MethodBase> GetCalledMethods(MethodInfo method) =>
        ReadInstructions(GetImplementationMethod(method))
            .Where(static instruction =>
                instruction.OpCode == OpCodes.Call
                || instruction.OpCode == OpCodes.Callvirt
                || instruction.OpCode == OpCodes.Newobj)
            .Select(static instruction => instruction.Operand)
            .OfType<MethodBase>()
            .ToArray();

    private static IReadOnlyList<FieldInfo> GetReferencedFields(MethodInfo method) =>
        ReadInstructions(GetImplementationMethod(method))
            .Select(static instruction => instruction.Operand)
            .OfType<FieldInfo>()
            .ToArray();

    private static IReadOnlyList<int> GetIntegerConstants(MethodInfo method)
    {
        var result = new List<int>();
        foreach (IlInstruction instruction in ReadInstructions(
                     GetImplementationMethod(method)))
        {
            if (instruction.OpCode == OpCodes.Ldc_I4_M1) result.Add(-1);
            else if (instruction.OpCode == OpCodes.Ldc_I4_0) result.Add(0);
            else if (instruction.OpCode == OpCodes.Ldc_I4_1) result.Add(1);
            else if (instruction.OpCode == OpCodes.Ldc_I4_2) result.Add(2);
            else if (instruction.OpCode == OpCodes.Ldc_I4_3) result.Add(3);
            else if (instruction.OpCode == OpCodes.Ldc_I4_4) result.Add(4);
            else if (instruction.OpCode == OpCodes.Ldc_I4_5) result.Add(5);
            else if (instruction.OpCode == OpCodes.Ldc_I4_6) result.Add(6);
            else if (instruction.OpCode == OpCodes.Ldc_I4_7) result.Add(7);
            else if (instruction.OpCode == OpCodes.Ldc_I4_8) result.Add(8);
            else if (instruction.OpCode == OpCodes.Ldc_I4
                     || instruction.OpCode == OpCodes.Ldc_I4_S)
            {
                result.Add(Convert.ToInt32(instruction.Operand));
            }
        }
        return result;
    }

    private static IReadOnlyList<string> GetStringConstants(MethodInfo method) =>
        ReadInstructions(GetImplementationMethod(method))
            .Select(static instruction => instruction.Operand)
            .OfType<string>()
            .ToArray();

    private static MethodInfo GetImplementationMethod(MethodInfo method)
    {
        AsyncStateMachineAttribute? asyncAttribute =
            method.GetCustomAttribute<AsyncStateMachineAttribute>();
        return asyncAttribute?.StateMachineType.GetMethod("MoveNext", InstanceFlags)
               ?? method;
    }

    private static IReadOnlyList<IlInstruction> ReadInstructions(MethodInfo method)
    {
        byte[] bytes = method.GetMethodBody()?.GetILAsByteArray()
            ?? throw new InvalidOperationException(method.Name + " has no IL body.");
        Type[]? typeArguments = method.DeclaringType?.GetGenericArguments();
        Type[]? methodArguments = method.GetGenericArguments();
        var instructions = new List<IlInstruction>();
        int position = 0;
        while (position < bytes.Length)
        {
            OpCode opCode;
            byte first = bytes[position++];
            if (first == 0xfe)
            {
                opCode = DoubleByteOpCodes[bytes[position++]];
            }
            else
            {
                opCode = SingleByteOpCodes[first];
            }

            object? operand = opCode.OperandType switch
            {
                OperandType.InlineNone => null,
                OperandType.ShortInlineI => (sbyte)bytes[position++],
                OperandType.InlineI => ReadInt32(bytes, ref position),
                OperandType.InlineI8 => ReadInt64(bytes, ref position),
                OperandType.ShortInlineR => ReadSingle(bytes, ref position),
                OperandType.InlineR => ReadDouble(bytes, ref position),
                OperandType.ShortInlineBrTarget => (sbyte)bytes[position++],
                OperandType.InlineBrTarget => ReadInt32(bytes, ref position),
                OperandType.InlineSwitch => ReadSwitch(bytes, ref position),
                OperandType.ShortInlineVar => bytes[position++],
                OperandType.InlineVar => ReadUInt16(bytes, ref position),
                OperandType.InlineString => ResolveString(
                    method.Module,
                    ReadInt32(bytes, ref position)),
                OperandType.InlineField => ResolveMember(
                    method.Module,
                    ReadInt32(bytes, ref position),
                    typeArguments,
                    methodArguments,
                    MemberTypes.Field),
                OperandType.InlineMethod => ResolveMember(
                    method.Module,
                    ReadInt32(bytes, ref position),
                    typeArguments,
                    methodArguments,
                    MemberTypes.Method),
                OperandType.InlineType => ResolveMember(
                    method.Module,
                    ReadInt32(bytes, ref position),
                    typeArguments,
                    methodArguments,
                    MemberTypes.TypeInfo),
                OperandType.InlineTok => ResolveMember(
                    method.Module,
                    ReadInt32(bytes, ref position),
                    typeArguments,
                    methodArguments,
                    null),
                OperandType.InlineSig => ReadInt32(bytes, ref position),
                _ => throw new NotSupportedException(
                    "Unsupported IL operand type " + opCode.OperandType + "."),
            };
            instructions.Add(new IlInstruction(opCode, operand));
        }
        return instructions;
    }

    private static object? ResolveMember(
        Module module,
        int token,
        Type[]? typeArguments,
        Type[]? methodArguments,
        MemberTypes? expectedType)
    {
        try
        {
            MemberInfo? member = module.ResolveMember(token, typeArguments, methodArguments);
            if (member == null)
            {
                return token;
            }
            return expectedType == null || member.MemberType == expectedType
                ? member
                : member;
        }
        catch (ArgumentException)
        {
            return token;
        }
    }

    private static object ResolveString(Module module, int token)
    {
        try
        {
            return module.ResolveString(token);
        }
        catch (ArgumentException)
        {
            return token;
        }
    }

    private static int[] ReadSwitch(byte[] bytes, ref int position)
    {
        int count = ReadInt32(bytes, ref position);
        var values = new int[count];
        for (int index = 0; index < count; index++)
        {
            values[index] = ReadInt32(bytes, ref position);
        }
        return values;
    }

    private static ushort ReadUInt16(byte[] bytes, ref int position)
    {
        ushort value = BitConverter.ToUInt16(bytes, position);
        position += sizeof(ushort);
        return value;
    }

    private static int ReadInt32(byte[] bytes, ref int position)
    {
        int value = BitConverter.ToInt32(bytes, position);
        position += sizeof(int);
        return value;
    }

    private static long ReadInt64(byte[] bytes, ref int position)
    {
        long value = BitConverter.ToInt64(bytes, position);
        position += sizeof(long);
        return value;
    }

    private static float ReadSingle(byte[] bytes, ref int position)
    {
        float value = BitConverter.ToSingle(bytes, position);
        position += sizeof(float);
        return value;
    }

    private static double ReadDouble(byte[] bytes, ref int position)
    {
        double value = BitConverter.ToDouble(bytes, position);
        position += sizeof(double);
        return value;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record MoveIntentContract(
        XiaoGuestMove Move,
        Type IntentType,
        int? LowAscensionDamage,
        int? HighAscensionDamage,
        int Repeats,
        int Block = 0,
        bool IsIndiscriminate = false);

    private readonly record struct IlInstruction(OpCode OpCode, object? Operand);
}

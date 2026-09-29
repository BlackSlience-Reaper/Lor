using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.specialguests;
using LibraryOfRuina.content.specialguests.Kali;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class KaliSpecialGuestContractVerificationPatch
{
    private const string VerifyArg = "lor-verify-kali-special-guest";
    private const string BloodMistVerifyArg = "lor-verify-kali-blood-mist";
    private const string TurnContractVerifyArg = "lor-verify-kali-turn-contract";
    private const string LogPrefix = "[LibraryOfRuina.KaliSpecialGuest.Verify] ";
    private const BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static bool _started;

    internal static void Start()
    {
        bool verifyAll = HasVerifyArg(VerifyArg);
        bool verifyBloodMist = HasVerifyArg(BloodMistVerifyArg);
        bool verifyTurnContract = HasVerifyArg(TurnContractVerifyArg);
        if (_started || (!verifyAll && !verifyBloodMist && !verifyTurnContract))
        {
            return;
        }

        _started = true;
        Callable.From(
            verifyBloodMist
                ? RunBloodMist
                : verifyTurnContract
                    ? RunTurnContract
                    : Run).CallDeferred();
    }

    private static bool HasVerifyArg(string verifyArg) =>
        CommandLineHelper.HasArg(verifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            verifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static void RunBloodMist()
    {
        try
        {
            VerifyKaliSceneContract();
            VerifyBloodMistSettlementDelay();
            Log.Info(LogPrefix + "KALI_BLOOD_MIST_CONTRACT_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "KALI_BLOOD_MIST_CONTRACT_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void RunTurnContract()
    {
        try
        {
            VerifyKaliTurnContract();
            Log.Info(LogPrefix + "KALI_TURN_CONTRACT_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "KALI_TURN_CONTRACT_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void Run()
    {
        try
        {
            VerifyRegistrationAndStage();
            VerifyUnlockContract();
            VerifyKaliCombatContract();
            VerifyVisualContract();
            Log.Info(LogPrefix + "KALI_SPECIAL_GUEST_CONTRACT_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "KALI_SPECIAL_GUEST_CONTRACT_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyRegistrationAndStage()
    {
        KaliSpecialGuestRegistration.Initialize();
        Require(SpecialGuestRegistry.TryGet(
                KaliSpecialGuestIds.Guest,
                out SpecialGuestDefinition definition),
            "Kali is not registered as a special guest.");
        Require(definition.Stages.Count == 1,
            $"Kali registered {definition.Stages.Count} stages instead of one.");

        SpecialGuestStageDefinition stage = definition.Stages[0];
        Require(stage.BeforeCombatStory == null && stage.AfterVictoryStory == null,
            "Kali special guest unexpectedly registered a story sequence.");
        Require(stage.GetAssetPaths().Contains(
                KaliSpecialGuestIds.EventImage,
                StringComparer.Ordinal),
            "Kali event image is absent from special-guest preloading.");

        EventModel eventModel = definition.EventFactory();
        Require(eventModel is KaliSpecialGuestEvent
                && eventModel.Id.Entry == KaliSpecialGuestIds.Event,
            "Kali special-guest event factory or ID is incorrect.");
        eventModel.CalculateVars();
        Require(eventModel.DynamicVars[SpecialGuestEventBase.ReceptionStageCountVar].IntValue
                == definition.Stages.Count,
            "Kali's event option does not display its registered stage count.");
        Require(SpecialGuestEventBase.ResolveReceptionStageCount(1) == 1
                && SpecialGuestEventBase.ResolveReceptionStageCount(2) == 2
                && SpecialGuestEventBase.ResolveReceptionStageCount(4) == 4,
            "Special-guest stage count is hard-coded instead of definition-driven.");

        EncounterModel encounter = stage.EncounterFactory();
        Require(encounter is KaliSpecialGuestEncounter reception
                && reception.SpecialGuestId == KaliSpecialGuestIds.Guest
                && reception.SpecialGuestStageIndex == 0,
            "Kali stage metadata is incorrect.");
        Require(encounter.Id.Entry == KaliSpecialGuestIds.Encounter,
            "Kali special-guest encounter ID is incorrect.");
        Require(encounter.RoomType == RoomType.Elite
                && encounter.ShouldGiveRewards
                && encounter.HasScene,
            "Kali stage is not a reward-bearing elite scene.");
        Require(encounter.Slots.SequenceEqual(["kali"]),
            "Kali stage must contain exactly the kali scene slot.");
        Require(encounter.AllPossibleMonsters.Single() is Kali,
            "Kali stage does not reuse the canonical Kali monster.");

        bool hasCustomBackground = (bool)(typeof(KaliSpecialGuestEncounter)
            .GetProperty("HasCustomBackground", InstanceFlags)
            ?.GetValue(encounter)
            ?? false);
        Require(hasCustomBackground,
            "Kali stage did not retain the custom Language-floor background.");

        string[] assets = encounter.ExtraAssetPaths.ToArray();
        Require(assets.Contains(KaliSpecialGuestIds.EncounterScene, StringComparer.Ordinal),
            "Kali encounter scene is absent from preloading.");
        Require(Kali.StaticAssetPaths.All(path => assets.Contains(path, StringComparer.Ordinal)),
            "One or more canonical Kali assets were not migrated.");
        Require(ResourceLoader.Exists(KaliSpecialGuestIds.EventImage)
                && ResourceLoader.Exists(KaliSpecialGuestIds.EncounterScene),
            "Kali event image or encounter scene cannot be loaded.");
    }

    private static void VerifyUnlockContract()
    {
        Require(KaliSpecialGuestRegistration.MeetsUnlockContract(1, [6], true),
            "Act 2 / six pages / full first-act floor liberation did not unlock Kali.");
        Require(KaliSpecialGuestRegistration.MeetsUnlockContract(1, [6, 9], true),
            "A valid multiplayer party did not unlock Kali.");
        Require(!KaliSpecialGuestRegistration.MeetsUnlockContract(0, [6], true)
                && !KaliSpecialGuestRegistration.MeetsUnlockContract(2, [6], true),
            "Kali can appear outside Act 2.");
        Require(!KaliSpecialGuestRegistration.MeetsUnlockContract(1, [5], true)
                && !KaliSpecialGuestRegistration.MeetsUnlockContract(1, [6, 5], true),
            "Kali unlocked without every player owning six page relics.");
        Require(!KaliSpecialGuestRegistration.MeetsUnlockContract(1, [6], false),
            "Kali unlocked without a fully liberated first-act floor.");
        Require(LiberationFloorIds.FirstAct.SequenceEqual(
                [
                    LiberationFloorIds.History,
                    LiberationFloorIds.Technology,
                    LiberationFloorIds.Literature
                ]),
            "The public first-act liberation set does not include History, Technology, and Literature.");
    }

    private static void VerifyKaliCombatContract()
    {
        Require(typeof(Kali).IsSubclassOf(typeof(SpecialGuestMonsterBase)),
            "Kali does not inherit the reusable special-guest combat contract.");
        var kali = (Kali)ModelDb.Monster<Kali>().ToMutable();
        Require(kali.EmotionUnitThresholds.SequenceEqual([3, 3, 5, 7, 9]),
            "Kali emotion thresholds changed from the five-level contract.");
        VerifyKaliTurnContract();
        Require(Kali.ResolveScaledEgoHpThreshold(null) == Kali.EgoHpThreshold,
            "Kali's legacy null-creature threshold fallback changed.");
        Require(Kali.ResolveMinimumDirectDamagePerRound(null, null)
                == Kali.MinimumDirectDamagePerRound,
            "Kali's solo minimum-damage threshold changed during multiplayer scaling.");
        Require(Kali.FocusBreathStrongDurationTurns == 1,
            "Focus Breath strong is not configured for exactly one turn.");
        Require(Kali.FocusBreathBlock == 12,
            "Focus Breath block is not configured to 22.");
        Require((int)Math.Ceiling(Creature.ScaleHpForMultiplayer(
                    Kali.MinimumDirectDamagePerRound, null, 2, 1)) == 20
                && (int)Math.Ceiling(Creature.ScaleHpForMultiplayer(
                    Kali.MinimumDirectDamagePerRound, null, 3, 1)) == 29
                && (int)Math.Ceiling(Creature.ScaleHpForMultiplayer(
                    Kali.MinimumDirectDamagePerRound, null, 4, 1)) == 39,
            "Kali's Act 2 minimum-damage thresholds do not scale to 20/29/39 for 2/3/4 players.");

        decimal firstLoss = Kali.ResolveThresholdTurnLockedHpLoss(
            220m, 30m, 200m, lockConsumed: false, lockActive: false,
            out bool activateLock);
        Require(firstLoss == 20m && activateLock,
            "The first below-50% hit was not clamped and did not start the turn lock.");
        decimal sameTurnLoss = Kali.ResolveThresholdTurnLockedHpLoss(
            200m, 50m, 200m, lockConsumed: true, lockActive: true,
            out activateLock);
        Require(sameTurnLoss == 0m && !activateLock,
            "A later hit in the triggering turn bypassed the 50% lock.");
        decimal laterTurnLoss = Kali.ResolveThresholdTurnLockedHpLoss(
            200m, 50m, 200m, lockConsumed: true, lockActive: false,
            out activateLock);
        Require(laterTurnLoss == 50m && !activateLock,
            "The 50% lock incorrectly persisted into later turns.");
        Require(Kali.ShouldQueueFirstEgoManifestation(
                    egoTriggered: false,
                    manifestationPending: false,
                    currentHp: 200m,
                    threshold: 200m)
                && !Kali.ShouldQueueFirstEgoManifestation(
                    egoTriggered: false,
                    manifestationPending: true,
                    currentHp: 200m,
                    threshold: 200m)
                && !Kali.ShouldQueueFirstEgoManifestation(
                    egoTriggered: true,
                    manifestationPending: false,
                    currentHp: 200m,
                    threshold: 200m),
            "Kali's first E.G.O. threshold is not queued exactly once.");

        for (int seed = 0; seed < 64; seed++)
        {
            IReadOnlyList<string> animations =
                Kali.ResolveAttackAnimationSequence(3, "AttackSlash", seed);
            Require(animations.Count == 3
                    && animations.Distinct(StringComparer.Ordinal).Count() == 3
                    && animations.All(animation => animation is
                        "AttackBlunt" or "AttackPierce" or "AttackSlash"),
                $"Kali's three-hit animation bag repeated or used an invalid frame (seed={seed}).");
        }

        IReadOnlyList<string> longAnimationChain =
            Kali.ResolveAttackAnimationSequence(8, "AttackSlash", 314159);
        Require(longAnimationChain.Zip(longAnimationChain.Skip(1))
                .All(pair => pair.First != pair.Second),
            "Kali's extended multi-hit animation chain contains adjacent repeats.");

        foreach (string propertyName in new[]
                 {
                     nameof(SpecialGuestMonsterBase.EmotionLevel),
                     nameof(SpecialGuestMonsterBase.EmotionUnits),
                     nameof(SpecialGuestMonsterBase.IntentCapacity),
                     nameof(Kali.EgoTriggered),
                     nameof(Kali.EgoActive),
                     nameof(Kali.EgoManifestationPending),
                     nameof(Kali.EgoThresholdTurnLockConsumed),
                     nameof(Kali.EgoThresholdTurnLockRound),
                     nameof(Kali.EgoThresholdTurnLockSide),
                     nameof(Kali.PersistedEnemyCardPlanNumber),
                 })
        {
            _ = typeof(Kali).GetProperty(propertyName, InstanceFlags)
                ?? throw new MissingMemberException(typeof(Kali).FullName, propertyName);
            // 怪物状态不进存档也不同步；这里守住它们不再带 [SavedProperty]，并且在套件的读档模拟里。
            Require(CombatStateProperties.IsListed(typeof(Kali), propertyName),
                $"Kali.{propertyName} is a SavedProperty again, or missing from the reload list.");
        }
    }

    private static void VerifyKaliTurnContract()
    {
        var kali = (Kali)ModelDb.Monster<Kali>().ToMutable();
        MethodInfo initialCapacityGetter = typeof(Kali)
            .GetProperty("InitialIntentCapacity", InstanceFlags)
            ?.GetMethod
            ?? throw new MissingMethodException(
                typeof(Kali).FullName,
                "get_InitialIntentCapacity");
        Require((int)(initialCapacityGetter.Invoke(kali, null) ?? 0)
                == Kali.InitialKaliIntentCapacity,
            "Kali did not retain the configured one-intent opening capacity.");
        Require(Kali.ResolveReceptionRoundIntentCapacity(1, 0) == 1
                && Kali.ResolveReceptionRoundIntentCapacity(2, 0) == 2
                && Kali.ResolveReceptionRoundIntentCapacity(3, 0) == 3
                && Kali.ResolveReceptionRoundIntentCapacity(4, 0) == 4
                && Kali.ResolveReceptionRoundIntentCapacity(99, 0) == 4,
            "Kali's reception-round intent capacity does not grow from one by 0/1/2/3 slots.");
        Require(Kali.ResolveReceptionRoundIntentCapacity(1, 4) == 2
                && Kali.ResolveReceptionRoundIntentCapacity(4, 4) == 5,
            "Kali's level-IV emotion reward does not add the fifth intent slot.");
        MethodInfo resolveEmotion = typeof(SpecialGuestMonsterBase)
            .GetMethod("ResolveEmotionAtPlayerTurnStart", InstanceFlags)
            ?? throw new MissingMethodException(
                typeof(SpecialGuestMonsterBase).FullName,
                "ResolveEmotionAtPlayerTurnStart");
        SetProperty(kali, nameof(SpecialGuestMonsterBase.EmotionLevel), 5);
        SetProperty(kali, nameof(SpecialGuestMonsterBase.LevelFiveRoundCounter), 0);
        int[] levelFiveSteps = new int[4];
        for (int index = 0; index < levelFiveSteps.Length; index++)
        {
            try
            {
                ((Task)resolveEmotion.Invoke(kali, null)!)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (InvalidOperationException exception) when (
                exception.Message == "Creature was accessed before it was set.")
            {
                // The contract model is intentionally unbound; the saved cycle
                // step is written before the real combat power is applied.
            }

            levelFiveSteps[index] = kali.LevelFiveRoundCounter;
        }

        Require(levelFiveSteps.SequenceEqual([1, 2, 3, 1]),
            "Maximum emotion Intangible cycle is not player-turn steps 1/2/3/1.");
        Require(SpecialGuestMonsterBase.ResolveLevelFiveRoundStep(
                    0,
                    reachedLevelFiveThisTurn: true) == 1
                && SpecialGuestMonsterBase.ShouldGrantLevelFiveIntangible(5, 5, 1)
                && !SpecialGuestMonsterBase.ShouldGrantLevelFiveIntangible(5, 5, 2)
                && !SpecialGuestMonsterBase.ShouldGrantLevelFiveIntangible(5, 5, 3)
                && !SpecialGuestMonsterBase.ShouldGrantLevelFiveIntangible(4, 5, 1),
            "Level-V Intangible is not restricted to step one of each three-turn cycle.");
        Require(!Kali.ShouldTickEgoReturnCountdown(CombatSide.Enemy)
                && Kali.ShouldTickEgoReturnCountdown(CombatSide.Player)
                && Kali.ResolveEgoReturnCountdownAfterPlayerTurnStart(2) == 1
                && Kali.ResolveEgoReturnCountdownAfterPlayerTurnStart(1) == 0,
            "Kali's E.G.O return countdown is not tied to the next two player-turn starts.");
        Require(Kali.ResolvePlanCardLimit(1, 1) == 1
                && Kali.ResolvePlanCardLimit(2, 2) == 2
                && Kali.ResolvePlanCardLimit(3, 5) == 5,
            "Kali's plan limit does not consume every available intent slot.");
        Require(Kali.ManifestationRequiredCardIds.SequenceEqual([
                    "RED_MIST_BLOOD_MIST_EGO_CARD",
                    "RED_MIST_FIELD_OF_CORPSES_EGO_CARD"
                ]),
            "Kali's manifestation plan does not force Blood Mist and Field of Corpses.");
        IReadOnlyList<string> normalPlan = Kali.BuildPlanCardIds(
            5,
            egoActive: false,
            forceManifestationCards: false,
            queuedCardIds: [],
            static candidates => candidates[0]);
        Require(normalPlan.Count == 5
                && normalPlan.Distinct(StringComparer.Ordinal).Count() == 5
                && !normalPlan.Contains(Kali.FieldOfCorpsesCardId, StringComparer.Ordinal),
            "Kali's normal plan did not randomly fill all five slots from the legal pool.");
        IReadOnlyList<string> manifestationPlan = Kali.BuildPlanCardIds(
            5,
            egoActive: true,
            forceManifestationCards: true,
            queuedCardIds: [],
            static candidates => candidates[0]);
        Require(manifestationPlan.Count == 5
                && manifestationPlan.Take(2).SequenceEqual(Kali.ManifestationRequiredCardIds)
                && manifestationPlan.Distinct(StringComparer.Ordinal).Count() == 5,
            "Kali's manifestation plan did not force both E.G.O cards and fill the remaining slots.");
    }

    private static void VerifyVisualContract()
    {
        VerifyKaliSceneContract();
        Require(typeof(IContinuousAttackVisuals).IsAssignableFrom(
                typeof(KaliCreatureVisuals)),
            "Kali scene visuals do not support an idle-free attack chain.");
        VerifyBloodMistSettlementDelay();
    }

    private static void VerifyKaliSceneContract()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>(
                KaliCreatureVisuals.ScenePath)
            ?? throw new InvalidOperationException(
                "Could not load Kali's scene-backed visuals.");
        Node2D root = scene.Instantiate<Node2D>();
        try
        {
            Require(root.GetScript().VariantType == Variant.Type.Nil,
                "Kali's tscn root must remain scriptless.");
            Require(root.Position.IsEqualApprox(Vector2.Zero)
                    && root.Scale.IsEqualApprox(Vector2.One)
                    && Mathf.IsZeroApprox(root.Rotation)
                    && Mathf.IsZeroApprox(root.Skew),
                "Kali's tscn root must remain identity; edit MotionRoot/tracks.");
            Node2D motionRoot = root.GetNode<Node2D>("MotionRoot");
            Require(motionRoot.Position.IsEqualApprox(
                        KaliCreatureVisuals.SceneMotionRootPosition)
                    && motionRoot.Scale.IsEqualApprox(Vector2.One)
                    && root.GetNode<Marker2D>("CenterPos").Position
                        .IsEqualApprox(KaliCreatureVisuals.SceneCenterPosition)
                    && root.GetNode<Marker2D>("IntentPos").Position
                        .IsEqualApprox(KaliCreatureVisuals.SceneIntentPosition)
                    && root.GetNode<Marker2D>("TalkPos").Position
                        .IsEqualApprox(KaliCreatureVisuals.SceneTalkPosition),
                "Kali's compact scene layout/anchors changed.");

            AnimationPlayer player = root.GetNode<AnimationPlayer>(
                "AnimationPlayer");
            Require(string.Equals(
                    player.Autoplay,
                    "normal/Idle",
                    StringComparison.Ordinal),
                "Kali editor autoplay must remain normal/Idle.");
            Require(player.GetAnimationLibraryList()
                    .Select(static name => name.ToString())
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .SequenceEqual(KaliAnimationContract.Libraries
                        .OrderBy(static name => name, StringComparer.Ordinal)),
                "Kali's normal/ego animation libraries changed.");

            foreach (string libraryName in KaliAnimationContract.Libraries)
            {
                AnimationLibrary library =
                    player.GetAnimationLibrary(libraryName)
                    ?? throw new InvalidOperationException(
                        $"Missing Kali animation library '{libraryName}'.");
                Require(library.ResourcePath.EndsWith(
                        $"kali_{libraryName}_animations.tres",
                        StringComparison.Ordinal),
                    $"Kali library '{libraryName}' is not external/editable.");
                Require(library.GetAnimationList()
                        .Select(static name => name.ToString())
                        .OrderBy(static name => name, StringComparer.Ordinal)
                        .SequenceEqual(KaliAnimationContract.Animations
                            .OrderBy(static name => name, StringComparer.Ordinal)),
                    $"Kali library '{libraryName}' has a missing/extra action.");

                foreach (string trigger in KaliAnimationContract.Animations)
                {
                    Animation animation = library.GetAnimation(trigger)
                        ?? throw new InvalidOperationException(
                            $"Missing Kali animation '{libraryName}/{trigger}'.");
                    if (trigger == "Idle")
                    {
                        Require(animation.LoopMode
                                == Animation.LoopModeEnum.Linear,
                            $"Kali '{libraryName}/Idle' must loop.");
                        VerifyKaliTrackMode(
                            animation,
                            "MotionRoot/Visuals:texture",
                            Animation.UpdateMode.Discrete,
                            $"{libraryName}/{trigger}");
                        continue;
                    }

                    Require(animation.LoopMode
                            == Animation.LoopModeEnum.None
                            && Mathf.IsEqualApprox(
                                animation.Length,
                                KaliAnimationContract.DurationForTrigger(
                                    trigger)),
                        $"Kali '{libraryName}/{trigger}' duration drifted.");
                    VerifyKaliActionCompletion(
                        animation,
                        $"{libraryName}/{trigger}");

                    if (trigger == "BloodMist")
                    {
                        int textureTrack = FindKaliTrack(
                            animation,
                            "MotionRoot/AttackVisuals:texture");
                        int offsetTrack = FindKaliTrack(
                            animation,
                            "MotionRoot/AttackVisuals:offset");
                        Require(animation.TrackGetKeyCount(textureTrack) == 5
                                && animation.TrackGetKeyCount(offsetTrack) == 5,
                            $"Kali '{libraryName}/BloodMist' must keep five frames.");
                        Vector2 fourth = animation.TrackGetKeyValue(
                            offsetTrack,
                            3).AsVector2();
                        Vector2 fifth = animation.TrackGetKeyValue(
                            offsetTrack,
                            4).AsVector2();
                        Require(Mathf.IsEqualApprox(fourth.Y, fifth.Y),
                            $"Kali '{libraryName}/BloodMist' frame five drifts vertically.");
                    }

                    if (trigger == "FieldOfCorpses")
                    {
                        int textureTrack = FindKaliTrack(
                            animation,
                            "MotionRoot/AttackVisuals:texture");
                        int expectedFrames = libraryName
                            == KaliAnimationContract.EgoLibrary ? 2 : 1;
                        Require(animation.TrackGetKeyCount(textureTrack)
                                == expectedFrames,
                            $"Kali '{libraryName}/FieldOfCorpses' frame count changed.");
                    }
                }
            }
        }
        finally
        {
            root.Free();
        }

        NCreatureVisuals runtimeVisuals = KaliCreatureVisuals.Create(
            "KALI_VISUAL_VERIFY");
        try
        {
            CreatureStateDisplayOffset offset =
                runtimeVisuals.GetNode<CreatureStateDisplayOffset>(
                    "StateDisplayOffset");
            Require(Mathf.IsEqualApprox(
                    offset.LiftY,
                    KaliCreatureVisuals.StateDisplayLiftY),
                "Kali's state display vertical offset changed.");
        }
        finally
        {
            runtimeVisuals.Free();
        }
    }

    private static void VerifyKaliActionCompletion(
        Animation animation,
        string qualifiedName)
    {
        int idleVisible = FindKaliTrack(
            animation,
            "MotionRoot/Visuals:visible");
        int attackVisible = FindKaliTrack(
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
            $"Kali '{qualifiedName}' does not restore idle on its final frame.");
        VerifyKaliTrackMode(
            animation,
            "MotionRoot/AttackVisuals:texture",
            Animation.UpdateMode.Discrete,
            qualifiedName);
        VerifyKaliTrackMode(
            animation,
            "MotionRoot/AttackVisuals:offset",
            Animation.UpdateMode.Discrete,
            qualifiedName);
        VerifyKaliTrackMode(
            animation,
            "MotionRoot/AttackVisuals:position",
            Animation.UpdateMode.Continuous,
            qualifiedName);
        VerifyKaliTrackMode(
            animation,
            "MotionRoot/AttackVisuals:scale",
            Animation.UpdateMode.Continuous,
            qualifiedName);
    }

    private static void VerifyKaliTrackMode(
        Animation animation,
        string path,
        Animation.UpdateMode expected,
        string qualifiedName)
    {
        int track = FindKaliTrack(animation, path);
        Require(animation.ValueTrackGetUpdateMode(track) == expected,
            $"Kali '{qualifiedName}' track '{path}' has the wrong update mode.");
    }

    private static int FindKaliTrack(Animation animation, string path)
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

    private static void VerifyBloodMistSettlementDelay()
    {
        Require(Kali.RequiresCompletedAnimationBeforeSettlement("BloodMist")
                && !Kali.RequiresCompletedAnimationBeforeSettlement("AttackSlash")
                && Mathf.IsZeroApprox(
                    Kali.ResolveAttackerAnimationDelaySeconds("BloodMist"))
                && Mathf.IsEqualApprox(
                    Kali.ResolvePreSettlementAnimationWaitSeconds("BloodMist"),
                    KaliCreatureVisuals.BloodMistAnimationSeconds)
                && Mathf.IsZeroApprox(
                    Kali.ResolvePreSettlementAnimationWaitSeconds("AttackSlash"))
                && Mathf.IsEqualApprox(
                    Kali.ResolveAttackerAnimationDelaySeconds("AttackSlash"),
                    AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds),
            "Kali Blood Mist does not suppress settlement until its animation completes.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void SetProperty<T>(object instance, string propertyName, T value)
    {
        PropertyInfo property = typeof(SpecialGuestMonsterBase).GetProperty(
                propertyName,
                InstanceFlags)
            ?? instance.GetType().GetProperty(
                propertyName,
                InstanceFlags)
            ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
        property.SetValue(instance, value);
    }
}

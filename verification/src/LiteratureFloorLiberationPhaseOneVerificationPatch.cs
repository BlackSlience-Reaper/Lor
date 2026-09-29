using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using ActLikeIt2;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.acts;
using LibraryOfRuina.cards.Leticia;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters.LiteratureFloorLiberation;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.powers.LiteratureFloorLiberation;
using LibraryOfRuina.visuals.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using Environment = System.Environment;
using FileAccess = Godot.FileAccess;

namespace LibraryOfRuinaVerification;

internal static class LiteratureFloorLiberationPhaseOneVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-literature-floor-phase1";
    private const string GiftBoxStunVerifyArg =
        "lor-verify-literature-floor-gift-box-stun";
    private const string LogPrefix =
        "[LibraryOfRuina.LiteratureFloor.Verify] ";
    private static string LocalProjectRoot => VerificationPaths.ProjectRoot;

    private const string BossScenePath =
        "res://scenes/creature_visuals/literature_floor_laetitia_boss.tscn";
    private const string BossAnimationLibraryPath =
        "res://scenes/creature_visuals/literature_floor_laetitia_boss_animations.tres";
    private const string GiftBoxScenePath =
        "res://scenes/creature_visuals/literature_floor_surprise_gift_box.tscn";
    private const string GiftBoxAnimationLibraryPath =
        "res://scenes/creature_visuals/literature_floor_surprise_gift_box_animations.tres";
    private const string FriendScenePath =
        "res://scenes/creature_visuals/literature_floor_little_witch_friend.tscn";
    private const string FriendAnimationLibraryPath =
        "res://scenes/creature_visuals/literature_floor_little_witch_friend_animations.tres";

    private static readonly BindingFlags DeclaredInstance =
        BindingFlags.Instance
        | BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    private static bool _started;

    internal static void Start()
    {
        if (_started
            || (!HasVerifyArg(VerifyArg)
                && !HasVerifyArg(GiftBoxStunVerifyArg)))
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg(string verifyArg) =>
        CommandLineHelper.HasArg(verifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            verifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            if (HasVerifyArg(GiftBoxStunVerifyArg))
            {
                await VerifyCountdownSelfDestructAndFullFriend(
                    focusedGiftBoxStunOnly: true);
                Log.Info(LogPrefix + "GIFT_BOX_STUN_INTENT_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            VerifyRegistrationAndActContract();
            VerifyEncounterAndStateContract();
            VerifyMonsterAndIntentContract();
            VerifyNumericalAlgorithms();
            VerifyResourceAndLocalizationContract();
            await VerifySceneAnimationRuntime();
            await VerifyEarlySuppressionAndPhaseCleanup();
            await VerifyCountdownSelfDestructAndFullFriend();

            Log.Info(LogPrefix + "LITERATURE_FLOOR_PHASE1_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(
                LogPrefix + "LITERATURE_FLOOR_PHASE1_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyRegistrationAndActContract()
    {
        Require(LiberationBossRegistry.RegisterLiteratureFloorLiberation,
            "Literature floor registration is disabled by default.");
        var encounter = ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>();
        Require(LiberationBossRegistry.IsLiberationEncounter(encounter)
                && LiberationBossRegistry.IsEncounterRegistered(encounter)
                && LiberationBossRegistry.IsEncounterRegistered<
                    LiteratureFloorLiberationEncounter>(),
            "Literature floor registry lookups were incomplete.");

        var hod = ModelDb.Act<Hod>();
        Require(hod.Index == 0 && hod.TemplateAct is Overgrowth,
            "Hod must be a first Act using the Overgrowth template.");
        Require(hod.ExpectedBoss is LiteratureFloorLiberationEncounter
                && hod.BossDiscoveryOrder.Single()
                    is LiteratureFloorLiberationEncounter,
            "Hod does not own the fixed Literature floor boss.");
        Require(hod.MapTraveledColor == new Color("3B2118")
                && hod.MapUntraveledColor == new Color("91664A")
                && hod.MapBgColor == new Color("B98E6D"),
            "Hod map colors differ from the muted pale-orange contract.");
        double traveledLuminance = RelativeLuminance(
            hod.MapTraveledColor);
        double untraveledLuminance = RelativeLuminance(
            hod.MapUntraveledColor);
        double backgroundLuminance = RelativeLuminance(hod.MapBgColor);
        Require(traveledLuminance < 0.04d
                && untraveledLuminance is > 0.12d and < 0.20d
                && backgroundLuminance is > 0.25d and < 0.36d
                && backgroundLuminance - untraveledLuminance >= 0.12d,
            "Hod map palette lost the native dark/mid/background luminance hierarchy.");
        Require(LibraryOfRuinaActModel.IsFirstFamily(hod)
                && LibraryActCatalog.All().Any(act => act is Hod),
            "Hod was omitted from the first-family Act catalog.");

        Require(ActRegistry.TryGet(hod.Id.Entry, out ActRegistration? hodReg)
                && hodReg != null
                && hodReg.ActNumber == 1
                && string.Equals(
                    hodReg.SelectionGroupId,
                    "LibraryOfRuina.FirstPair",
                    StringComparison.Ordinal)
                && hodReg.SelectionGroupTitle?.LocEntryKey
                    == "LIBRARY_OF_RUINA_ACT_1_GROUP.title"
                && hodReg.SelectionGroupDescription?.LocEntryKey
                    == "LIBRARY_OF_RUINA_ACT_1_GROUP.description",
            "Hod was not registered in the first Act selection group.");
        ActRegistration[] firstGroup = ActRegistry
            .GetRegistrationsForSlot(1)
            .Where(registration => string.Equals(
                registration.SelectionGroupId,
                "LibraryOfRuina.FirstPair",
                StringComparison.Ordinal))
            .ToArray();
        Require(firstGroup.Select(static registration =>
                    registration.CanonicalAct.GetType())
                .ToHashSet()
                .SetEquals(
                [typeof(Malkuth), typeof(Yesod), typeof(Hod)]),
            "The first Act selection group does not contain exactly three floors.");

        var progress = new ProgressState();
        progress.MarkActAsSeen(hod.Id);
        Require(LibraryActBestiaryDiscoveryPatch.SynchronizeGroup(
                    progress,
                    ModelDb.Act<Malkuth>().Id,
                    ModelDb.Act<Yesod>().Id,
                    hod.Id)
                && progress.DiscoveredActs.Contains(ModelDb.Act<Malkuth>().Id)
                && progress.DiscoveredActs.Contains(ModelDb.Act<Yesod>().Id)
                && progress.DiscoveredActs.Contains(hod.Id),
            "Bestiary discovery did not synchronize the three first-floor Acts.");

        var completion = new CompletionResult();
        FightConsoleLiberationCompletionPatch.Postfix(
            Array.Empty<string>(),
            ref completion);
        Require(completion.Candidates.Contains(
                encounter.Id.Entry,
                StringComparer.OrdinalIgnoreCase),
            "fight completion omitted the Literature floor encounter.");
    }

    private static void VerifyEncounterAndStateContract()
    {
        var encounter = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        Require(encounter.RoomType == RoomType.Boss
                && encounter.HasScene
                && !encounter.ShouldGiveRewards
                && !encounter.IsFullyLiberated,
            "Literature floor encounter completion/reward flags changed.");
        Require(encounter.Slots.SequenceEqual(
            [
                LiteratureFloorLiberationEncounter.GiftLeftSlot,
                LiteratureFloorLiberationEncounter.GiftRightSlot,
                LiteratureFloorLiberationEncounter.LaetitiaSlot
            ]),
            "Literature floor slot order changed.");
        Require(encounter.AllPossibleMonsters.Select(static monster =>
                    monster.GetType())
                .ToHashSet()
                .SetEquals(
                [
                    typeof(LiteratureFloorLaetitiaBoss),
                    typeof(LiteratureFloorSurpriseGiftBox),
                    typeof(LiteratureFloorLittleWitchFriend),
                    typeof(LiteratureFloorRedEyesBoss),
                    typeof(LiteratureFloorEnhancedSmallSpider),
                    typeof(LiteratureFloorBloodlustBoss),
                    typeof(LiteratureFloorEnhancedLeftShoe),
                    typeof(LiteratureFloorTodaysExpressionBoss)
                ]),
            "Literature floor Bestiary roster is incomplete.");

        encounter.GenerateMonstersWithSlots(NullRunState.Instance);
        (MonsterModel, string?)[] generated = encounter.MonstersWithSlots
            .ToArray();
        Require(generated.Length == 3
                && generated.Count(pair =>
                    pair.Item1 is LiteratureFloorSurpriseGiftBox) == 2
                && generated.Count(pair =>
                    pair.Item1 is LiteratureFloorLaetitiaBoss) == 1,
            "Phase one did not generate Laetitia and two gift boxes.");

        var restored = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        restored.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "1",
            ["KilledBossCount"] = "0",
            ["PhaseComplete"] = "False",
            ["SuperGiftPending"] = "True",
            ["NormalMovesUntilSuperGift"] = "1",
            ["LeftFriendSpawnRound"] = "7",
            ["LeftFriendWeakened"] = "True",
            ["RightFriendSpawnRound"] = "8",
            ["RightFriendWeakened"] = "False"
        });
        Dictionary<string, string> roundTrip = restored.SaveCustomState();
        Require(roundTrip["SuperGiftPending"] == bool.TrueString
                && roundTrip["NormalMovesUntilSuperGift"] == "1"
                && roundTrip["LeftFriendSpawnRound"] == "7"
                && roundTrip["LeftFriendWeakened"] == bool.TrueString
                && roundTrip["RightFriendSpawnRound"] == "8",
            "Encounter custom state did not round-trip.");

        var phaseTwo = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        phaseTwo.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "2",
            ["KilledBossCount"] = "1",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False"
        });
        phaseTwo.GenerateMonstersWithSlots(NullRunState.Instance);
        Require(phaseTwo.CurrentPhase == 2
                && phaseTwo.KilledBossCount == 1
                && !phaseTwo.PhaseComplete
                && !phaseTwo.TransitionPending
                && phaseTwo.Slots.SequenceEqual(
                [
                    LiteratureFloorLiberationEncounter
                        .EnhancedSpiderLeftSlot,
                    LiteratureFloorLiberationEncounter
                        .EnhancedSpiderRightSlot,
                    LiteratureFloorLiberationEncounter.RedEyesSlot
                ])
                && phaseTwo.MonstersWithSlots.Count(pair =>
                    pair.Item1
                        is LiteratureFloorEnhancedSmallSpider) == 2
                && phaseTwo.MonstersWithSlots.Count(pair =>
                    pair.Item1 is LiteratureFloorRedEyesBoss) == 1
                && !phaseTwo.IsFullyLiberated,
            "Direct phase-two state did not generate Red Eyes and two spiders.");

        var phaseThree = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        phaseThree.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "3",
            ["KilledBossCount"] = "2",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False"
        });
        phaseThree.GenerateMonstersWithSlots(NullRunState.Instance);
        Require(phaseThree.Slots.SequenceEqual(
                [
                    LiteratureFloorLiberationEncounter
                        .EnhancedLeftShoeSlot,
                    LiteratureFloorLiberationEncounter.BloodlustSlot
                ])
                && phaseThree.MonstersWithSlots.Count(pair =>
                    pair.Item1 is LiteratureFloorEnhancedLeftShoe) == 1
                && phaseThree.MonstersWithSlots.Count(pair =>
                    pair.Item1 is LiteratureFloorBloodlustBoss) == 1,
            "Direct phase-three state did not generate Bloodlust and the enhanced Left Shoe.");

        var phaseFour = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        phaseFour.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "4",
            ["KilledBossCount"] = "3",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False"
        });
        phaseFour.GenerateMonstersWithSlots(NullRunState.Instance);
        Require(phaseFour.Slots.SequenceEqual(
                [LiteratureFloorLiberationEncounter.TodaysExpressionSlot])
                && phaseFour.MonstersWithSlots.Count(pair =>
                    pair.Item1 is LiteratureFloorTodaysExpressionBoss) == 1,
            "Direct phase-four state did not generate Today's Expression.");

        var futurePhase = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        futurePhase.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "5",
            ["KilledBossCount"] = "4",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False"
        });
        futurePhase.GenerateMonstersWithSlots(NullRunState.Instance);
        Require(futurePhase.Slots.Count == 0
                && futurePhase.MonstersWithSlots.Count == 0,
            "A future phase fell back to an implemented Literature-floor layout.");

        Type[] customStateTypes =
        [
            typeof(LiteratureFloorLiberationEncounter),
            typeof(LiteratureFloorLaetitiaBoss),
            typeof(LiteratureFloorSurpriseGiftBox),
            typeof(LiteratureFloorLittleWitchFriend),
            typeof(LiteratureFloorRedEyesBoss),
            typeof(LiteratureFloorEnhancedSmallSpider),
            typeof(LiteratureFloorBloodlustBoss),
            typeof(LiteratureFloorEnhancedLeftShoe),
            typeof(LiteratureFloorTodaysExpressionBoss),
            typeof(LiteratureFloorDeepWoundPower),
            typeof(LiteratureFloorBloodlustGiantAxePassivePower),
            typeof(LiteratureFloorBloodlustGlitterPassivePower)
        ];
        Require(customStateTypes.All(type => type
                .GetProperties(DeclaredInstance)
                .All(property => property.GetCustomAttribute<
                    SavedPropertyAttribute>() == null)),
            "Literature floor added a SavedProperty wire-schema field.");
    }

    private static void VerifyMonsterAndIntentContract()
    {
        Require(LiteratureFloorLaetitiaBoss.DebugHpRange(false)
                    == (120, 127)
                && LiteratureFloorLaetitiaBoss.DebugHpRange(true)
                    == (133, 140)
                && LiteratureFloorLaetitiaBoss.DebugAttackDamage(false) == 12
                && LiteratureFloorLaetitiaBoss.DebugAttackDamage(true) == 14
                && LiteratureFloorLaetitiaBoss.DebugPermanentStrong(false) == 2
                && LiteratureFloorLaetitiaBoss.DebugPermanentStrong(true) == 3,
            "Laetitia normal/ascended values changed.");
        Require(LiteratureFloorSurpriseGiftBox.DebugHpRange(false)
                    == (30, 34)
                && LiteratureFloorSurpriseGiftBox.DebugHpRange(true)
                    == (38, 40)
                && LiteratureFloorSurpriseGiftBox.DebugSmallDamage(false) == 3
                && LiteratureFloorSurpriseGiftBox.DebugSmallDamage(true) == 4
                && LiteratureFloorSurpriseGiftBox
                    .DebugSelfDestructDamage(false) == 16
                && LiteratureFloorSurpriseGiftBox
                    .DebugSelfDestructDamage(true) == 19,
            "Gift-box normal/ascended values changed.");
        Require(LiteratureFloorLittleWitchFriend
                    .DebugMoveOneDamage(false) == 7
                && LiteratureFloorLittleWitchFriend
                    .DebugMoveOneDamage(true) == 9
                && LiteratureFloorLittleWitchFriend
                    .DebugMoveTwoDamage(false) == 2
                && LiteratureFloorLittleWitchFriend
                    .DebugMoveTwoDamage(true) == 3,
            "Little Witch's Friend normal/ascended damage changed.");

        var laetitia = ModelDb.Monster<LiteratureFloorLaetitiaBoss>();
        var giftBox = ModelDb.Monster<LiteratureFloorSurpriseGiftBox>();
        var friend = ModelDb.Monster<LiteratureFloorLittleWitchFriend>();
        Require(laetitia.DefaultChaoResistance == 100
                && giftBox.DefaultChaoResistance == 20
                && friend.DefaultChaoResistance == 40
                && friend.MinInitialHp == 75
                && friend.MaxInitialHp == 75,
            "Literature floor fixed HP/chao values changed.");
        RequireUniformResistance(
            laetitia.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Normal,
            "Laetitia physical");
        RequireUniformResistance(
            laetitia.DefaultChaoResistanceData,
            LibraryResistanceLevel.Endure,
            "Laetitia chao");
        RequireResistance(
            friend.DefaultPhysicalResistanceData,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Vulnerable,
            "Little Witch's Friend physical");
        RequireResistance(
            friend.DefaultChaoResistanceData,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Vulnerable,
            "Little Witch's Friend chao");

        MonsterMoveStateMachine bossMoves = GenerateStateMachine(
            ModelDb.Monster<LiteratureFloorLaetitiaBoss>().ToMutable());
        RequireIntentTypes(
            bossMoves,
            "SEND_YOU_A_GIFT",
            typeof(SingleAttackIntent),
            typeof(DetailedStatusCardIntent<LeticiaGift>),
            typeof(DefendIntent));
        RequireIntentTypes(
            bossMoves,
            "DONT_GET_HURT",
            typeof(DefendIntent),
            typeof(HealIntent));
        RequireIntentTypes(
            bossMoves,
            "HAVE_FUN",
            typeof(BuffIntent),
            typeof(DefendIntent));
        RequireIntentTypes(
            bossMoves,
            "ITS_A_GIFT",
            typeof(DefendIntent),
            typeof(DebuffIntent));
        RequireIntentTypes(
            bossMoves,
            "SUPER_GIFT",
            typeof(IndiscriminateAttackIntent),
            typeof(DetailedStatusCardIntent<LeticiaGift>));
        RequireIntentTypes(
            bossMoves,
            LiteratureFloorLaetitiaBoss.ReviveAndEmpowerMoveId,
            typeof(HealIntent),
            typeof(BuffIntent));

        MonsterMoveStateMachine giftBoxMoves = GenerateStateMachine(
            ModelDb.Monster<LiteratureFloorSurpriseGiftBox>().ToMutable());
        RequireIntentTypes(
            giftBoxMoves,
            "EE_YO_LI_WOO",
            typeof(DetailedStatusCardIntent<LeticiaGift>));
        RequireIntentTypes(
            giftBoxMoves,
            "COUGH_OMM_JJI_AO",
            typeof(MultiAttackIntent),
            typeof(DetailedStatusCardIntent<LeticiaGift>));
        Require(((MultiAttackIntent)((MoveState)giftBoxMoves.States[
                    "COUGH_OMM_JJI_AO"]).Intents[0]).Repeats == 3,
            "Gift-box multiattack repeat count changed.");
        RequireIntentTypes(
            giftBoxMoves,
            "PU_AO_JJI_AH",
            typeof(DeathBlowIntent),
            typeof(DetailedStatusCardIntent<LeticiaGift>));

        MonsterMoveStateMachine friendMoves = GenerateStateMachine(
            ModelDb.Monster<LiteratureFloorLittleWitchFriend>().ToMutable());
        RequireIntentTypes(
            friendMoves,
            "GLITCH_FLUTTER",
            typeof(BadgedAttackIntent));
        RequireIntentTypes(
            friendMoves,
            "BROKEN_LAUGHTER",
            typeof(MultiAttackIntent));
        Require(((MultiAttackIntent)((MoveState)friendMoves.States[
                    "BROKEN_LAUGHTER"]).Intents[0]).Repeats == 4,
            "Friend multiattack repeat count changed.");
        RequireIntentTypes(
            friendMoves,
            "SNATCH_GIFT",
            typeof(BuffIntent));

        RequireGreenPassive<LiteratureFloorLaetitiaSuperGiftPassivePower>();
        RequireGreenPassive<LiteratureFloorLaetitiaPlayWithMePassivePower>();
        RequireGreenPassive<LiteratureFloorLaetitiaLonelyPassivePower>();
        RequireGreenPassive<LiteratureFloorGiftBoxBoomPassivePower>();
        RequireGreenPassive<LiteratureFloorSurpriseAppearancePassivePower>();
        RequireGreenPassive<
            LiteratureFloorLittleWitchFriendHandItOverPassivePower>();
    }

    private static void VerifyNumericalAlgorithms()
    {
        Require(LiteratureFloorGiftHandMetrics.MinGiftCountFromCounts(
                    [4, 1, 3]) == 1
                && LiteratureFloorGiftHandMetrics.MaxGiftCountFromCounts(
                    [4, 1, 3]) == 4
                && LiteratureFloorGiftHandMetrics.MinGiftCountFromCounts([])
                    == 0
                && LiteratureFloorGiftHandMetrics.MaxGiftCountFromCounts([])
                    == 0,
            "Minimum/maximum player Gift algorithms changed.");

        decimal[] expectedSendGiftBlocks = [18m, 14m, 10m, 7m, 3m, 0m];
        decimal[] expectedLargeBlocks = [36m, 28m, 21m, 14m, 7m, 0m];
        for (int gifts = 0; gifts < expectedSendGiftBlocks.Length; gifts++)
        {
            Require(LiteratureFloorLaetitiaBoss.CalculateAdjustedBlock(
                        18,
                        gifts) == expectedSendGiftBlocks[gifts]
                    && LiteratureFloorLaetitiaBoss.CalculateAdjustedBlock(
                        36,
                        gifts) == expectedLargeBlocks[gifts],
                "Block floor/reduction changed for Gift count " + gifts + ".");
        }

        Require(LiteratureFloorLaetitiaBoss.CalculateHealAmount(127m) == 11m
                && LiteratureFloorLaetitiaBoss.CalculateHealAmount(40m) == 4m
                && LiteratureFloorLaetitiaBoss.CalculateHealAmount(75m) == 6m,
            "Eight-percent healing no longer rounds upward.");
        Require(LiteratureFloorLiberationEncounter.CalculateFriendCurrentHp(
                    75m,
                    weakened: false) == 75m
                && LiteratureFloorLiberationEncounter.CalculateFriendCurrentHp(
                    75m,
                    weakened: true) == 38m,
            "Full/half-health friend calculation changed.");

        var cadence = (LiteratureFloorLiberationEncounter)ModelDb
            .Encounter<LiteratureFloorLiberationEncounter>()
            .ToMutable();
        cadence.CompleteSuperGift();
        Require(!cadence.IsSuperGiftDue(null),
            "Super Gift repeated immediately after use.");
        cadence.CompleteNormalGiftMove();
        Require(!cadence.IsSuperGiftDue(null),
            "Super Gift repeated after only one normal move.");
        cadence.CompleteNormalGiftMove();
        Require(cadence.IsSuperGiftDue(null),
            "Super Gift was not due on the third action.");
    }

    private static void VerifyResourceAndLocalizationContract()
    {
        string[] requiredResources =
        [
            LiteratureFloorLiberationEncounter.EncounterScenePath,
            LiteratureFloorLiberationEncounter.BackgroundScenePath,
            LiteratureFloorLiberationEncounter.BackgroundLayerScenePath,
            LiteratureFloorLiberationEncounter.BackgroundTexturePath,
            LiteratureFloorLiberationEncounter.BossNodeResourcePath + ".png",
            LiteratureFloorLiberationEncounter.BossNodeResourcePath
                + "_outline.png",
            "res://images/ui/run_history/literature_floor_liberation_encounter.png",
            "res://images/ui/run_history/literature_floor_liberation_encounter_outline.png",
            BossScenePath,
            BossAnimationLibraryPath,
            GiftBoxScenePath,
            GiftBoxAnimationLibraryPath,
            FriendScenePath,
            FriendAnimationLibraryPath,
            "res://audio/sfx/literature_floor_liberation/laetitia_strong_charge.ogg",
            "res://audio/sfx/literature_floor_liberation/laetitia_strong_attack.ogg",
            "res://images/powers/library_passive_green.png"
        ];
        foreach (string path in requiredResources)
        {
            Require(ResourceLoader.Exists(path),
                "Required Literature floor resource is missing: " + path);
        }

        VerifySceneNodeContract(
            LiteratureFloorLiberationEncounter.EncounterScenePath,
            ["gift_left", "gift_right", "laetitia"],
            requireScriptless: true);
        VerifySceneNodeContract(
            BossScenePath,
            ["MotionRoot", "MotionRoot/Visuals", "MotionRoot/AttackVisuals",
                "AnimationPlayer", "Bounds", "CenterPos", "IntentPos", "TalkPos"],
            requireScriptless: true);
        VerifySceneNodeContract(
            GiftBoxScenePath,
            ["MotionRoot", "MotionRoot/Visuals", "MotionRoot/AttackVisuals",
                "AnimationPlayer", "Bounds", "CenterPos", "IntentPos", "TalkPos"],
            requireScriptless: true);
        VerifySceneNodeContract(
            FriendScenePath,
            ["MotionRoot", "MotionRoot/Visuals", "MotionRoot/AttackVisuals",
                "AnimationPlayer", "Bounds", "CenterPos", "IntentPos", "TalkPos"],
            requireScriptless: true);

        VerifyAnimationLibrary(
            BossAnimationLibraryPath,
            new Dictionary<string, double>
            {
                ["Idle"] = 1d,
                ["Attack"] = LiteratureFloorLaetitiaAnimationContract
                    .AttackDurationSeconds,
                ["Cast"] = LiteratureFloorLaetitiaAnimationContract
                    .CastDurationSeconds,
                ["Hit"] = LiteratureFloorLaetitiaAnimationContract
                    .HitDurationSeconds,
                ["SuperGift"] = LiteratureFloorLaetitiaAnimationContract
                    .SuperGiftDurationSeconds
            });
        VerifyAnimationLibrary(
            GiftBoxAnimationLibraryPath,
            new Dictionary<string, double>
            {
                ["Idle"] = 1d,
                ["Attack"] = LiteratureFloorGiftBoxAnimationContract
                    .AttackDurationSeconds,
                ["Cast"] = LiteratureFloorGiftBoxAnimationContract
                    .CastDurationSeconds,
                ["Hit"] = LiteratureFloorGiftBoxAnimationContract
                    .HitDurationSeconds,
                ["SelfDestruct"] = LiteratureFloorGiftBoxAnimationContract
                    .SelfDestructDurationSeconds
            });
        VerifyAnimationLibrary(
            FriendAnimationLibraryPath,
            new Dictionary<string, double>
            {
                ["Idle"] = 1d,
                ["Attack"] = LiteratureFloorLittleWitchFriendAnimationContract
                    .AttackDurationSeconds,
                ["AttackAlt"] = LiteratureFloorLittleWitchFriendAnimationContract
                    .AttackDurationSeconds,
                ["Cast"] = LiteratureFloorLittleWitchFriendAnimationContract
                    .CastDurationSeconds,
                ["Hit"] = LiteratureFloorLittleWitchFriendAnimationContract
                    .HitDurationSeconds
            });

        VerifyLaetitiaSourceHashes();
        VerifyIconContract();
        VerifyLocalizationKeyParity();
    }

    private static async Task VerifySceneAnimationRuntime()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        var host = new Node2D
        {
            Name = "LiteratureFloorVisualVerificationHost"
        };
        var boss = (LiteratureFloorLaetitiaBossCreatureVisuals)
            WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LiteratureFloorLaetitiaBossCreatureVisuals>(
                "LITERATURE_FLOOR_LAETITIA_VERIFY",
                BossScenePath);
        var giftBox = (LiteratureFloorGiftBoxCreatureVisuals)
            WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LiteratureFloorGiftBoxCreatureVisuals>(
                "LITERATURE_FLOOR_GIFT_BOX_VERIFY",
                GiftBoxScenePath);
        var friend = (LiteratureFloorLittleWitchFriendCreatureVisuals)
            WrappedMonsterVisualFactory.CreateSceneBackedVisuals<
                LiteratureFloorLittleWitchFriendCreatureVisuals>(
                "LITERATURE_FLOOR_FRIEND_VERIFY",
                FriendScenePath);
        host.AddChild(boss);
        host.AddChild(giftBox);
        host.AddChild(friend);
        game.AddChild(host);

        try
        {
            await WaitFrame();
            Require(boss.CurrentAnimationName == "laetitia/Idle"
                    && giftBox.CurrentAnimationName == "gift_box/Idle"
                    && friend.CurrentAnimationName == "friend/Idle",
                "A Literature floor visual did not start in Idle.");

            Require(boss.TryPlayTrigger("SuperGift")
                    && boss.CurrentAnimationName == "laetitia/SuperGift"
                    && boss.GetNode<Sprite2D>("%AttackVisuals")
                        .Texture.ResourcePath.EndsWith(
                            "laetitia_super_s1.png",
                            StringComparison.Ordinal),
                "Super Gift did not claim its S1 frame at time zero.");
            boss.AnimationPlayer.Advance(0.94d);
            Require(boss.GetNode<Sprite2D>("%AttackVisuals")
                    .Texture.ResourcePath.EndsWith(
                        "laetitia_super_s2.png",
                        StringComparison.Ordinal),
                "Super Gift did not advance to S2.");
            boss.AnimationPlayer.Advance(0.87d);
            Require(boss.GetNode<Sprite2D>("%AttackVisuals")
                    .Texture.ResourcePath.EndsWith(
                        "laetitia_super_s3.png",
                        StringComparison.Ordinal),
                "Super Gift did not advance to S3.");
            Require(boss.TryPlayTrigger("Hit")
                    && boss.CurrentAnimationName == "laetitia/Hit",
                "Hit did not interrupt Super Gift.");
            boss.AnimationPlayer.Advance(
                LiteratureFloorLaetitiaAnimationContract.HitDurationSeconds
                + 0.02d);
            await WaitFrame();
            Require(boss.CurrentAnimationName == "laetitia/Idle",
                "Laetitia did not return to Idle after Hit.");

            Require(giftBox.TryPlayTrigger("SelfDestruct")
                    && giftBox.CurrentAnimationName
                        == "gift_box/SelfDestruct"
                    && giftBox.TryPlayTrigger("Hit")
                    && giftBox.CurrentAnimationName == "gift_box/Hit",
                "Gift-box action preemption failed.");
            giftBox.AnimationPlayer.Advance(
                LiteratureFloorGiftBoxAnimationContract.HitDurationSeconds
                + 0.02d);
            await WaitFrame();
            Require(giftBox.CurrentAnimationName == "gift_box/Idle",
                "Gift box did not return to Idle.");

            Require(friend.TryPlayTrigger("Attack")
                    && friend.TryPlayTrigger("AttackAlt")
                    && friend.CurrentAnimationName == "friend/AttackAlt",
                "Friend alternate attack did not preempt the prior action.");
            friend.AnimationPlayer.Advance(
                LiteratureFloorLittleWitchFriendAnimationContract
                    .AttackDurationSeconds + 0.02d);
            await WaitFrame();
            Require(friend.CurrentAnimationName == "friend/Idle",
                "Friend did not return to Idle.");
        }
        finally
        {
            host.QueueFree();
            await WaitFrame();
        }
    }

    private sealed record CombatContext(
        CombatState State,
        LiteratureFloorLiberationEncounter Encounter,
        LiteratureFloorLaetitiaBoss Boss,
        Creature BossCreature,
        IReadOnlyList<LiteratureFloorSurpriseGiftBox> GiftBoxes,
        Creature Player);

    private static async Task VerifyEarlySuppressionAndPhaseCleanup()
    {
        CombatContext fight = await StartFight(
            "LITERATUREFLOORVERIFY_EARLY",
            ascensionLevel: 0);
        Require(fight.BossCreature.MaxHp is >= 120 and <= 127,
            "A0 Laetitia HP was outside 120..127.");
        Require(((LibraryCreature)fight.BossCreature).MaxChaoValue == 100,
            "Laetitia chao resistance was not 100.");
        Require(fight.GiftBoxes.All(box =>
                    box.Creature.MaxHp is >= 30 and <= 34
                    && ((LibraryCreature)box.Creature).MaxChaoValue == 20),
            "A0 gift-box HP/chao values were incorrect.");
        Require(fight.BossCreature.HasPower<HistoryFloorCorrosionPower>()
                && fight.BossCreature.HasPower<
                    LiteratureFloorLaetitiaSuperGiftPassivePower>()
                && fight.BossCreature.HasPower<
                    LiteratureFloorLaetitiaPlayWithMePassivePower>()
                && fight.BossCreature.HasPower<
                    LiteratureFloorLaetitiaLonelyPassivePower>()
                && fight.GiftBoxes.All(box => box.Creature.HasPower<
                    LiteratureFloorGiftBoxBoomPassivePower>()),
            "Opening passive bundle was incomplete.");
        Require(fight.Boss.NextMove.StateId == "SEND_YOU_A_GIFT"
                && fight.GiftBoxes.All(box =>
                    box.NextMove.StateId == "EE_YO_LI_WOO"),
            "Opening move sequence changed.");

        await fight.Boss.PerformMove();
        Require(CountGifts(fight.Player, PileType.Hand) == 3,
            "Send You a Gift did not add three hand Gifts.");
        Require(fight.BossCreature.Block == 7m,
            "Send You a Gift did not use the latest three-card hand for Block.");
        fight.Boss.RollMove(fight.State.PlayerCreatures);
        Require(fight.Boss.NextMove.StateId == "DONT_GET_HURT",
            "Laetitia did not advance to Don't Get Hurt.");

        Dictionary<Creature, decimal> hpBeforeHeal = fight.State.Enemies
            .Where(static enemy => enemy.IsAlive)
            .ToDictionary(static enemy => enemy, static enemy =>
                enemy.MaxHp - 10m);
        foreach ((Creature enemy, decimal hp) in hpBeforeHeal)
        {
            await CreatureCmd.SetCurrentHp(enemy, hp);
        }
        Dictionary<Creature, int> blockBeforeHeal = fight.State.Enemies
            .Where(static enemy => enemy.IsAlive)
            .ToDictionary(static enemy => enemy, static enemy => enemy.Block);
        await fight.Boss.PerformMove();
        foreach ((Creature enemy, decimal hpBefore) in hpBeforeHeal)
        {
            Require(enemy.CurrentHp == Math.Min(
                        enemy.MaxHp,
                        hpBefore + LiteratureFloorLaetitiaBoss
                            .CalculateHealAmount(enemy.MaxHp))
                    && enemy.Block == blockBeforeHeal[enemy] + 8m,
                "Don't Get Hurt heal/Block settlement changed.");
        }
        fight.Boss.RollMove(fight.State.PlayerCreatures);
        Require(fight.Boss.NextMove.StateId == "HAVE_FUN",
            "Laetitia did not advance to Have Fun.");

        Dictionary<Creature, int> blockBeforeFun = fight.State.Enemies
            .Where(static enemy => enemy.IsAlive)
            .ToDictionary(static enemy => enemy, static enemy => enemy.Block);
        await fight.Boss.PerformMove();
        foreach (Creature enemy in fight.State.Enemies.Where(static enemy =>
                     enemy.IsAlive))
        {
            Require(enemy.Block == blockBeforeFun[enemy] + 9m
                    && enemy.GetPower<LibraryStrongPower>()?.Amount == 2,
                "Have Fun did not grant adjusted Block and permanent Strong.");
        }
        fight.Boss.RollMove(fight.State.PlayerCreatures);
        Require(fight.Boss.NextMove.StateId == "SEND_YOU_A_GIFT",
            "Gift-box-alive sequence did not loop to Send You a Gift.");

        foreach (LiteratureFloorSurpriseGiftBox giftBox in fight.GiftBoxes)
        {
            await CreatureCmd.Kill(giftBox.Creature, force: true);
        }
        await WaitFrames(3);
        Require(fight.Boss.NextMove.StateId == "SUPER_GIFT",
            "Player-turn suppression did not immediately queue Super Gift.");
        RequirePhysicalResistance(
            fight.BossCreature,
            LibraryResistanceLevel.Fatal,
            "Laetitia without friends");

        int hpBeforeSuperGift = fight.Player.CurrentHp;
        int expectedSuperGiftDamage = fight.Boss.NextMove.Intents
            .OfType<AttackIntent>()
            .Single()
            .GetSingleDamage(
                fight.State.PlayerCreatures,
                fight.BossCreature);
        await fight.Boss.PerformMove();
        Require(fight.Player.CurrentHp
                    == hpBeforeSuperGift - expectedSuperGiftDamage
                && CountGifts(fight.Player, PileType.Hand) == 5,
            "Super Gift A0 damage or hand Gifts were incorrect: expectedDamage="
            + expectedSuperGiftDamage + " hpBefore=" + hpBeforeSuperGift
            + " hpAfter=" + fight.Player.CurrentHp + " handGifts="
            + CountGifts(fight.Player, PileType.Hand) + ".");
        fight.Boss.RollMove(fight.State.PlayerCreatures);
        Require(fight.Boss.NextMove.StateId == "ITS_A_GIFT",
            "Super Gift did not enter the normal Gift cadence.");

        decimal blockBeforeGift = fight.BossCreature.Block;
        await fight.Boss.PerformMove();
        Require(fight.BossCreature.Block == blockBeforeGift
                && fight.Player.GetPower<LibraryWeakPower>()?.Amount == 3
                && fight.Player.GetPower<LibraryVulnerablePower>()?.Amount == 3,
            "It's a Gift failed its zero-Block/debuff settlement at five Gifts.");
        fight.Boss.RollMove(fight.State.PlayerCreatures);
        Require(fight.Boss.NextMove.StateId == "ITS_A_GIFT",
            "Super Gift cadence did not retain the second normal move.");
        await fight.Boss.PerformMove();
        fight.Boss.RollMove(fight.State.PlayerCreatures);
        Require(fight.Boss.NextMove.StateId == "SUPER_GIFT",
            "Super Gift did not recur on the third action.");

        fight.State.RoundNumber++;
        await fight.Encounter.SpawnDueFriends(
            CombatSide.Player,
            fight.State);
        LiteratureFloorLittleWitchFriend[] weakenedFriends = fight.State
            .Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorLittleWitchFriend>()
            .ToArray();
        Require(weakenedFriends.Length == 2
                && weakenedFriends.All(friend =>
                    friend.Creature.CurrentHp == 38m),
            "Early suppression did not summon two half-health friends next turn.");
        RequirePhysicalResistance(
            fight.BossCreature,
            LibraryResistanceLevel.Normal,
            "Laetitia after friend spawn");

        await CardPileCmdCompat.AddToCombatAndPreview<LeticiaGift>(
            [fight.Player],
            PileType.Draw,
            1,
            addedByPlayer: false);
        await CardPileCmdCompat.AddToCombatAndPreview<LeticiaGift>(
            [fight.Player],
            PileType.Discard,
            1,
            addedByPlayer: false);
        Require(CountAllGifts(fight.Player) >= 7,
            "Gift cleanup verifier failed to seed visible/hidden piles.");

        int hpBeforeTransition = Math.Max(
            1,
            fight.Player.MaxHp - 20);
        await CreatureCmd.SetCurrentHp(
            fight.Player,
            hpBeforeTransition);
        await CreatureCmd.Kill(fight.BossCreature, force: true);
        await WaitFrames(3);
        Require(fight.Encounter.CurrentPhase == 2
                && fight.Encounter.KilledBossCount == 1
                && !fight.Encounter.PhaseComplete
                && fight.Encounter.TransitionPending
                && !fight.Encounter.IsFullyLiberated
                && CountAllGifts(fight.Player) == 0
                && !fight.State.Enemies.Any(static enemy => enemy.IsAlive),
            "Laetitia death did not enter the controlled phase-two transition.");
        Require(fight.Boss.NextMove.StateId
                    == LiteratureFloorLaetitiaBoss
                        .ReviveAndEmpowerMoveId,
            "Laetitia did not expose REVIVE_AND_EMPOWER after death.");
        Dictionary<string, string> completedState =
            fight.Encounter.SaveCustomState();
        Require(completedState["LeftFriendSpawnRound"] == "-1"
                && completedState["RightFriendSpawnRound"] == "-1"
                && completedState["SuperGiftPending"] == bool.FalseString
                && completedState["TransitionPending"] == bool.TrueString,
            "Laetitia death did not cancel pending friend/group state.");

        await fight.Boss.PerformMove();
        await WaitFrames(3);
        Require(!fight.Encounter.TransitionPending
                && !fight.Encounter.PhaseComplete
                && fight.Player.CurrentHp
                    == Math.Min(
                        fight.Player.MaxHp,
                        hpBeforeTransition
                        + LiteratureFloorLiberationEncounter
                            .PhaseTransitionHealAmount)
                && fight.State.Enemies.Count(static enemy =>
                    enemy.IsAlive
                    && enemy.Monster
                        is LiteratureFloorEnhancedSmallSpider) == 2
                && fight.State.Enemies.Count(static enemy =>
                    enemy.IsAlive
                    && enemy.Monster is LiteratureFloorRedEyesBoss) == 1,
            "Laetitia transition did not heal 10 HP and spawn phase two.");

        CleanupRun();
        await WaitForRunCleanup("early-suppression verifier cleanup");
    }

    private static async Task VerifyCountdownSelfDestructAndFullFriend(
        bool focusedGiftBoxStunOnly = false)
    {
        CombatContext fight = await StartFight(
            "LITERATUREFLOORVERIFY_COUNTDOWN",
            (int)AscensionLevel.DeadlyEnemies);
        if (!focusedGiftBoxStunOnly)
        {
            Require(fight.BossCreature.MaxHp is >= 133 and <= 140
                    && fight.GiftBoxes.All(box =>
                        box.Creature.MaxHp is >= 38 and <= 40),
                "Ascended HP ranges were incorrect.");
            var openingAttack = fight.Boss.NextMove.Intents
                .OfType<AttackIntent>()
                .Single();
            Require(openingAttack.GetSingleDamage(
                        fight.State.PlayerCreatures,
                        fight.BossCreature) == 14,
                "Ascended Laetitia intent damage was not 14.");
        }

        LiteratureFloorSurpriseGiftBox giftBox = fight.GiftBoxes[0];
        LiteratureFloorGiftBoxBoomPassivePower boom = giftBox.Creature
            .GetPower<LiteratureFloorGiftBoxBoomPassivePower>()
            ?? throw new InvalidOperationException(
                "Gift-box Boom passive was missing.");
        MonsterMoveStateMachine giftBoxStateMachine =
            giftBox.MoveStateMachine
            ?? throw new InvalidOperationException(
                "Gift-box move state machine was missing.");
        var choiceContext = new BlockingPlayerChoiceContext();

        giftBoxStateMachine.OnMovePerformed(giftBox.NextMove);
        await boom.AfterSideTurnEnd(
            choiceContext,
            CombatSide.Enemy,
            fight.State.Enemies);
        Require(boom.Amount == 2 && giftBox.Creature.IsAlive,
            "Boom did not count the first survival turn.");
        giftBox.Creature.PrepareForNextTurn(fight.State.PlayerCreatures);
        Require(giftBox.NextMove.Id == "COUGH_OMM_JJI_AO",
            "Second-turn intent did not follow the countdown.");

        giftBoxStateMachine.OnMovePerformed(giftBox.NextMove);
        await boom.AfterSideTurnEnd(
            choiceContext,
            CombatSide.Enemy,
            fight.State.Enemies);
        Require(boom.Amount == 1 && giftBox.Creature.IsAlive,
            "Boom did not count the second survival turn.");
        giftBox.Creature.PrepareForNextTurn(fight.State.PlayerCreatures);
        Require(giftBox.NextMove.Id == "PU_AO_JJI_AH"
                && giftBox.NextMove.Intents
                    .OfType<DeathBlowIntent>()
                    .Count() == 1,
            "Third-turn intent was not self-destruct.");

        await LibraryCreatureCmd.Stun((LibraryCreature)giftBox.Creature);
        Require(giftBox.NextMove.Id == "STUNNED"
                && giftBox.NextMove.FollowUpState?.Id
                    == "GIFT_BOX_COUNTDOWN_ROUTER",
            "Third-turn stun did not preserve countdown routing.");
        MoveState stunnedMove = giftBox.NextMove;
        await stunnedMove.PerformMove(fight.State.PlayerCreatures);
        giftBoxStateMachine.OnMovePerformed(stunnedMove);
        await boom.AfterSideTurnEnd(
            choiceContext,
            CombatSide.Enemy,
            fight.State.Enemies);
        Require(boom.Amount == 0 && giftBox.Creature.IsAlive,
            "Countdown power executed self-destruct during the stun turn.");
        giftBox.Creature.PrepareForNextTurn(fight.State.PlayerCreatures);
        Require(giftBox.NextMove.Id == "PU_AO_JJI_AH"
                && giftBox.NextMove.Intents
                    .OfType<DeathBlowIntent>()
                    .Count() == 1,
            "Delayed turn did not restore the self-destruct intent.");

        int drawGiftCountBeforeSelfDestruct =
            CountGifts(fight.Player, PileType.Draw);
        await giftBox.PerformMove();
        await WaitFrames(3);
        Require(giftBox.Creature.IsDead
                && CountGifts(fight.Player, PileType.Draw)
                    == drawGiftCountBeforeSelfDestruct + 3,
            "Delayed self-destruct move did not deal its shared settlement.");

        if (focusedGiftBoxStunOnly)
        {
            CleanupRun();
            await WaitForRunCleanup("gift-box stun verifier cleanup");
            return;
        }

        fight.State.RoundNumber++;
        await fight.Encounter.SpawnDueFriends(
            CombatSide.Player,
            fight.State);
        LiteratureFloorLittleWitchFriend friend = fight.State.Enemies
            .Where(enemy => enemy.IsAlive
                && enemy.SlotName
                    == LiteratureFloorLiberationEncounter.GiftLeftSlot)
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorLittleWitchFriend>()
            .Single();
        Require(friend.Creature.CurrentHp == 75m
                && friend.Creature.MaxHp == 75m
                && ((LibraryCreature)friend.Creature).MaxChaoValue == 40,
            "Natural self-destruction did not summon a full-health friend.");
        Require(friend.ConfiguredInitialMoveId == "GLITCH_FLUTTER"
                && friend.NextMove.StateId == "GLITCH_FLUTTER",
            "Left-slot friend initial move changed.");

        await CardPileCmdCompat.AddToCombatAndPreview<LeticiaGift>(
            [fight.Player],
            PileType.Hand,
            2,
            addedByPlayer: false);
        AttackIntent friendAttack = friend.NextMove.Intents
            .OfType<AttackIntent>()
            .Single();
        Require(friendAttack.GetSingleDamage(
                    fight.State.PlayerCreatures,
                    friend.Creature) == 11,
            "Friend intent did not share the maximum-hand Gift damage hook.");
        decimal hpBeforeAttack = fight.Player.CurrentHp;
        await friend.PerformMove();
        Require(fight.Player.CurrentHp == hpBeforeAttack - 11m
                && fight.Player.GetPower<LibraryBleedingPower>()?.Amount == 3,
            "Friend attack damage/Bleed settlement differed from its intent.");

        var snatch = (MoveState)friend.MoveStateMachine!.States["SNATCH_GIFT"];
        friend.SetMoveImmediate(snatch, forceTransition: true);
        await friend.PerformMove();
        Require(CountGifts(fight.Player, PileType.Hand) == 0
                && friend.Creature.GetPower<StrengthPower>()?.Amount == 2,
            "Take the Gifts did not consume the hand and gain maximum Strength.");

        CleanupRun();
        await WaitForRunCleanup("countdown verifier cleanup");
    }

    private static async Task<CombatContext> StartFight(
        string seed,
        int ascensionLevel)
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            ascensionLevel);
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            ModelDb.Encounter<LiteratureFloorLiberationEncounter>()
                .ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress,
            "Literature floor combat start");
        await WaitFrames(6);

        CombatState state = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
        var encounter = state.Encounter
                as LiteratureFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "Literature floor encounter was not active.");
        LiteratureFloorLaetitiaBoss boss = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorLaetitiaBoss>()
            .Single();
        LiteratureFloorSurpriseGiftBox[] giftBoxes = state.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorSurpriseGiftBox>()
            .OrderBy(static box => box.Creature.SlotName)
            .ToArray();
        Creature player = state.PlayerCreatures.Single();
        return new CombatContext(
            state,
            encounter,
            boss,
            boss.Creature,
            giftBoxes,
            player);
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
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (skipNextFork?.CanWrite == true)
        {
            skipNextFork.SetValue(null, true);
        }
    }

    private static MonsterMoveStateMachine GenerateStateMachine(
        MonsterModel monster)
    {
        MethodInfo method = monster.GetType().GetMethod(
                "GenerateMoveStateMachine",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "GenerateMoveStateMachine was missing for "
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
                && state is MoveState move,
            "Missing move state " + moveId + ".");
        Type[] actual = ((MoveState)state!).Intents
            .Select(static intent => intent.GetType())
            .ToArray();
        Require(actual.SequenceEqual(expectedTypes),
            moveId + " intent types changed: "
            + string.Join(",", actual.Select(static type => type.Name)) + ".");
    }

    private static void RequireGreenPassive<T>()
        where T : LiteratureFloorGreenPassivePower
    {
        T power = ModelDb.Power<T>();
        Require(power.Type == PowerType.None
                && power.StackType is PowerStackType.Single
                    or PowerStackType.Counter
                && power.PackedIconPath.EndsWith(
                    "library_passive_green.png",
                    StringComparison.Ordinal),
            typeof(T).Name + " did not use the green passive contract.");
    }

    private static void VerifySceneNodeContract(
        string scenePath,
        IEnumerable<string> nodePaths,
        bool requireScriptless)
    {
        string sceneText = FileAccess.GetFileAsString(scenePath);
        if (requireScriptless)
        {
            Require(!sceneText.Contains(
                    "ext_resource type=\"Script\"",
                    StringComparison.Ordinal),
                scenePath + " contains an attached script.");
        }

        PackedScene packedScene = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException(
                "Unable to load scene " + scenePath + ".");
        Node root = packedScene.Instantiate();
        try
        {
            foreach (string nodePath in nodePaths)
            {
                Require(root.GetNodeOrNull(nodePath) != null,
                    scenePath + " is missing node " + nodePath + ".");
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
        AnimationLibrary library = ResourceLoader.Load<AnimationLibrary>(path)
            ?? throw new InvalidOperationException(
                "Unable to load animation library " + path + ".");
        HashSet<string> actualNames = library.GetAnimationList()
            .Select(static name => name.ToString())
            .ToHashSet(StringComparer.Ordinal);
        Require(actualNames.SetEquals(expectedLengths.Keys),
            path + " animation names changed: "
            + string.Join(",", actualNames) + ".");
        foreach ((string name, double expectedLength) in expectedLengths)
        {
            Animation animation = library.GetAnimation(name)
                ?? throw new InvalidOperationException(
                    path + " lacks animation " + name + ".");
            Require(Math.Abs(animation.Length - expectedLength) < 0.001d,
                path + "/" + name + " length changed.");
            for (int track = 0; track < animation.GetTrackCount(); track++)
            {
                Require(animation.TrackGetKeyCount(track) > 0
                        && Math.Abs(animation.TrackGetKeyTime(track, 0))
                            < 0.0001d,
                    path + "/" + name
                    + " has a track without a time-zero owner.");
            }
        }
    }

    private static void VerifyLaetitiaSourceHashes()
    {
        string? sourceRoot = VerificationPaths.ArtSourceRoot;
        if (sourceRoot == null)
        {
            Log.Info(LogPrefix + "LOR_ART_SOURCE_ROOT is not set; skipped the Laetitia source hash check.");
            return;
        }

        var mappings = new Dictionary<string, (string RepoName, string Hash)>
        {
            ["Laetitia.png"] = (
                "laetitia_idle.png",
                "1E3FB782605122E6D07B1B08E353C947343610E66E6C89970DA3ED5A2BAD0ECF"),
            ["Laetitia-开火.png"] = (
                "laetitia_attack.png",
                "8C1D6404C4764AA2300B65323BFF59857F699FAB6A34305225668A33FF9C1B2A"),
            ["Laetitia-招架.png"] = (
                "laetitia_cast.png",
                "F2874E493E1B257F179579DC9BAA2A8C98CA2C568E795B09304F45C0CFD36A9E"),
            ["Laetitia-受击.png"] = (
                "laetitia_hit.png",
                "65FBB91DF0399B15E440708490EBA67E3457AC3F152E0B7F3963BD0956E99C0A"),
            ["Laetitia-S1.png"] = (
                "laetitia_super_s1.png",
                "80CC51CDED2BAC8E5778D507CF70EB7BEC0F653A8577D252B6F900BFCBD05604"),
            ["Laetitia-S2.png"] = (
                "laetitia_super_s2.png",
                "998ED7A09D1DE7DB9814185AC9E7551E37D64AB4EFE6A6C80F58E486A76C2EB4"),
            ["Laetitia-S3.png"] = (
                "laetitia_super_s3.png",
                "F3D07CE4C8C0D508C2B64A868FC06143041A26A7B8BEBCD2B20B13E975B052F8")
        };
        foreach ((string sourceName, (string repoName, string expectedHash))
                 in mappings)
        {
            string sourcePath = Path.Combine(
                sourceRoot,
                sourceName);
            string repoPath = Path.Combine(
                LocalProjectRoot,
                "images",
                "monsters",
                "literature_floor_liberation",
                repoName);
            Require(File.Exists(sourcePath)
                    && File.Exists(repoPath),
                "Missing Laetitia source/repository image: " + sourceName);
            string sourceHash = Sha256(sourcePath);
            string repoHash = Sha256(repoPath);
            Require(sourceHash == expectedHash
                    && repoHash == expectedHash
                    && sourceHash == repoHash,
                sourceName + " SHA256 mismatch: source=" + sourceHash
                + " repo=" + repoHash + ".");
        }
    }

    private static void VerifyIconContract()
    {
        string runMain = Path.Combine(
            LocalProjectRoot,
            "images",
            "ui",
            "run_history",
            "literature_floor_liberation_encounter.png");
        string runOutline = Path.Combine(
            LocalProjectRoot,
            "images",
            "ui",
            "run_history",
            "literature_floor_liberation_encounter_outline.png");
        Require(Sha256(runMain) == Sha256(runOutline),
            "Run-history Hod icon did not preserve the original colored crop.");

        string mapMain =
            LiteratureFloorLiberationEncounter.BossNodeResourcePath + ".png";
        string mapOutline =
            LiteratureFloorLiberationEncounter.BossNodeResourcePath
            + "_outline.png";
        int linePixels = CountWhitePixels(mapMain);
        int outlinePixels = CountWhitePixels(mapOutline);
        Require(linePixels > 0 && outlinePixels > linePixels,
            "Hod map icon is empty or its outline was not dilated.");

        string sourceBackground = Path.Combine(
            LocalProjectRoot,
            "images",
            "backgrounds",
            "leticia_elite",
            "leticia_background.png");
        string dedicatedBackground = Path.Combine(
            LocalProjectRoot,
            "images",
            "backgrounds",
            "literature_floor_liberation_encounter",
            "creature_map_latitia_composite.png");
        Require(Sha256(sourceBackground) == Sha256(dedicatedBackground),
            "Literature floor background differs from the CreatureMap_Latitia composite.");
    }

    private static int CountWhitePixels(string path)
    {
        Texture2D texture = ResourceLoader.Load<Texture2D>(path)
            ?? throw new InvalidOperationException(
                "Unable to load icon texture " + path + ".");
        Image image = texture.GetImage();
        int count = 0;
        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                Color pixel = image.GetPixel(x, y);
                if (pixel.A <= 0.001f)
                {
                    continue;
                }

                Require(pixel.R >= 0.995f
                        && pixel.G >= 0.995f
                        && pixel.B >= 0.995f,
                    path + " contains a non-white map-line pixel.");
                count++;
            }
        }

        return count;
    }

    private static void VerifyLocalizationKeyParity()
    {
        string[] languages = ["zhs", "eng", "jpn", "kor"];
        string[] tables = ["acts", "encounters", "monsters", "powers"];
        foreach (string table in tables)
        {
            HashSet<string>? baseline = null;
            foreach (string language in languages)
            {
                string path =
                    $"res://LibraryOfRuina/localization/{language}/{table}.json";
                using JsonDocument document = JsonDocument.Parse(
                    FileAccess.GetFileAsString(path));
                HashSet<string> keys = document.RootElement
                    .EnumerateObject()
                    .Select(static property => property.Name)
                    .ToHashSet(StringComparer.Ordinal);
                baseline ??= keys;
                Require(baseline.SetEquals(keys),
                    table + " localization keys differ for " + language + ".");
            }
        }
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static double RelativeLuminance(Color color)
    {
        static double Linearize(float value) => value <= 0.04045f
            ? value / 12.92d
            : Math.Pow((value + 0.055d) / 1.055d, 2.4d);

        return 0.2126d * Linearize(color.R)
            + 0.7152d * Linearize(color.G)
            + 0.0722d * Linearize(color.B);
    }

    private static int CountGifts(Creature player, PileType pileType) =>
        player.Player == null
            ? 0
            : pileType.GetPile(player.Player).Cards.Count(static card =>
                card is LeticiaGift);

    private static int CountAllGifts(Creature player) =>
        player.Player?.PlayerCombatState?.AllCards.Count(static card =>
            card is LeticiaGift) ?? 0;

    private static void RequirePhysicalResistance(
        Creature creature,
        LibraryResistanceLevel expected,
        string label)
    {
        var libraryCreature = creature as LibraryCreature
            ?? throw new InvalidOperationException(
                label + " was not a LibraryCreature.");
        Require(libraryCreature.GetPhysicalResistanceLevel(
                    LibraryDamageType.Slash) == expected
                && libraryCreature.GetPhysicalResistanceLevel(
                    LibraryDamageType.Pierce) == expected
                && libraryCreature.GetPhysicalResistanceLevel(
                    LibraryDamageType.Blunt) == expected,
            label + " physical resistance mismatch.");
    }

    private static void RequireUniformResistance(
        LibraryCreatureResistanceData.Resistance? resistance,
        LibraryResistanceLevel expected,
        string label) =>
        RequireResistance(
            resistance,
            expected,
            expected,
            expected,
            label);

    private static void RequireResistance(
        LibraryCreatureResistanceData.Resistance? resistance,
        LibraryResistanceLevel slash,
        LibraryResistanceLevel pierce,
        LibraryResistanceLevel blunt,
        string label)
    {
        Require(resistance?.Slash == slash
                && resistance.Pierce == pierce
                && resistance.Blunt == blunt,
            label + " resistance mismatch.");
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

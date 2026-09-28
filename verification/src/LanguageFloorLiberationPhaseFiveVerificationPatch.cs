using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.backgrounds.LanguageFloorLiberation;
using LibraryOfRuina.cards.LanguageFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.events.LanguageFloorLiberation;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters.LanguageFloorLiberation;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class LanguageFloorLiberationPhaseFiveVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-language-floor-phase5";
    private const string VerifyLethalArg =
        "lor-verify-language-floor-phase5-lethal";
    private const string VerifyTargetingArg =
        "lor-verify-language-floor-mimicry-targeting";
    private const string LogPrefix =
        "[LibraryOfRuina.LanguageFloor.Phase5.Verify] ";
    private static bool _started;

    internal static void Start()
    {
        if (_started || (!HasVerifyArg()
            && !HasVerifyLethalArg()
            && !HasVerifyTargetingArg()))
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyArg,
                StringComparison.OrdinalIgnoreCase));

    private static bool HasVerifyLethalArg() =>
        CommandLineHelper.HasArg(VerifyLethalArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyLethalArg,
                StringComparison.OrdinalIgnoreCase));

    private static bool HasVerifyTargetingArg() =>
        CommandLineHelper.HasArg(VerifyTargetingArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyTargetingArg,
                StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            if (HasVerifyTargetingArg())
            {
                await VerifyAllPlayerAttackCoverage();
                Log.Info(
                    LogPrefix
                    + "LANGUAGE_FLOOR_MIMICRY_TARGETING_OK");
                CleanupRun();
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (HasVerifyLethalArg())
            {
                await VerifyMissingFinalBossFallback();
                await VerifyLethalEvolutionAndVictory();
                Log.Info(
                    LogPrefix
                    + "LANGUAGE_FLOOR_PHASE5_LETHAL_OK");
                CleanupRun();
                NGame.Instance?.GetTree().Quit();
                return;
            }

            VerifyStaticDefinitions();
            VerifyLocalization();
            await VerifyMissingFinalBossFallback();

            PhaseFiveContext normal = await StartPhaseFiveFight(
                "LANGUAGEFLOORVERIFY_MIMICRY_NORMAL");
            await VerifyOpening(normal);
            await VerifyFormOneMovesAndFear(normal);
            await VerifyNormalEvolution(normal);
            await VerifyFormThreePassives(normal);
            await VerifyFormThreeMoves(normal);
            VerifySavedPropertyRoundTrip(normal.Boss);
            CleanupRun();
            await WaitForRunCleanup("normal phase-five cleanup");

            await VerifyLethalEvolutionAndVictory();
            Log.Info(LogPrefix + "LANGUAGE_FLOOR_PHASE5_OK");
            CleanupRun();
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "LANGUAGE_FLOOR_PHASE5_FAIL\n" + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyStaticDefinitions()
    {
        Require(LanguageFloorLiberationEncounter.MaxPhase == 5,
            "Language Floor encounter maximum phase was not five.");
        Require(LanguageFloorMimicry.FormOneMaxHpBase == 300
                && LanguageFloorMimicry.FormTwoMaxHpBase == 300
                && LanguageFloorMimicry.FormThreeMaxHpBase == 500,
            "Mimicry form HP constants were incorrect.");
        SpriteVisualProfile mimicryProfile =
            LanguageFloorMimicryCreatureVisuals.Profile;
        Require(mimicryProfile.Variants["third"].Position
                    == new Vector2(0f, 100f)
                && mimicryProfile.Frames["first_thrust"].NudgeValue
                    == new Vector2(-6f, 53f)
                && mimicryProfile.Frames["third_strike"].NudgeValue
                    == new Vector2(0f, -60f),
            "Mimicry sprite profile offsets were incorrect.");
        Require(LanguageFloorMimicry.FormOneMaxChao == 160
                && LanguageFloorMimicry.FormTwoMaxChao == 400
                && LanguageFloorMimicry.FormThreeMaxChao == 500,
            "Mimicry form chao constants were incorrect.");
        Require(LanguageFloorMimicry.FormTwoEntryHpPercent == 50
                && LanguageFloorMimicry.FormTwoThorns == 16
                && LanguageFloorMimicry.FormTwoBlock == 99
                && LanguageFloorMimicry
                    .FormTwoActionsBeforeEvolution == 2
                && LanguageFloorMimicry
                    .FormThreeMinimumHpPercent == 70,
            "Mimicry form-two/evolution constants were incorrect.");
        Require(LanguageFloorMimicry.HardenThreshold == 13
                && LanguageFloorMimicry
                    .FormThreeRegenerationDamageThreshold == 80
                && LanguageFloorMimicry
                    .FormThreeRegenerationPercent == 10
                && LanguageFloorMimicry.MaximumMimicStacks == 20
                && LanguageFloorMimicry.MimicDecayPerTurn == 1,
            "Mimicry form-three passive constants were incorrect.");
        Require(LanguageFloorFearCard.HpLoss == 3,
            "Terror HP loss was not three.");

        Dictionary<LanguageFloorMimicryMove, (int Normal, int Deadly)>
            damages = new()
            {
                [LanguageFloorMimicryMove.ClumsyFlesh] = (7, 8),
                [LanguageFloorMimicryMove.EyeContact] = (17, 19),
                [LanguageFloorMimicryMove.ExtendArm] = (12, 13),
                [LanguageFloorMimicryMove.Mimic] = (8, 9),
                [LanguageFloorMimicryMove.Skin] = (7, 8),
                [LanguageFloorMimicryMove.Hello] = (30, 33),
                [LanguageFloorMimicryMove.Goodbye] = (20, 25)
            };
        foreach ((LanguageFloorMimicryMove move, var values) in damages)
        {
            Require(LanguageFloorMimicry.DebugGetMoveDamage(
                        move,
                        deadlyEnemies: false) == values.Normal
                    && LanguageFloorMimicry.DebugGetMoveDamage(
                        move,
                        deadlyEnemies: true) == values.Deadly,
                move + " damage pair was incorrect.");
        }

        Require(LanguageFloorMimicry.DebugGetWeights(
                    LanguageFloorMimicryForm.First)
                .SequenceEqual(
                [
                    (LanguageFloorMimicryMove.ClumsyFlesh, 2),
                    (LanguageFloorMimicryMove.EyeContact, 1),
                    (LanguageFloorMimicryMove.ExtendArm, 1)
                ]),
            "Form-one move weights were not 2:1:1.");
        Require(LanguageFloorMimicry.DebugGetWeights(
                    LanguageFloorMimicryForm.Third)
                .SequenceEqual(
                [
                    (LanguageFloorMimicryMove.Mimic, 3),
                    (LanguageFloorMimicryMove.Skin, 2),
                    (LanguageFloorMimicryMove.Imitate, 2),
                    (LanguageFloorMimicryMove.Hello, 1),
                    (LanguageFloorMimicryMove.Goodbye, 1)
                ]),
            "Form-three move weights were not 3:2:2:1:1.");
        VerifyDeterministicSelection(LanguageFloorMimicryForm.First, 3);
        VerifyDeterministicSelection(LanguageFloorMimicryForm.Third, 5);
        VerifySceneMarker();

        Require(EncounterBgmController
                    .ResolveLiberationPhaseTrackIndex(5) == 2
                && LanguageFloorLiberationEncounter
                    .RolandLiberationBgmTracks[2]
                    .EndsWith(
                        "roland_liberation_phase_3.ogg",
                        StringComparison.Ordinal),
            "Phase five did not use the third Language Floor BGM.");
        Require(LanguageFloorLiberationBackgroundController
                    .GetPhaseBackgroundTexturePath(5)
                == LanguageFloorLiberationBackgroundController
                    .PhaseFiveTexturePath,
            "Phase five did not use the Nothing There background.");

        var encounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        Require(encounter.AllPossibleMonsters
                .Any(static monster =>
                    monster is LanguageFloorMimicry),
            "Mimicry was absent from the Language Floor encounter.");

        foreach (string path in LanguageFloorMimicry.AssetPathsStatic
                     .Append(
                         LanguageFloorLiberationBackgroundController
                             .PhaseFiveTexturePath))
        {
            Require(ResourceLoader.Exists(path),
                "Required phase-five resource was missing: " + path);
        }

        Type[] savedTypes = SavedPropertiesTypeCacheCompat
            .GetAllModSavedPropertyTypes();
        Require(savedTypes.Contains(typeof(LanguageFloorMimicry)),
            "Mimicry SavedProperties type was not auto-discovered.");
        string schema = SavedPropertiesTypeCacheCompat
            .BuildSchemaFingerprintMaterial();
        string[] requiredSavedFields =
        [
            nameof(LanguageFloorMimicry.Form),
            nameof(LanguageFloorMimicry.FormTwoActionsCompleted),
            nameof(LanguageFloorMimicry.PreviousMove),
            nameof(LanguageFloorMimicry.EvolutionPending),
            nameof(LanguageFloorMimicry.PendingForm),
            nameof(LanguageFloorMimicry.MimicStacks),
            nameof(LanguageFloorMimicry.RoundDamageTaken),
            nameof(LanguageFloorMimicry.PlannedMoveEnhanced),
            nameof(LanguageFloorMimicry.PlannedTargetCombatId)
        ];
        Require(schema.Contains(
                    typeof(LanguageFloorMimicry).FullName!,
                    StringComparison.Ordinal)
                && requiredSavedFields.All(field =>
                    schema.Contains(field, StringComparison.Ordinal)),
            "Mimicry save fields were absent from the schema fingerprint.");
    }

    private static void VerifyDeterministicSelection(
        LanguageFloorMimicryForm form,
        int expectedDistinct)
    {
        var first = (LanguageFloorMimicry)ModelDb
            .Monster<LanguageFloorMimicry>()
            .ToMutable();
        var second = (LanguageFloorMimicry)ModelDb
            .Monster<LanguageFloorMimicry>()
            .ToMutable();
        first.DebugSetMovePlanState(form);
        second.DebugSetMovePlanState(form);
        var firstRng = new Rng(0x51A1C5UL);
        var secondRng = new Rng(0x51A1C5UL);
        string[] firstSequence = new string[120];
        string[] secondSequence = new string[120];
        for (int index = 0; index < firstSequence.Length; index++)
        {
            firstSequence[index] = first.DebugChooseMove(firstRng);
            secondSequence[index] = second.DebugChooseMove(secondRng);
            if (index > 0)
            {
                Require(firstSequence[index] != firstSequence[index - 1],
                    form + " selected the same move consecutively.");
            }
        }

        Require(firstSequence.SequenceEqual(secondSequence),
            form + " selection was not deterministic for a fixed RNG.");
        Require(firstSequence.Distinct(StringComparer.Ordinal).Count()
                == expectedDistinct,
            form + " deterministic sample did not reach every move.");
    }

    private static void VerifyLocalization()
    {
        string[] locales = ["zhs", "eng", "jpn", "kor"];
        string[] files = ["monsters", "intents", "powers", "cards"];
        var documents = new Dictionary<(string Locale, string File),
            Dictionary<string, string>>();
        foreach (string locale in locales)
        {
            foreach (string file in files)
            {
                string path =
                    $"res://LibraryOfRuina/localization/{locale}/{file}.json";
                string json = FileAccess.GetFileAsString(path);
                Require(!string.IsNullOrWhiteSpace(json),
                    "Localization file was unreadable: " + path);
                using JsonDocument parsed = JsonDocument.Parse(json);
                documents[(locale, file)] = parsed.RootElement
                    .EnumerateObject()
                    .ToDictionary(
                        static property => property.Name,
                        static property => property.Value.GetString()
                            ?? string.Empty,
                        StringComparer.Ordinal);
            }
        }

        foreach (string file in files)
        {
            HashSet<string> keys = documents[("zhs", file)].Keys
                .Where(static key =>
                    key.StartsWith(
                        "LANGUAGE_FLOOR_MIMICRY",
                        StringComparison.Ordinal)
                    || key.StartsWith(
                        "LANGUAGE_FLOOR_FEAR",
                        StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
            foreach (string locale in locales.Skip(1))
            {
                HashSet<string> localeKeys =
                    documents[(locale, file)].Keys
                        .Where(static key =>
                            key.StartsWith(
                                "LANGUAGE_FLOOR_MIMICRY",
                                StringComparison.Ordinal)
                            || key.StartsWith(
                                "LANGUAGE_FLOOR_FEAR",
                                StringComparison.Ordinal))
                        .ToHashSet(StringComparer.Ordinal);
                Require(keys.SetEquals(localeKeys),
                    locale + "/" + file
                    + " Mimicry localization keys were not aligned.");
            }

            foreach (string key in keys)
            {
                string[] expectedTokens = ExtractTokens(
                    documents[("zhs", file)][key]);
                foreach (string locale in locales.Skip(1))
                {
                    Require(expectedTokens.SequenceEqual(ExtractTokens(
                            documents[(locale, file)][key])),
                        locale + "/" + file
                        + " token mismatch: " + key);
                }
            }
        }
    }

    private static async Task VerifyOpening(PhaseFiveContext fight)
    {
        Require(fight.Encounter.CurrentPhase == 5
                && !fight.Encounter.PhaseComplete
                && !fight.Encounter.TransitionPending
                && !fight.Encounter.ShouldKeepCombatOpen(
                    fight.CombatState),
            "Direct phase-five load did not remain active.");
        Require(fight.Boss.Form == LanguageFloorMimicryForm.First
                && fight.BossCreature.MaxHp == 200
                && fight.BossCreature.CurrentHp == 200
                && fight.BossCreature.SlotName
                    == LanguageFloorLiberationEncounter.MimicryBossSlot,
            "Mimicry did not open in first form at 200/200 HP.");
        LibraryCreature libraryCreature =
            RequireLibraryCreature(fight.BossCreature);
        Require(libraryCreature.MaxChaoValue == 80
                && libraryCreature.CurrentChaoValue == 80,
            "First-form chao resistance was not 80/80.");
        Require(fight.BossCreature.GetPower<
                    LanguageFloorMimicryFormOneEvolutionPower>() != null
                && fight.BossCreature.GetPower<
                    LanguageFloorMimicryFormTwoEvolutionPower>() == null
                && fight.BossCreature.GetPower<
                    LanguageFloorMimicryHardenPower>() == null,
            "First-form passive set was incorrect.");
        RequireBackgroundTexture(
            LanguageFloorLiberationBackgroundController.PhaseFiveTexturePath);
        Require(fight.BossNode.Visuals
                is LanguageFloorMimicryCreatureVisuals,
            "Mimicry did not use its scripted non-Spine visuals.");

        IReadOnlyList<AbstractIntent> clumsy =
            fight.Boss.DebugGetIntents(
                LanguageFloorMimicryMove.ClumsyFlesh);
        IReadOnlyList<AbstractIntent> imitate =
            fight.Boss.DebugGetIntents(
                LanguageFloorMimicryMove.Imitate);
        IReadOnlyList<AbstractIntent> skin =
            fight.Boss.DebugGetIntents(
                LanguageFloorMimicryMove.Skin);
        Require(clumsy.Count == 1
                && clumsy[0] is BadgedAttackIntent
                    {
                        Badges.Count: 1
                    } clumsyAttack
                && clumsyAttack.Badges[0].Amount
                    == LanguageFloorMimicry.ClumsyFleshFlaw
                && !clumsyAttack.AssetPaths.Contains(
                    IndiscriminateAttackIntent
                        .GroupAttackBadgeImagePath,
                    StringComparer.Ordinal)
                && imitate.Count == 2
                && imitate[0]
                    is DynamicDetailedStatusCardIntent<Burn>
                && imitate[1] is BadgedDebuffIntent
                && skin.Count == 1
                && skin[0] is BadgedAttackIntent
                    {
                        Badges.Count: 1
                    } skinAttack
                && skinAttack.Badges[0].PowerType
                    == typeof(LibraryStrongPower),
            "Mimicry intent composition was incorrect.");
        foreach (LanguageFloorMimicryMove move in new[]
                 {
                     LanguageFloorMimicryMove.ClumsyFlesh,
                     LanguageFloorMimicryMove.EyeContact,
                     LanguageFloorMimicryMove.ExtendArm,
                     LanguageFloorMimicryMove.Mimic,
                     LanguageFloorMimicryMove.Skin,
                     LanguageFloorMimicryMove.Hello,
                     LanguageFloorMimicryMove.Goodbye
                 })
        {
            Require(fight.Boss.DebugGetIntents(move)
                    .Single() is BadgedAttackIntent,
                move + " was not a normal all-player attack intent.");
        }

        await CreatureCmd.SetMaxHp(fight.Player, 1000m);
        await CreatureCmd.SetCurrentHp(fight.Player, 1000m);
    }

    private static async Task VerifyAllPlayerAttackCoverage()
    {
        PhaseFiveContext fight = await StartPhaseFiveFight(
            "LANGUAGEFLOORVERIFY_MIMICRY_TARGETING",
            useFakeMultiplayer: true);
        Creature[] players = fight.CombatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();
        Require(players.Length == 2,
            "Mimicry targeting verifier did not create two living players.");

        LanguageFloorMimicryMove[] attackMoves =
        [
            LanguageFloorMimicryMove.ClumsyFlesh,
            LanguageFloorMimicryMove.EyeContact,
            LanguageFloorMimicryMove.ExtendArm,
            LanguageFloorMimicryMove.Mimic,
            LanguageFloorMimicryMove.Skin,
            LanguageFloorMimicryMove.Hello,
            LanguageFloorMimicryMove.Goodbye
        ];
        foreach (LanguageFloorMimicryMove move in attackMoves)
        {
            foreach (Creature player in players)
            {
                await CreatureCmd.SetMaxHp(player, 5000m);
                await CreatureCmd.SetCurrentHp(player, 5000m);
                await ClearBlock(player, fight.BossCreature);
            }

            await ResetBossOffensivePowers(fight);
            fight.Boss.DebugSetMovePlanState(
                move is LanguageFloorMimicryMove.ClumsyFlesh
                    or LanguageFloorMimicryMove.EyeContact
                    or LanguageFloorMimicryMove.ExtendArm
                    ? LanguageFloorMimicryForm.First
                    : LanguageFloorMimicryForm.Third);
            await fight.Boss.DebugPlanMove(move);
            int[] hpBefore = players
                .Select(static player => player.CurrentHp)
                .ToArray();
            await fight.Boss.DebugPerformMove(move);
            int[] losses = players
                .Select((player, index) =>
                    hpBefore[index] - player.CurrentHp)
                .ToArray();
            Require(losses.All(static loss => loss > 0)
                    && losses.Distinct().Count() == 1,
                move + " did not damage every living player equally.");
            Require(fight.Boss.DebugGetIntents(move)
                    .Single() is BadgedAttackIntent,
                move + " did not use the default all-player attack intent.");
            if (move is LanguageFloorMimicryMove.ClumsyFlesh
                or LanguageFloorMimicryMove.EyeContact
                or LanguageFloorMimicryMove.ExtendArm
                or LanguageFloorMimicryMove.Mimic
                or LanguageFloorMimicryMove.Skin)
            {
                RequireLastTargetsAreAllLivingPlayers(fight);
            }
        }
    }

    private static async Task VerifyFormOneMovesAndFear(
        PhaseFiveContext fight)
    {
        await ResetPlayer(fight);
        int hpBefore = fight.Player.CurrentHp;
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.ClumsyFlesh);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.ClumsyFlesh);
        Require(fight.Player.CurrentHp == hpBefore - 21,
            "Dense Flesh did not deal 7 damage three times.");
        Require(fight.Player.GetPowerInstances<LibraryDisarmPower>()
                .SingleOrDefault() is
                {
                    Amount: 3,
                    TurnsRemaining: 3
                },
            "Dense Flesh did not apply 3 Flaw for 3 turns.");
        Require(fight.Boss.LastAttackAnimationTriggers.SequenceEqual(
                new[]
                {
                    "AttackThrust",
                    "AttackThrust",
                    "AttackThrust"
                }),
            "First-form multi-hit animation was not repeated thrust.");
        RequireLastTargetsAreAllLivingPlayers(fight);

        await ResetPlayer(fight);
        int fearBefore = CountCards<LanguageFloorFearCard>(
            fight.Player);
        hpBefore = fight.Player.CurrentHp;
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.EyeContact);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.EyeContact);
        Require(fight.Player.CurrentHp == hpBefore - 17
                && CountCards<LanguageFloorFearCard>(fight.Player)
                    == fearBefore + LanguageFloorMimicry.FearCards,
            "Eye Contact damage or Terror generation was incorrect.");
        LanguageFloorFearCard[] fears = PileType.Hand
            .GetPile(fight.Player.Player!)
            .Cards
            .OfType<LanguageFloorFearCard>()
            .Take(LanguageFloorMimicry.FearCards)
            .ToArray();
        Require(fears.Length == LanguageFloorMimicry.FearCards
                && fears.All(static fear =>
                    fear.Keywords.Contains(CardKeyword.Unplayable)),
            "Generated Terror cards were not unplayable.");
        hpBefore = fight.Player.CurrentHp;
        var choiceContext = new BlockingPlayerChoiceContext();
        foreach (LanguageFloorFearCard fear in fears)
        {
            await fear.OnTurnEndInHandWrapper(choiceContext);
        }

        Require(fight.Player.CurrentHp
                == hpBefore - LanguageFloorFearCard.HpLoss * fears.Length,
            "Terror cards did not independently lose the configured HP.");
        Require(fears.All(static fear =>
                fear.Pile?.Type == PileType.Draw),
            "Terror did not shuffle the same card into the draw pile.");

        await ResetPlayer(fight);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.ExtendArm);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.ExtendArm);
        Require(fight.Player.CurrentHp == 988
                && fight.Player.GetPowerInstances<LibraryWeakPower>()
                    .SingleOrDefault() is
                    {
                        Amount: 3,
                        TurnsRemaining: 3
                    }
                && CountCards<LanguageFloorFearCard>(fight.Player) >= 2,
            "Reaching Hand damage, Weak, or Terror was incorrect.");

        await VerifyFullHandFearFallback(fight);
    }

    private static async Task VerifyFullHandFearFallback(
        PhaseFiveContext fight)
    {
        Player owner = fight.Player.Player
            ?? throw new InvalidOperationException(
                "Verification player owner was null.");
        CardPile hand = PileType.Hand.GetPile(owner);
        if (hand.Cards.Count > 0)
        {
            await CardPileCmd.Add(hand.Cards.ToArray(), PileType.Discard);
        }

        await CardPileCmdCompat.AddToCombatWithoutPreview<StrikeIronclad>(
            fight.Player,
            PileType.Hand,
            CardPile.MaxCardsInHand,
            addedByPlayer: false);
        Require(hand.Cards.Count == CardPile.MaxCardsInHand,
            "Could not fill the verification hand.");
        int fearInDiscardBefore = PileType.Discard.GetPile(owner)
            .Cards.OfType<LanguageFloorFearCard>().Count();
        await ResetPlayer(fight, preserveHand: true);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.EyeContact);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.EyeContact);
        int fearInDiscardAfter = PileType.Discard.GetPile(owner)
            .Cards.OfType<LanguageFloorFearCard>().Count();
        Require(hand.Cards.Count == CardPile.MaxCardsInHand
                && fearInDiscardAfter
                    == fearInDiscardBefore + LanguageFloorMimicry.FearCards
                && hand.Cards.All(static card =>
                    card is StrikeIronclad),
            "Full-hand Terror generation displaced cards or missed "
            + "the native discard fallback.");
        await CardPileCmd.Add(hand.Cards.ToArray(), PileType.Discard);
    }

    private static async Task VerifyNormalEvolution(
        PhaseFiveContext fight)
    {
        await ClearBlock(fight.BossCreature, fight.Player);
        await fight.Boss.DebugTransitionToSecond();
        Require(fight.Boss.Form == LanguageFloorMimicryForm.Second
                && fight.BossCreature.MaxHp == 200
                && fight.BossCreature.CurrentHp == 100,
            "Second-form entry was not 200 max HP and 100 current HP.");
        LibraryCreature libraryCreature =
            RequireLibraryCreature(fight.BossCreature);
        Require(libraryCreature.MaxChaoValue == 200
                && libraryCreature.CurrentChaoValue == 200,
            "Second-form chao resistance was not 200/200.");
        RequireResistance(
            libraryCreature,
            LibraryDamageType.Slash,
            LibraryResistanceLevel.Resist,
            LibraryResistanceLevel.Resist,
            "second form");
        RequireResistance(
            libraryCreature,
            LibraryDamageType.Pierce,
            LibraryResistanceLevel.Normal,
            LibraryResistanceLevel.Normal,
            "second form");
        RequireResistance(
            libraryCreature,
            LibraryDamageType.Blunt,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Endure,
            "second form");
        Require(fight.BossCreature.GetPower<ThornsPower>()?.Amount == 8
                && fight.BossCreature.GetPower<
                    LanguageFloorMimicryFormTwoEvolutionPower>() != null
                && fight.BossCreature.GetPower<
                    LanguageFloorMimicryFormTwoRegenerationPower>() != null,
            "Second-form passive set or fixed Thorns was incorrect.");

        var context = new BlockingPlayerChoiceContext();
        await fight.Boss.AfterSideTurnEnd(
            context,
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        await fight.Boss.BeforeSideTurnStart(
            context,
            CombatSide.Enemy,
            fight.CombatState.Enemies,
            fight.CombatState);
        Require(fight.BossCreature.CurrentHp == 100
                && fight.BossCreature.GetPower<ThornsPower>()?.Amount == 8,
            "Second-form regeneration triggered before the first Cocoon.");

        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.HardCocoon);
        Require(fight.BossCreature.Block == 99,
            "Hard Cocoon did not grant 99 Block.");
        await fight.Boss.AfterSideTurnEnd(
            context,
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        Require(fight.Boss.FormTwoActionsCompleted == 1,
            "First Hard Cocoon was not counted.");

        await fight.Boss.BeforeSideTurnStart(
            context,
            CombatSide.Enemy,
            fight.CombatState.Enemies,
            fight.CombatState);
        Require(fight.BossCreature.CurrentHp == 200,
            "Second-form regeneration did not heal 50% before "
            + "the second Cocoon.");
        await ClearBlock(fight.BossCreature, fight.Player);
        await CreatureCmd.SetCurrentHp(fight.BossCreature, 150m);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.HardCocoon);
        await fight.Boss.AfterSideTurnEnd(
            context,
            CombatSide.Enemy,
            fight.CombatState.Enemies);

        Require(fight.Boss.Form == LanguageFloorMimicryForm.Third
                && fight.Boss.FormTwoActionsCompleted == 2
                && fight.BossCreature.MaxHp == 400
                && fight.BossCreature.CurrentHp == 300,
            "Normal second-to-third transition did not preserve "
            + "the 75% HP ratio.");
        Require(fight.BossCreature.Block == 0
                && fight.BossCreature.GetPower<ThornsPower>() == null
                && fight.BossCreature.GetPower<
                    LanguageFloorMimicryHardenPower>() != null
                && fight.BossCreature.GetPower<
                    LanguageFloorMimicryFormThreeRegenerationPower>()
                    != null
                && fight.Boss.MimicStacks == 0,
            "Third-form transition did not clear old state or install "
            + "the correct passives.");
        var visuals = fight.BossNode.Visuals
            as LanguageFloorMimicryCreatureVisuals
            ?? throw new InvalidOperationException(
                "Mimicry visuals disappeared after third-form transition.");
        Require(visuals.GetNode<Sprite2D>("%Visuals")
                .Position.IsEqualApprox(
                    LanguageFloorMimicryCreatureVisuals.Profile
                        .Variants["third"].Position),
            "Third-form Mimicry did not apply its downward offset.");
        libraryCreature = RequireLibraryCreature(fight.BossCreature);
        Require(libraryCreature.MaxChaoValue == 300
                && libraryCreature.CurrentChaoValue == 300,
            "Third-form chao resistance was not 300/300.");
        RequireResistance(
            libraryCreature,
            LibraryDamageType.Slash,
            LibraryResistanceLevel.Resist,
            LibraryResistanceLevel.Immune,
            "third form");
        RequireResistance(
            libraryCreature,
            LibraryDamageType.Pierce,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Immune,
            "third form");
        RequireResistance(
            libraryCreature,
            LibraryDamageType.Blunt,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Immune,
            "third form");
    }

    private static async Task VerifyFormThreePassives(
        PhaseFiveContext fight)
    {
        await CreatureCmd.SetCurrentHp(fight.BossCreature, 400m);
        await ClearBlock(fight.BossCreature, fight.Player);
        await CreatureCmd.GainBlock(
            fight.BossCreature,
            20m,
            ValueProp.Move,
            null);
        await DealToBoss(fight, 9, ValueProp.Move);
        Require(fight.BossCreature.CurrentHp == 400
                && fight.BossCreature.Block == 20
                && fight.Boss.MimicStacks == 0,
            "Hardness did not nullify 9 damage before Block.");

        await DealToBoss(fight, 10, ValueProp.Move);
        Require(fight.BossCreature.CurrentHp == 400
                && fight.BossCreature.Block == 10
                && fight.Boss.MimicStacks == 0,
            "Hardness incorrectly nullified 10 damage or fully blocked "
            + "damage granted Imitation.");
        await ClearBlock(fight.BossCreature, fight.Player);
        await DealToBoss(fight, 10, ValueProp.Move);
        Require(fight.BossCreature.CurrentHp == 390
                && fight.Boss.MimicStacks == 1,
            "Unblocked 10-damage attack did not deal damage and grant "
            + "one Imitation.");
        await DealToBoss(
            fight,
            9,
            ValueProp.Move | ValueProp.Unpowered
                | ValueProp.Unblockable);
        Require(fight.BossCreature.CurrentHp == 381
                && fight.Boss.MimicStacks == 1,
            "Hardness affected non-attack HP loss.");

        await fight.Boss.DebugGainMimic(50);
        Require(fight.Boss.MimicStacks == 20
                && fight.BossCreature.GetPower<
                    LanguageFloorMimicryMimicPower>()?.Amount == 20,
            "Imitation did not cap at 20.");
        var context = new BlockingPlayerChoiceContext();
        await CreatureCmd.SetCurrentHp(fight.BossCreature, 300m);
        fight.Boss.DebugSetRoundDamageTaken(79);
        await fight.Boss.AfterSideTurnEnd(
            context,
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        Require(fight.BossCreature.CurrentHp == 340
                && fight.Boss.MimicStacks == 19,
            "79 damage did not trigger 40 healing and one "
            + "Imitation decay.");

        await CreatureCmd.SetCurrentHp(fight.BossCreature, 300m);
        fight.Boss.DebugSetRoundDamageTaken(80);
        await fight.Boss.AfterSideTurnEnd(
            context,
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        Require(fight.BossCreature.CurrentHp == 300
                && fight.Boss.MimicStacks == 18,
            "Exactly 80 damage incorrectly triggered regeneration "
            + "or skipped Imitation decay.");
    }

    private static async Task VerifyFormThreeMoves(
        PhaseFiveContext fight)
    {
        await ResetBossOffensivePowers(fight);
        await fight.Boss.DebugSetMimicStacks(0);
        await ResetPlayer(fight);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.Mimic);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.Mimic);
        Require(fight.Player.CurrentHp == 984
                && fight.BossCreature.GetPower<StrengthPower>()?.Amount
                    == 2
                && fight.Boss.LastAttackAnimationTriggers.SequenceEqual(
                    new[] { "AttackStrike", "AttackThrust" }),
            "Follow did not deal 8x2, rotate attacks, and gain 2 "
            + "permanent Strength.");
        RequireLastTargetsAreAllLivingPlayers(fight);

        await ResetBossOffensivePowers(fight);
        await fight.Boss.DebugSetMimicStacks(0);
        await ClearBlock(fight.BossCreature, fight.Player);
        await ResetPlayer(fight);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.Skin);
        Require(!fight.Boss.PlannedMoveEnhanced
                && fight.Boss.MimicStacks == 0,
            "Base Flail Skin was not locked before intent display.");
        await fight.Boss.DebugSetMimicStacks(5);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.Skin);
        Require(fight.Player.CurrentHp == 979
                && fight.BossCreature.Block == 14
                && fight.Boss.MimicStacks == 5
                && !fight.BossCreature
                    .GetPowerInstances<LibraryStrongPower>().Any()
                && fight.Boss.LastAttackAnimationTriggers.SequenceEqual(
                    new[]
                    {
                        "AttackStrike",
                        "AttackThrust",
                        "AttackSlash"
                    }),
            "Base Flail Skin changed after Imitation was gained "
            + "following the intent roll.");

        await ClearBlock(fight.BossCreature, fight.Player);
        await ResetBossOffensivePowers(fight);
        await fight.Boss.DebugSetMimicStacks(5);
        await ResetPlayer(fight);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.Skin);
        Require(fight.Boss.PlannedMoveEnhanced
                && fight.Boss.MimicStacks == 0,
            "Empowered Flail Skin did not consume 5 Imitation "
            + "during the intent roll.");
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.Skin);
        Require(fight.Boss.MimicStacks == 0
                && fight.BossCreature.Block == 14,
            "Empowered Flail Skin did not consume 5 Imitation "
            + "and gain 14 Block.");
        RequireTemporaryStrong(fight.BossCreature, 4);

        await RemoveCards<Burn>(fight.Player);
        await RemovePowers<LibraryOfRuinaConfusionPower>(fight.Player);
        await fight.Boss.DebugSetMimicStacks(0);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.Imitate);
        Require(!fight.Boss.PlannedMoveEnhanced,
            "Base Mimic was incorrectly locked as empowered.");
        await fight.Boss.DebugSetMimicStacks(10);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.Imitate);
        Require(CountCardsInPile<Burn>(
                    fight.Player,
                    PileType.Draw) == 4
                && fight.Player.GetPower<
                    LibraryOfRuinaConfusionPower>()?.Amount == 1
                && fight.Boss.MimicStacks == 10,
            "Base Mimic changed after Imitation was gained "
            + "following the intent roll.");

        await RemoveCards<Burn>(fight.Player);
        await RemovePowers<LibraryOfRuinaConfusionPower>(fight.Player);
        await fight.Boss.DebugSetMimicStacks(10);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.Imitate);
        Require(fight.Boss.PlannedMoveEnhanced
                && fight.Boss.MimicStacks == 0,
            "Empowered Mimic did not consume 10 Imitation "
            + "during the intent roll.");
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.Imitate);
        Require(CountCardsInPile<Burn>(
                    fight.Player,
                    PileType.Draw) == 8
                && fight.Player.GetPower<
                    LibraryOfRuinaConfusionPower>()?.Amount == 2
                && fight.Boss.MimicStacks == 0,
            "Empowered Mimic branch did not double both effects.");

        await ResetBossOffensivePowers(fight);
        await RemovePowers<LibraryVulnerablePower>(fight.Player);
        await fight.Boss.DebugSetMimicStacks(0);
        await ResetPlayer(fight);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.Hello);
        Require(!fight.Boss.PlannedMoveEnhanced,
            "Base hello was incorrectly locked as empowered.");
        await fight.Boss.DebugSetMimicStacks(8);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.Hello);
        Require(fight.Player.CurrentHp == 970
                && !fight.Player
                    .GetPowerInstances<LibraryVulnerablePower>().Any()
                && fight.Boss.MimicStacks == 8,
            "Base hello changed after Imitation was gained "
            + "following the intent roll.");

        await fight.Boss.DebugSetMimicStacks(8);
        await ResetPlayer(fight);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.Hello);
        Require(fight.Boss.PlannedMoveEnhanced
                && fight.Boss.MimicStacks == 0,
            "Empowered hello did not consume 8 Imitation "
            + "during the intent roll.");
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.Hello);
        Require(fight.Player.CurrentHp == 970
                && fight.Boss.MimicStacks == 0,
            "Empowered hello branch did not consume 8 Imitation.");
        RequirePermanentDurationPower<LibraryVulnerablePower>(
            fight.Player,
            5);

        await ResetBossOffensivePowers(fight);
        await RemovePowers<LibraryBleedingPower>(fight.Player);
        await RemovePowers<LibraryVulnerablePower>(fight.Player);
        await fight.Boss.DebugSetMimicStacks(0);
        await ResetPlayer(fight);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.Goodbye);
        Require(!fight.Boss.PlannedMoveEnhanced,
            "Base Goodbye was incorrectly locked as empowered.");
        await fight.Boss.DebugSetMimicStacks(20);
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.Goodbye);
        Require(fight.Player.CurrentHp == 980
                && fight.Player.GetPower<
                    LibraryBleedingPower>()?.Amount == 10
                && fight.Boss.MimicStacks == 20,
            "Base Goodbye changed after Imitation was gained "
            + "following the intent roll.");

        await RemovePowers<LibraryBleedingPower>(fight.Player);
        await fight.Boss.DebugSetMimicStacks(20);
        await ResetPlayer(fight);
        await fight.Boss.DebugPlanMove(
            LanguageFloorMimicryMove.Goodbye);
        Require(fight.Boss.PlannedMoveEnhanced
                && fight.Boss.MimicStacks == 0,
            "Empowered Goodbye did not consume 20 Imitation "
            + "during the intent roll.");
        await fight.Boss.DebugPerformMove(
            LanguageFloorMimicryMove.Goodbye);
        Require(fight.Player.CurrentHp == 940
                && fight.Player.GetPower<
                    LibraryBleedingPower>()?.Amount == 30
                && fight.Boss.MimicStacks == 0,
            "Empowered Goodbye was not one 60-damage hit "
            + "with 30 Bleed.");
    }

    private static void VerifySavedPropertyRoundTrip(
        LanguageFloorMimicry source)
    {
        source.DebugSetMovePlanState(
            LanguageFloorMimicryForm.Third,
            (int)LanguageFloorMimicryMove.Goodbye,
            plannedMoveEnhanced: true,
            plannedTargetCombatId: 42);
        source.DebugSetRoundDamageTaken(80);
        SavedProperties props = SavedProperties.From(source)
            ?? throw new InvalidOperationException(
                "Mimicry SavedProperties were empty.");
        var clone = (LanguageFloorMimicry)ModelDb
            .Monster<LanguageFloorMimicry>()
            .ToMutable();
        props.Fill(clone);
        Require(clone.Form == source.Form
                && clone.Initialized == source.Initialized
                && clone.FormOneMaxHp == source.FormOneMaxHp
                && clone.FormTwoMaxHp == source.FormTwoMaxHp
                && clone.FormThreeMaxHp == source.FormThreeMaxHp
                && clone.FormTwoActionsCompleted
                    == source.FormTwoActionsCompleted
                && clone.PreviousMove == source.PreviousMove
                && clone.EvolutionPending == source.EvolutionPending
                && clone.PendingForm == source.PendingForm
                && clone.MimicStacks == source.MimicStacks
                && clone.RoundDamageTaken == source.RoundDamageTaken
                && clone.PlannedMoveEnhanced
                    == source.PlannedMoveEnhanced
                && clone.PlannedTargetCombatId
                    == source.PlannedTargetCombatId
                && clone.SkipCurrentFormTwoEnemyEnd
                    == source.SkipCurrentFormTwoEnemyEnd
                && clone.SkipCurrentFormThreeEnemyEndRecovery
                    == source.SkipCurrentFormThreeEnemyEndRecovery,
            "Mimicry saved state did not round-trip.");
    }

    private static async Task VerifyLethalEvolutionAndVictory()
    {
        PhaseFiveContext fight = await StartPhaseFiveFight(
            "LANGUAGEFLOORVERIFY_MIMICRY_LETHAL");
        Require(!fight.Encounter.ShouldKeepCombatOpen(fight.CombatState)
                && !fight.Encounter.PhaseComplete
                && !fight.Encounter.SettlementTriggered,
            "Live first-form Mimicry incorrectly held or settled combat.");
        fight.BossCreature.RemoveAllPowersInternalExcept();
        await CreatureCmd.Kill(fight.BossCreature, force: true);
        await WaitUntil(
            () => fight.Boss.EvolutionPending
                && fight.Boss.PendingForm
                    == (int)LanguageFloorMimicryForm.Second,
            "first-form console-win lethal evolution");
        Require(!fight.Encounter.PhaseComplete
                && fight.Encounter.ShouldKeepCombatOpen(
                    fight.CombatState)
                && fight.CombatState.Enemies.Contains(
                    fight.BossCreature)
                && fight.Boss.NextMove.Id
                    == LanguageFloorMimicry.EvolutionMoveId,
            "First-form lethal damage ended combat or removed Mimicry.");
        VerifySavedPropertyRoundTrip(fight.Boss);
        await fight.Boss.PerformMove();
        Require(fight.Boss.Form == LanguageFloorMimicryForm.Second
                && fight.BossCreature.MaxHp == 200
                && fight.BossCreature.CurrentHp == 100,
            "Lethal first-form evolution did not enter second form "
            + "at 100/200 HP.");

        fight.BossCreature.RemoveAllPowersInternalExcept();
        await CreatureCmd.Kill(fight.BossCreature, force: true);
        await WaitUntil(
            () => fight.Boss.EvolutionPending
                && fight.Boss.PendingForm
                    == (int)LanguageFloorMimicryForm.Third,
            "second-form console-win lethal evolution");
        Require(!fight.Encounter.PhaseComplete
                && fight.CombatState.Enemies.Contains(
                    fight.BossCreature),
            "Second-form lethal damage ended combat or removed Mimicry.");
        await fight.Boss.PerformMove();
        Require(fight.Boss.Form == LanguageFloorMimicryForm.Third
                && fight.BossCreature.MaxHp == 400
                && fight.BossCreature.CurrentHp == 280,
            "Lethal second-form evolution did not apply the 70% floor.");

        fight.BossCreature.RemoveAllPowersInternalExcept();
        await CreatureCmd.Kill(fight.BossCreature, force: true);
        await WaitUntil(
            () => fight.Encounter.PhaseComplete,
            "third-form final death");
        await WaitUntil(
            static () => !CombatManager.Instance.IsInProgress,
            "combat end after third-form final death");
        Require(fight.Encounter.CurrentPhase == 5
                && fight.Encounter.PhaseComplete
                && !fight.Encounter.TransitionPending
                && fight.Encounter.KilledBossCount == 5
                && fight.Encounter.SettlementTriggered
                && !fight.Encounter.EndedByLethalDamage
                && LanguageFloorLiberationSettlementStore
                    .PendingSettlement
                && !fight.Boss.EvolutionPending
                && !fight.Encounter.ShouldKeepCombatOpen(
                    fight.CombatState),
            "Third-form death did not complete phase five and queue settlement exactly once.");

        await RunManager.Instance.ProceedFromTerminalRewardsScreen();
        await WaitUntil(
            static () => RunManager.Instance.DebugOnlyGetState()?.CurrentRoom
                is EventRoom
                {
                    CanonicalEvent:
                        LanguageFloorLiberationSettlementEvent
                },
            "Language Floor settlement event room");
        Require(!LanguageFloorLiberationSettlementStore.PendingSettlement,
            "Settlement redirect did not consume the pending state.");
    }

    private static async Task VerifyMissingFinalBossFallback()
    {
        PhaseFiveContext fight = await StartPhaseFiveFight(
            "LANGUAGEFLOORVERIFY_MIMICRY_MISSING");
        await LiberationPhaseCleanup.RemoveTransitionCreature(
            fight.BossCreature,
            fight.CombatState);
        Require(!fight.CombatState.Enemies.Contains(fight.BossCreature)
                && NCombatRoom.Instance?.GetCreatureNode(
                    fight.BossCreature) == null,
            "Could not inject the missing final-boss state.");

        Require(!fight.Encounter.ShouldKeepCombatOpen(fight.CombatState)
                && fight.Encounter.PhaseComplete
                && fight.Encounter.SettlementTriggered
                && fight.Encounter.KilledBossCount == 4
                && LanguageFloorLiberationSettlementStore
                    .PendingSettlement,
            "Missing final boss did not fail open into the earned four-phase settlement.");
        await CombatManager.Instance.CheckWinCondition();
        await WaitUntil(
            static () => !CombatManager.Instance.IsInProgress,
            "combat end after missing final-boss recovery");

        LanguageFloorLiberationSettlementStore.Consume();
        CleanupRun();
        await WaitForRunCleanup(
            "missing final-boss fallback cleanup");
    }

    private static async Task<PhaseFiveContext> StartPhaseFiveFight(
        string seed,
        bool useFakeMultiplayer = false)
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException(
                "NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        if (useFakeMultiplayer)
        {
            await StartFakeMultiplayerRun(game, seed);
        }
        else
        {
            await game.StartNewSingleplayerRun(
                ModelDb.Character<Ironclad>(),
                shouldSave: false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                seed,
                GameMode.Standard,
                ascensionLevel: 0);
        }

        await RemoveBurningBlood();
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        var encounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "5",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False",
            ["KilledBossCount"] = "4",
            ["SettlementTriggered"] = "False",
            ["EndedByLethalDamage"] = "False"
        });
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            encounter,
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress
                && NCombatRoom.Instance != null
                && CombatManager.Instance.DebugOnlyGetState()?.Enemies
                    .Select(static enemy => enemy.Monster)
                    .OfType<LanguageFloorMimicry>()
                    .Count(static boss => boss.Initialized) == 1,
            "direct Language Floor phase-five combat start");
        await WaitFrames(10);

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException(
                "Phase-five combat state was null.");
        LanguageFloorMimicry boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorMimicry>()
            .Single();
        NCreature bossNode = NCombatRoom.Instance!.GetCreatureNode(
                boss.Creature)
            ?? throw new InvalidOperationException(
                "Mimicry visual node was missing.");
        var activeEncounter = combatState.Encounter
            as LanguageFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "Language Floor encounter was not active.");
        return new PhaseFiveContext(
            combatState,
            activeEncounter,
            boss,
            boss.Creature,
            combatState.PlayerCreatures.First(),
            bossNode);
    }

    private static async Task StartFakeMultiplayerRun(
        NGame game,
        string seed)
    {
        Player[] players =
        [
            Player.CreateForNewRun(
                ModelDb.Character<Ironclad>(),
                SaveManager.Instance.GenerateUnlockStateFromProgress(),
                1uL),
            Player.CreateForNewRun(
                ModelDb.Character<Silent>(),
                SaveManager.Instance.GenerateUnlockStateFromProgress(),
                2uL)
        ];
        RunState runState = RunState.CreateForNewRun(
            players,
            ActModel.GetDefaultList()
                .Select(static act => act.ToMutable())
                .ToList(),
            Array.Empty<ModifierModel>(),
            GameMode.Standard,
            ascensionLevel: 0,
            seed);
        RunManager.Instance.SetUpNewSingleplayer(
            runState,
            shouldSave: false);
        MethodInfo startRun = typeof(NGame).GetMethod(
                "StartRun",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "NGame.StartRun was unavailable for fake multiplayer.");
        Task startRunTask = startRun.Invoke(game, [runState]) as Task
            ?? throw new InvalidOperationException(
                "NGame.StartRun did not return a Task.");
        await startRunTask;
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

    private static async Task RemoveBurningBlood()
    {
        RunState runState = RunManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException(
                "Run state was missing.");
        foreach (Player player in runState.Players)
        {
            if (player.GetRelic<BurningBlood>() is { } burningBlood)
            {
                await RelicCmd.Remove(burningBlood);
            }
        }
    }

    private static Task<IEnumerable<DamageResult>> DealToBoss(
        PhaseFiveContext fight,
        int damage,
        ValueProp props) =>
        CreatureCmdCompat.Damage(
            new BlockingPlayerChoiceContext(),
            fight.BossCreature,
            damage,
            props,
            fight.Player,
            null);

    private static async Task ResetPlayer(
        PhaseFiveContext fight,
        bool preserveHand = false)
    {
        await CreatureCmd.SetCurrentHp(fight.Player, 1000m);
        await ClearBlock(fight.Player, fight.BossCreature);
        await RemovePowers<LibraryDisarmPower>(fight.Player);
        await RemovePowers<LibraryWeakPower>(fight.Player);
        if (!preserveHand)
        {
            Player owner = fight.Player.Player!;
            CardModel[] hand = PileType.Hand.GetPile(owner)
                .Cards.ToArray();
            if (hand.Length > 0)
            {
                await CardPileCmd.Add(hand, PileType.Discard);
            }
        }
    }

    private static async Task ResetBossOffensivePowers(
        PhaseFiveContext fight)
    {
        await RemovePowers<StrengthPower>(fight.BossCreature);
        await RemovePowers<LibraryStrongPower>(fight.BossCreature);
    }

    private static async Task ClearBlock(
        Creature creature,
        Creature dealer)
    {
        if (creature.Block > 0)
        {
            await CreatureCmd.LoseBlock(
                new BlockingPlayerChoiceContext(),
                creature,
                creature.Block,
                dealer);
        }
    }

    private static async Task RemovePowers<TPower>(
        Creature creature)
        where TPower : PowerModel
    {
        foreach (TPower power in creature
                     .GetPowerInstances<TPower>()
                     .ToArray())
        {
            await PowerCmd.Remove(power);
        }
    }

    private static async Task RemoveCards<TCard>(
        Creature creature)
        where TCard : CardModel
    {
        Player owner = creature.Player
            ?? throw new InvalidOperationException(
                "Card owner was null.");
        CardModel[] cards = Enum.GetValues<PileType>()
            .Where(static pile => pile.IsCombatPile())
            .SelectMany(pile => pile.GetPile(owner).Cards)
            .OfType<TCard>()
            .Cast<CardModel>()
            .ToArray();
        if (cards.Length > 0)
        {
            await CardPileCmd.Add(cards, PileType.Exhaust);
        }
    }

    private static int CountCards<TCard>(Creature creature)
        where TCard : CardModel
    {
        Player owner = creature.Player
            ?? throw new InvalidOperationException(
                "Card owner was null.");
        return Enum.GetValues<PileType>()
            .Where(static pile => pile.IsCombatPile())
            .Sum(pile => pile.GetPile(owner).Cards
                .OfType<TCard>().Count());
    }

    private static int CountCardsInPile<TCard>(
        Creature creature,
        PileType pileType)
        where TCard : CardModel
    {
        Player owner = creature.Player
            ?? throw new InvalidOperationException(
                "Card owner was null.");
        return pileType.GetPile(owner).Cards
            .OfType<TCard>()
            .Count();
    }

    private static void RequireLastTargetsAreAllLivingPlayers(
        PhaseFiveContext fight)
    {
        HashSet<uint?> livingPlayerIds = fight.CombatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .Select(static player => player.CombatId)
            .ToHashSet();
        Require(livingPlayerIds.Count > 0
                && fight.Boss.LastAttackTargetCombatIds.Count
                    == livingPlayerIds.Count
                && livingPlayerIds.SetEquals(
                    fight.Boss.LastAttackTargetCombatIds),
            "Mimicry attack did not cover every living player.");
        HashSet<uint?> enemyIds = fight.CombatState.Enemies
            .Select(static enemy => enemy.CombatId)
            .ToHashSet();
        Require(!fight.Boss.LastAttackTargetCombatIds
                .Any(enemyIds.Contains),
            "Mimicry attack targeted an enemy-side unit.");
    }

    private static void VerifySceneMarker()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>(
            LanguageFloorLiberationEncounter.EncounterScenePath)
            ?? throw new InvalidOperationException(
                "Language Floor encounter scene was missing.");
        Control root = scene.Instantiate<Control>();
        try
        {
            Marker2D? marker = root.GetNodeOrNull<Marker2D>(
                LanguageFloorLiberationEncounter.MimicryBossSlot);
            Require(marker != null
                    && marker.Position.IsEqualApprox(
                        new Vector2(1600f, 755f)),
                "Mimicry encounter marker was not moved right.");
        }
        finally
        {
            root.Free();
        }
    }

    private static void RequireTemporaryStrong(
        Creature creature,
        decimal expectedAmount)
    {
        LibraryStrongPower? strong = creature
            .GetPowerInstances<LibraryStrongPower>()
            .SingleOrDefault(static power => power.TurnsRemaining > 0);
        Require(strong?.Amount == expectedAmount
                && strong.TurnsRemaining == 1,
            "Temporary Strong was not 4 for one turn.");
    }

    private static void RequirePermanentDurationPower<TPower>(
        Creature creature,
        decimal expectedAmount)
        where TPower : LibraryTurnsPowerModel
    {
        TPower? power = creature.GetPowerInstances<TPower>()
            .SingleOrDefault(static candidate =>
                candidate.AmountPlan.Count == 0);
        Require(power?.Amount == expectedAmount,
            typeof(TPower).Name
            + " permanent amount mismatch.");
    }

    private static LibraryCreature RequireLibraryCreature(
        Creature creature) =>
        creature as LibraryCreature
        ?? throw new InvalidOperationException(
            "Mimicry was not a LibraryCreature.");

    private static void RequireResistance(
        LibraryCreature creature,
        LibraryDamageType type,
        LibraryResistanceLevel physical,
        LibraryResistanceLevel chao,
        string label)
    {
        Require(creature.GetPhysicalResistanceLevel(type) == physical,
            label + " physical resistance mismatch for " + type + ".");
        Require(creature.GetChaosResistanceLevel(type) == chao,
            label + " chao resistance mismatch for " + type + ".");
    }

    private static void RequireBackgroundTexture(
        string expectedPath)
    {
        TextureRect? image = NCombatRoom.Instance?.Background
            .GetNodeOrNull<TextureRect>(
                "%LittleRedMercenaryBackgroundImage")
            ?? NCombatRoom.Instance?.Background.FindChild(
                "LittleRedMercenaryBackgroundImage",
                recursive: true,
                owned: false) as TextureRect;
        Require(image?.Texture?.ResourcePath == expectedPath,
            "Background texture mismatch: "
            + (image?.Texture?.ResourcePath ?? "<null>"));
    }

    private static string[] ExtractTokens(string text) =>
        Regex.Matches(text, @"\{[A-Za-z0-9_]+\}")
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static token => token, StringComparer.Ordinal)
            .ToArray();

    private static void CleanupRun()
    {
        if (RunManager.Instance.DebugOnlyGetState() != null)
        {
            RunManager.Instance.CleanUp(graceful: true);
        }
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
        int maxFrames = 1200)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (predicate())
            {
                return;
            }

            await WaitFrames(1);
        }

        throw new TimeoutException(
            "Timed out waiting for " + description + ".");
    }

    private static async Task WaitFrames(int frames)
    {
        SceneTree tree = NGame.Instance?.GetTree()
            ?? throw new InvalidOperationException(
                "SceneTree is unavailable.");
        for (int frame = 0; frame < frames; frame++)
        {
            await tree.ToSignal(
                tree,
                SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record PhaseFiveContext(
        CombatState CombatState,
        LanguageFloorLiberationEncounter Encounter,
        LanguageFloorMimicry Boss,
        Creature BossCreature,
        Creature Player,
        NCreature BossNode);
}

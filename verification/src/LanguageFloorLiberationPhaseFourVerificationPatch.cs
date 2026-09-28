using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.backgrounds.LanguageFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters.LanguageFloorLiberation;
using LibraryOfRuina.monsters.Nosferatu;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.powers.Nosferatu;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.LanguageFloorLiberation;
using LibraryOfRuina.visuals.Nosferatu;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
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
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class LanguageFloorLiberationPhaseFourVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-language-floor-phase4";
    private const string LogPrefix =
        "[LibraryOfRuina.LanguageFloor.Phase4.Verify] ";
    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
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

    private static async Task RunAsync()
    {
        try
        {
            VerifyStaticDefinitions();
            VerifyLocalization();
            await VerifyPhaseThreeTransition();
            PhaseFourContext fight = await StartPhaseFourFight(
                "LANGUAGEFLOORVERIFY_DIPSIA");
            await VerifyOpeningFormation(fight);
            await VerifySelectionAndNormalMoves(fight);
            await VerifyDetachedCreatureNodeRecovery(fight);
            await VerifyTransformAndTransformedMoves(fight);
            await VerifyBossDeathAndPhaseFiveTransition(fight);
            Log.Info(LogPrefix + "LANGUAGE_FLOOR_PHASE4_OK");
            CleanupRun();
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "LANGUAGE_FLOOR_PHASE4_FAIL\n" + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyStaticDefinitions()
    {
        Require(LanguageFloorDipsia.MaxHp == 350
                && LanguageFloorDipsia.MaxChaoResistance == 120,
            "Dipsia HP/chao constants were not 350/120.");
        Require(LanguageFloorBloodBat.MaxHp == 90
                && LanguageFloorBloodBat.MaxChaoResistance == 70,
            "Phase-four Blood Bat HP/chao constants were not 90/70.");
        Require(LanguageFloorDipsia.TransformHpPercent == 50
                && LanguageFloorDipsia.HydrophobiaBloodThreshold == 4
                && LanguageFloorDipsia.HydrophobiaStrong == 3,
            "Dipsia transform or Hydrophobia constants were incorrect.");
        Require(LanguageFloorBloodBat.HydrophobiaBloodThreshold == 1
                && LanguageFloorBloodBat.HydrophobiaStrong == 2,
            "Phase-four Blood Bat Hydrophobia constants were incorrect.");

        Dictionary<LanguageFloorDipsiaMove, (int Normal, int Deadly)>
            damageValues = new()
            {
                [LanguageFloorDipsiaMove.ElegantDinner] = (18, 20),
                [LanguageFloorDipsiaMove.Thirst] = (11, 13),
                [LanguageFloorDipsiaMove.BloodFeast] = (24, 25),
                [LanguageFloorDipsiaMove.ColdClaws] = (7, 8),
                [LanguageFloorDipsiaMove.ViolentGesture] = (2, 3),
                [LanguageFloorDipsiaMove.ExtremeBloodthirst] = (33, 34)
            };
        foreach ((LanguageFloorDipsiaMove move, var values) in damageValues)
        {
            Require(LanguageFloorDipsia.DebugGetMoveDamage(
                        move,
                        deadlyEnemies: false) == values.Normal
                    && LanguageFloorDipsia.DebugGetMoveDamage(
                        move,
                        deadlyEnemies: true) == values.Deadly,
                move + " did not preserve its normal/DeadlyEnemies pair.");
        }

        Require(LanguageFloorDipsia.ColdClawsHits == 2
                && LanguageFloorDipsia.ViolentGestureHits == 5
                && LanguageFloorDipsia.ColdClawsHealPercent == 20
                && LanguageFloorDipsia.UnbearableThirstConfusion == 4,
            "Transformed move constants were incorrect.");
        Require(LanguageFloorDipsia.GroupBreakSegmentSeconds == 0.55f
                && LanguageFloorDipsia.GroupBlockBreakPauseSeconds == 1f,
            "Group attack segment timing was incorrect.");
        SpriteVisualProfile dipsiaProfile =
            LanguageFloorDipsiaCreatureVisuals.Profile;
        Require(dipsiaProfile.Frames["group_break"].ScaleValue
                    == new Vector2(0.60f, 0.60f)
                && dipsiaProfile.Frames["group_attack"].ScaleValue
                    == new Vector2(0.60f, 0.60f)
                && dipsiaProfile.Frames["group_attack"].NudgeValue
                    == new Vector2(-185f, 105f),
            "Group attack S1/S2 scale was not enlarged.");
        Require(dipsiaProfile.Frames["strike"].ScaleValue
                    == new Vector2(0.72f, 0.72f)
                && dipsiaProfile.Frames["strike"].NudgeValue
                    == new Vector2(-100f, 30f)
                && dipsiaProfile.Frames["slash"].ScaleValue
                    == new Vector2(0.84f, 0.84f)
                && dipsiaProfile.Frames["slash"].NudgeValue
                    == new Vector2(65f, 250f),
            "Transformed strike/slash scale or anchor was incorrect.");

        Require(EncounterBgmController.ResolveLiberationPhaseTrackIndex(4)
                == 1
                && LanguageFloorLiberationEncounter
                    .RolandLiberationBgmTracks[1]
                    .EndsWith(
                        "roland_liberation_phase_2.ogg",
                        StringComparison.Ordinal),
            "Phase four did not map to Language Floor BGM track two.");
        Require(EncounterBgmController.ResolveLiberationPhaseTrackIndex(5)
                == 2
                && LanguageFloorLiberationEncounter
                    .RolandLiberationBgmTracks[2]
                    .EndsWith(
                        "roland_liberation_phase_3.ogg",
                        StringComparison.Ordinal),
            "Phase five did not map to Language Floor BGM track three.");
        Require(LanguageFloorLiberationBackgroundController
                .GetPhaseBackgroundTexturePath(4)
                == LanguageFloorLiberationBackgroundController
                    .PhaseFourTexturePath,
            "Phase four did not map to the Nosferatu background.");

        var encounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        Type[] monsterTypes = encounter.AllPossibleMonsters
            .Select(static monster => monster.GetType())
            .ToArray();
        Require(monsterTypes.Contains(typeof(LanguageFloorDipsia))
                && monsterTypes.Contains(typeof(LanguageFloorBloodBat)),
            "Phase-four monsters were absent from the Language Floor encounter.");
        Require(!encounter.ShouldGiveRewards,
            "Language Floor realization unexpectedly grants rewards.");

        foreach (string path in LanguageFloorDipsia.AssetPathsStatic
                     .Append(
                         LanguageFloorLiberationBackgroundController
                             .PhaseFourTexturePath))
        {
            Require(ResourceLoader.Exists(path),
                "Required phase-four resource was missing: " + path);
        }

        VerifySceneMarkers();

        Type[] savedTypes = SavedPropertiesTypeCacheCompat
            .GetAllModSavedPropertyTypes();
        Require(savedTypes.Contains(typeof(LanguageFloorDipsia)),
            "Dipsia SavedProperties type was not auto-discovered.");
        string schema = SavedPropertiesTypeCacheCompat
            .BuildSchemaFingerprintMaterial();
        Require(schema.Contains(
                    typeof(LanguageFloorDipsia).FullName!,
                    StringComparison.Ordinal)
                && schema.Contains(
                    nameof(LanguageFloorDipsia.IsTransformed),
                    StringComparison.Ordinal)
                && schema.Contains(
                    nameof(LanguageFloorDipsia.TransformPending),
                    StringComparison.Ordinal)
                && schema.Contains(
                    nameof(LanguageFloorDipsia.TransformTriggered),
                    StringComparison.Ordinal)
                && schema.Contains(
                    nameof(LanguageFloorDipsia.PreviousMove),
                    StringComparison.Ordinal),
            "Dipsia save fields were absent from the schema fingerprint.");
    }

    private static void VerifyLocalization()
    {
        string[] locales = ["zhs", "eng", "jpn", "kor"];
        string[] files = ["monsters", "intents", "powers"];
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

        Dictionary<string, string> officialNames = new()
        {
            ["zhs"] = "渴血症",
            ["eng"] = "Dipsia",
            ["jpn"] = "渇き",
            ["kor"] = "갈증"
        };
        Dictionary<string, string[]> officialMoveTitles = new()
        {
            ["zhs"] =
            [
                "高雅的休憩", "精致的晚宴", "干渴", "猩红的餐食", "血宴",
                "阴森的气息", "冷酷的魔爪", "激烈的手势",
                "无法忍受的饥渴", "极度渴血"
            ],
            ["eng"] =
            [
                "Noble Repose", "Elegant Supper", "Thirst",
                "Scarlet Meal", "Banquet of Blood", "Looming Presence",
                "Merciless Gesture", "Violent Gesture",
                "Unbearable Drought", "Lust for Blood"
            ],
            ["jpn"] =
            [
                "高潔な休息", "上品な食事", "渇き", "鮮紅色の糧", "血の宴",
                "不気味な気配", "無慈悲な手つき", "激しい手つき",
                "耐え難い渇き", "血の渇望"
            ],
            ["kor"] =
            [
                "고결한 휴식", "품위 있는 식사", "갈증", "선홍빛 양식",
                "피의 연회", "섬뜩한 기운", "무자비한 손길", "격한 손짓",
                "참을 수 없는 갈증", "피의 갈망"
            ]
        };
        string[] moveIds =
        [
            LanguageFloorDipsia.GracefulRestMoveId,
            LanguageFloorDipsia.ElegantDinnerMoveId,
            LanguageFloorDipsia.ThirstMoveId,
            LanguageFloorDipsia.CrimsonMealMoveId,
            LanguageFloorDipsia.BloodFeastMoveId,
            LanguageFloorDipsia.OminousAuraMoveId,
            LanguageFloorDipsia.ColdClawsMoveId,
            LanguageFloorDipsia.ViolentGestureMoveId,
            LanguageFloorDipsia.UnbearableThirstMoveId,
            LanguageFloorDipsia.ExtremeBloodthirstMoveId
        ];

        foreach (string locale in locales)
        {
            Dictionary<string, string> monsters =
                documents[(locale, "monsters")];
            Require(monsters["LANGUAGE_FLOOR_DIPSIA.name"]
                    == officialNames[locale],
                locale + " Dipsia name was not the official name.");
            for (int i = 0; i < moveIds.Length; i++)
            {
                string key =
                    $"LANGUAGE_FLOOR_DIPSIA.moves.{moveIds[i]}.title";
                Require(monsters[key] == officialMoveTitles[locale][i],
                    locale + " move title mismatch: " + key);
            }
        }

        foreach (string file in files)
        {
            HashSet<string> keys = documents[("zhs", file)].Keys
                .Where(static key =>
                    key.StartsWith(
                        "LANGUAGE_FLOOR_DIPSIA",
                        StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
            foreach (string locale in locales.Skip(1))
            {
                HashSet<string> localeKeys = documents[(locale, file)].Keys
                    .Where(static key =>
                        key.StartsWith(
                            "LANGUAGE_FLOOR_DIPSIA",
                            StringComparison.Ordinal))
                    .ToHashSet(StringComparer.Ordinal);
                Require(keys.SetEquals(localeKeys),
                    locale + "/" + file
                    + " Dipsia localization keys were not aligned.");
            }

            foreach (string key in keys)
            {
                string[] expectedTokens = ExtractTokens(
                    documents[("zhs", file)][key]);
                foreach (string locale in locales.Skip(1))
                {
                    Require(expectedTokens.SequenceEqual(ExtractTokens(
                            documents[(locale, file)][key])),
                        locale + "/" + file + " token mismatch: " + key);
                }
            }
        }
    }

    private static async Task VerifyPhaseThreeTransition()
    {
        PhaseThreeTransitionContext context =
            await StartPhaseThreeFightForTransition();
        await context.Encounter.CompletePhaseThree(
            context.Boss,
            context.CombatState);
        Require(context.Encounter.CurrentPhase == 4
                && context.Encounter.TransitionPending
                && !context.Encounter.PhaseComplete,
            "Phase-three completion did not queue phase four.");
        Require(context.CombatState.Enemies.Contains(context.Boss.Creature),
            "Smiling Face was not retained for its transition intent.");
        Require(context.Boss.NextMove.Id
                == LanguageFloorSmilingFace.ReviveAndEmpowerMoveId,
            "Smiling Face did not expose the forced phase-transition intent.");
        Require(!context.CombatState.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<LanguageFloorDipsia>()
                .Any(),
            "Phase four spawned before Smiling Face performed its transition turn.");

        await context.Boss.PerformMove();
        await WaitUntil(
            () => !context.Encounter.TransitionPending
                && context.CombatState.Enemies
                    .Select(static enemy => enemy.Monster)
                    .OfType<LanguageFloorDipsia>()
                    .Count() == 1
                && context.CombatState.Enemies
                    .Select(static enemy => enemy.Monster)
                    .OfType<LanguageFloorBloodBat>()
                    .Count() == 2,
            "phase-four spawn after Smiling Face transition");
        Require(!context.CombatState.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<LanguageFloorSmilingFace>()
                .Any()
                && !context.CombatState.Enemies
                    .Select(static enemy => enemy.Monster)
                    .OfType<LanguageFloorMeltingCorpse>()
                    .Any(),
            "Phase-four transition left Smiling Face or corpses behind.");
        RequireBackgroundTexture(
            LanguageFloorLiberationBackgroundController.PhaseFourTexturePath);
        CleanupRun();
        await WaitForRunCleanup("phase-three to phase-four cleanup");
    }

    private static async Task VerifyOpeningFormation(PhaseFourContext fight)
    {
        Require(fight.Encounter.CurrentPhase == 4
                && !fight.Encounter.PhaseComplete
                && !fight.Encounter.TransitionPending,
            "Direct phase-four save recovery was not active.");
        Dictionary<string, string> state = fight.Encounter.SaveCustomState();
        var clone = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        clone.LoadCustomState(state);
        Require(clone.CurrentPhase == 4
                && !clone.PhaseComplete
                && !clone.TransitionPending,
            "Phase-four encounter state did not save/load.");

        Require(fight.CombatState.Enemies.Count(static enemy =>
                    enemy.IsAlive) == 3
                && fight.BossCreature.SlotName
                    == LanguageFloorLiberationEncounter.DipsiaBossSlot
                && fight.LeftBat.Creature.SlotName
                    == LanguageFloorLiberationEncounter.DipsiaLeftBatSlot
                && fight.RightBat.Creature.SlotName
                    == LanguageFloorLiberationEncounter.DipsiaRightBatSlot,
            "Phase-four opening formation or slots were incorrect.");
        Require(fight.BossCreature.MaxHp == 350
                && fight.BossCreature.CurrentHp == 350,
            "Dipsia did not start at fixed 350/350 HP.");
        Require(fight.LeftBat.Creature.MaxHp == 90
                && fight.LeftBat.Creature.CurrentHp == 90
                && fight.RightBat.Creature.MaxHp == 90
                && fight.RightBat.Creature.CurrentHp == 90,
            "Phase-four Blood Bats did not start at fixed 90/90 HP.");

        LibraryCreature dipsia = RequireLibraryCreature(
            fight.BossCreature,
            "Dipsia");
        Require(dipsia.MaxChaoValue == 120
                && dipsia.CurrentChaoValue == 120,
            "Dipsia chao resistance was not 120/120.");
        VerifyResistancePattern(dipsia, "Dipsia");
        foreach (LanguageFloorBloodBat bat in new[]
                 {
                     fight.LeftBat,
                     fight.RightBat
                 })
        {
            LibraryCreature libraryBat = RequireLibraryCreature(
                bat.Creature,
                "phase-four Blood Bat");
            Require(libraryBat.MaxChaoValue == 70
                    && libraryBat.CurrentChaoValue == 70,
                "Phase-four Blood Bat chao resistance was not 70/70.");
            Require(libraryBat.GetPhysicalResistanceLevel(
                        LibraryDamageType.Slash)
                    == LibraryResistanceLevel.Normal
                    && libraryBat.GetPhysicalResistanceLevel(
                        LibraryDamageType.Pierce)
                    == LibraryResistanceLevel.Normal
                    && libraryBat.GetPhysicalResistanceLevel(
                        LibraryDamageType.Blunt)
                    == LibraryResistanceLevel.Endure
                    && libraryBat.GetChaosResistanceLevel(
                        LibraryDamageType.Slash)
                    == LibraryResistanceLevel.Normal
                    && libraryBat.GetChaosResistanceLevel(
                        LibraryDamageType.Pierce)
                    == LibraryResistanceLevel.Normal
                    && libraryBat.GetChaosResistanceLevel(
                        LibraryDamageType.Blunt)
                    == LibraryResistanceLevel.Endure,
                "Phase-four Blood Bat did not reuse Blood Bat resistances.");
            Require(bat is NosferatuBloodBatBase
                    && bat.Creature.GetPower<MinionPower>() != null
                    && bat.Creature.GetPower<
                        LanguageFloorDipsiaHydrophobiaPassivePower>() != null,
                "Phase-four Blood Bat did not reuse shared behavior/resources.");
        }

        await EnsureHydrophobiaTriggered(
            fight.Boss,
            fight.CombatState);
        await EnsureHydrophobiaTriggered(
            fight.LeftBat,
            fight.CombatState);
        RequireTemporaryStrong(
            fight.BossCreature,
            LanguageFloorDipsia.HydrophobiaStrong);
        RequireTemporaryStrong(
            fight.LeftBat.Creature,
            LanguageFloorBloodBat.HydrophobiaStrong);

        RequireBackgroundTexture(
            LanguageFloorLiberationBackgroundController.PhaseFourTexturePath);
        Require(fight.BossNode.Visuals
                is LanguageFloorDipsiaCreatureVisuals,
            "Dipsia did not use its scripted sprite visuals.");
        var visuals =
            (LanguageFloorDipsiaCreatureVisuals)fight.BossNode.Visuals!;
        RequireTriggerTexture(
            visuals,
            "AttackFire",
            LanguageFloorDipsia.FireTexturePath);
        RequireAnchoredTrigger(
            visuals,
            "AttackStrike",
            LanguageFloorDipsia.StrikeTexturePath,
            LanguageFloorDipsiaCreatureVisuals.Profile
                .Frames["strike"].ScaleValue!.Value,
            LanguageFloorDipsiaCreatureVisuals.Profile
                .Frames["strike"].NudgeValue);
        RequireAnchoredTrigger(
            visuals,
            "AttackSlash",
            LanguageFloorDipsia.SlashTexturePath,
            LanguageFloorDipsiaCreatureVisuals.Profile
                .Frames["slash"].ScaleValue!.Value,
            LanguageFloorDipsiaCreatureVisuals.Profile
                .Frames["slash"].NudgeValue);
        RequireAnchoredTrigger(
            visuals,
            "GroupBreak",
            LanguageFloorDipsia.GroupBreakTexturePath,
            LanguageFloorDipsiaCreatureVisuals.Profile
                .Frames["group_break"].ScaleValue!.Value,
            Vector2.Zero);
        RequireAnchoredTrigger(
            visuals,
            "GroupAttack",
            LanguageFloorDipsia.GroupAttackTexturePath,
            LanguageFloorDipsiaCreatureVisuals.Profile
                .Frames["group_attack"].ScaleValue!.Value,
            LanguageFloorDipsiaCreatureVisuals.Profile
                .Frames["group_attack"].NudgeValue);
        RequireTriggerTexture(
            visuals,
            "Cast",
            LanguageFloorDipsia.EvadeTexturePath);
        RequireTriggerTexture(
            visuals,
            "Hit",
            LanguageFloorDipsia.HitTexturePath);
        Require(NCombatRoom.Instance?.GetCreatureNode(
                    fight.LeftBat.Creature)?.Visuals
                is BloodBatCreatureVisuals,
            "Phase-four Blood Bat did not reuse Blood Bat visuals.");
        await WaitFrames(60);
        Sprite2D idle = visuals.GetNode<Sprite2D>("%Visuals");
        Sprite2D attack = visuals.GetNode<Sprite2D>("%AttackVisuals");
        Require(idle.Visible
                && idle.Texture?.ResourcePath
                    == LanguageFloorDipsia.IdleTexturePath
                && !attack.Visible,
            "Dipsia action visuals did not restore the idle sprite.");
    }

    private static async Task VerifySelectionAndNormalMoves(
        PhaseFourContext fight)
    {
        Require(!fight.Boss.DebugCanUseGracefulRest()
                && !fight.Boss.DebugEligibleMoveIds()
                    .Contains(LanguageFloorDipsia.GracefulRestMoveId),
            "Noble Repose was eligible while a Blood Bat was alive.");
        var rng = new Rng(0xD1A51AUL);
        string? previous = null;
        for (int i = 0; i < 40; i++)
        {
            string selected = fight.Boss.DebugChooseMove(rng);
            Require(selected != previous,
                "Dipsia selected the same move consecutively: " + selected);
            Require(selected != LanguageFloorDipsia.GracefulRestMoveId,
                "Dipsia selected Noble Repose while Blood Bats lived.");
            previous = selected;
        }

        Require(fight.Boss.DebugGetPrimaryIntent(
                    LanguageFloorDipsiaMove.ElegantDinner)
                is CombinedAttackBuffIntent
                && fight.Boss.DebugGetPrimaryIntent(
                    LanguageFloorDipsiaMove.Thirst)
                is CombinedAttackDebuffIntent
                && fight.Boss.DebugGetPrimaryIntent(
                    LanguageFloorDipsiaMove.ViolentGesture)
                is MultiAttackIntent
                && fight.Boss.DebugGetPrimaryIntent(
                    LanguageFloorDipsiaMove.BloodFeast)
                is IndiscriminateAttackIntent
                && fight.Boss.DebugGetPrimaryIntent(
                    LanguageFloorDipsiaMove.ExtremeBloodthirst)
                is IndiscriminateAttackIntent,
            "Ordinary/group attack intent types were incorrect.");

        await RemovePhaseBats(fight.CombatState);
        Require(fight.Boss.DebugCanUseGracefulRest()
                && fight.Boss.DebugEligibleMoveIds()
                    .Contains(LanguageFloorDipsia.GracefulRestMoveId),
            "Noble Repose was not eligible after all Blood Bats died.");

        decimal bossBlockBefore = fight.BossCreature.Block;
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.GracefulRest);
        LanguageFloorBloodBat[] newBats = LivingPhaseBats(
            fight.CombatState);
        Require(newBats.Length == 2
                && newBats.All(static bat =>
                    bat.Creature.CurrentHp == 90
                    && bat.Creature.MaxHp == 90),
            "Noble Repose did not summon two full-status Blood Bats.");
        Require(fight.BossCreature.Block
                == bossBlockBefore + LanguageFloorDipsia.GracefulRestBlock,
            "Noble Repose did not grant Dipsia 25 Block.");
        foreach (Creature enemy in fight.CombatState.Enemies
                     .Where(static enemy => enemy.IsAlive))
        {
            Require(enemy.GetPower<StrengthPower>()?.Amount
                    == LanguageFloorDipsia.GracefulRestStrength,
                "Noble Repose did not permanently give every enemy 2 Strength.");
        }

        await RemovePowers<StrengthPower>(fight.BossCreature);
        await RemovePowers<LibraryStrongPower>(fight.BossCreature);
        await ResetCreatureHpAndBlock(
            fight.Player,
            fight.BossCreature);
        decimal playerHpBefore = fight.Player.CurrentHp;
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.ElegantDinner);
        Require(fight.Player.CurrentHp == playerHpBefore - 18,
            "Elegant Supper did not deal its A0 18 damage.");
        Require(NosferatuBloodPower.GetStacks(fight.BossCreature) == 1,
            "Elegant Supper did not gain exactly 1 Blood after unblocked damage.");
        RequireLastTargetsAreLivingPlayers(fight);

        await ResetCreatureHpAndBlock(
            fight.Player,
            fight.BossCreature);
        await CreatureCmd.GainBlock(
            fight.Player,
            99,
            ValueProp.Move,
            null);
        int woundsBefore = CardPile
            .GetCards(fight.Player.Player!, PileType.Discard)
            .OfType<Wound>()
            .Count();
        await fight.Boss.DebugPerformMove(LanguageFloorDipsiaMove.Thirst);
        Require(fight.Player.CurrentHp == fight.Player.MaxHp,
            "Thirst dealt HP damage through sufficient Block.");
        Require(fight.Player.GetPower<StrengthPower>()?.Amount == -1
                && fight.Player.GetPower<DexterityPower>()?.Amount == -1,
            "Thirst did not permanently remove 1 Strength and Dexterity.");
        Require(CardPile.GetCards(
                    fight.Player.Player!,
                    PileType.Discard)
                .OfType<Wound>()
                .Count() == woundsBefore + 3,
            "Thirst did not add 3 Wounds to the discard pile.");
        RequireLastTargetsAreLivingPlayers(fight);
        await RemovePowers<StrengthPower>(fight.Player);
        await RemovePowers<DexterityPower>(fight.Player);

        Dictionary<Creature, int> blockBefore = fight.CombatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .ToDictionary(
                static enemy => enemy,
                static enemy => enemy.Block);
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.CrimsonMeal);
        foreach ((Creature enemy, int oldBlock) in blockBefore)
        {
            Require(enemy.Block
                    == oldBlock + LanguageFloorDipsia.CrimsonMealBlock,
                "Scarlet Meal did not grant every enemy 12 Block.");
            RequirePermanentDurationPower<LibraryProtectionPower>(
                enemy,
                LanguageFloorDipsia.CrimsonMealProtection);
            RequirePermanentDurationPower<LibraryEndurancePower>(
                enemy,
                LanguageFloorDipsia.CrimsonMealEndurance);
        }

        Dictionary<Creature, int> blockAfterFirstMeal = blockBefore.Keys
            .ToDictionary(
                static enemy => enemy,
                static enemy => enemy.Block);
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.CrimsonMeal);
        foreach ((Creature enemy, int oldBlock) in blockAfterFirstMeal)
        {
            Require(enemy.Block
                    == oldBlock + LanguageFloorDipsia.CrimsonMealBlock,
                "Scarlet Meal did not grant another 12 Block.");
            RequirePermanentDurationPower<LibraryProtectionPower>(
                enemy,
                LanguageFloorDipsia.CrimsonMealProtection * 2);
            RequirePermanentDurationPower<LibraryEndurancePower>(
                enemy,
                LanguageFloorDipsia.CrimsonMealEndurance * 2);
        }

        await ResetCreatureHpAndBlock(
            fight.Player,
            fight.BossCreature);
        await CreatureCmd.GainBlock(
            fight.Player,
            10,
            ValueProp.Move,
            null);
        await NosferatuBloodPower.Change(
            fight.BossCreature,
            2,
            fight.BossCreature);
        int s1Before = fight.Boss.GroupBreakTriggerCount;
        int s2Before = fight.Boss.GroupAttackTriggerCount;
        playerHpBefore = fight.Player.CurrentHp;
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.BloodFeast);
        Require(fight.Boss.GroupBreakTriggerCount == s1Before + 1
                && fight.Boss.GroupAttackTriggerCount == s2Before + 1
                && fight.Boss.LastGroupAttackBrokeBlock,
            "Banquet of Blood did not play S1, break Block, then play S2.");
        Require(fight.Player.CurrentHp == playerHpBefore - 19,
            "Banquet of Blood block-break result was not 19 HP damage:"
            + " hpBefore=" + playerHpBefore
            + " hpAfter=" + fight.Player.CurrentHp
            + " blockAfter=" + fight.Player.Block
            + ".");
        Require(NosferatuBloodPower.GetStacks(fight.BossCreature) == 0,
            "Banquet of Blood did not remove all Blood.");
        RequireLastTargetsAreLivingPlayers(fight);
    }

    private static async Task VerifyTransformAndTransformedMoves(
        PhaseFourContext fight)
    {
        await ClearBlock(fight.BossCreature, fight.Player);
        await LibraryPowerCmd.Apply<LibraryProtectionPower>(
            fight.BossCreature,
            LanguageFloorDipsia.CrimsonMealProtection * 3,
            turns: -1,
            fight.BossCreature,
            null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            fight.BossCreature,
            LanguageFloorDipsia.CrimsonMealEndurance * 3,
            turns: -1,
            fight.BossCreature,
            null);
        await LibraryCreatureCmd.SetCurrentChaoValue(
            RequireLibraryCreature(fight.BossCreature, "Dipsia"),
            37m);
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            fight.CombatState.Enemies
                .Where(static enemy => enemy.IsAlive)
                .ToList(),
            999m,
            ValueProp.Unpowered,
            null,
            null,
            null);
        Require(fight.BossCreature.CurrentHp == 175
                && fight.Boss.TransformPending
                && fight.Boss.TransformTriggered
                && !fight.Boss.IsTransformed,
            "Protection/Endurance left Dipsia stuck above the 50% transform threshold.");
        AccessTools.Property(
                typeof(LanguageFloorDipsia),
                nameof(LanguageFloorDipsia.TransformPending))
            .SetValue(fight.Boss, false);
        await RemovePowers<LibraryProtectionPower>(fight.BossCreature);
        await RemovePowers<LibraryEndurancePower>(fight.BossCreature);
        await LibraryCreatureCmd.SetCurrentChaoValue(
            RequireLibraryCreature(fight.BossCreature, "Dipsia"),
            37m);
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            fight.CombatState.Enemies
                .Where(static enemy => enemy.IsAlive)
                .ToList(),
            999m,
            ValueProp.Unpowered,
            null,
            null,
            null);
        Require(fight.BossCreature.CurrentHp == 175
                && fight.Boss.TransformPending
                && fight.Boss.TransformTriggered
                && !fight.Boss.IsTransformed,
            "Post-resistance damage did not clamp Dipsia at 50% HP.");
        AccessTools.Property(
                typeof(LanguageFloorDipsia),
                nameof(LanguageFloorDipsia.TransformPending))
            .SetValue(fight.Boss, false);
        Require(!fight.Boss.TransformPending,
            "Transform recovery setup could not clear the pending flag.");
        await CreatureCmdCompat.Damage(
            new ThrowingPlayerChoiceContext(),
            fight.BossCreature,
            999m,
            ValueProp.Move,
            fight.Player,
            null);
        Require(fight.BossCreature.CurrentHp == 175
                && fight.Boss.TransformPending,
            "At-threshold damage did not recover the pending transform.");
        AccessTools.Property(
                typeof(LanguageFloorDipsia),
                nameof(LanguageFloorDipsia.TransformPending))
            .SetValue(fight.Boss, false);

        fight.CombatState.RoundNumber++;
        await Hook.BeforeSideTurnStart(
            fight.CombatState,
            CombatSide.Player,
            fight.CombatState.PlayerCreatures);
        Require(fight.Boss.IsTransformed
                && !fight.Boss.TransformPending
                && fight.Boss.TransformTriggered
                && fight.Boss.PreviousMove
                    >= (int)LanguageFloorDipsiaMove.OminousAura
                && fight.Boss.PreviousMove
                    <= (int)LanguageFloorDipsiaMove.ExtremeBloodthirst,
            "Dipsia did not transform, clear its normal-form history,"
            + " and plan a transformed move: transformed="
            + fight.Boss.IsTransformed
            + " pending=" + fight.Boss.TransformPending
            + " previousMove=" + fight.Boss.PreviousMove
            + ".");
        Require(fight.BossCreature.CurrentHp == 175
                && RequireLibraryCreature(
                    fight.BossCreature,
                    "Dipsia").CurrentChaoValue == 37,
            "Transformation healed HP or reset chao resistance.");
        Require(LivingPhaseBats(fight.CombatState).Length == 0,
            "Transformation did not kill every phase-four Blood Bat.");
        Require(NosferatuBloodPower.GetStacks(fight.BossCreature) == 4
                && fight.BossCreature.GetPower<
                    NosferatuFlowingBloodPower>() != null
                && fight.BossCreature.GetPower<
                    LanguageFloorDipsiaTransformPower>() == null,
            "Transform did not gain 5 Blood then lose 1 Flowing Blood.");
        RequireTemporaryStrong(
            fight.BossCreature,
            LanguageFloorDipsia.HydrophobiaStrong);

        SavedProperties saved = SavedProperties.From(fight.Boss)
            ?? throw new InvalidOperationException(
                "Dipsia produced no SavedProperties.");
        var clone = (LanguageFloorDipsia)ModelDb
            .Monster<LanguageFloorDipsia>()
            .ToMutable();
        saved.Fill(clone);
        Require(clone.IsTransformed
                && !clone.TransformPending
                && clone.TransformTriggered
                && clone.PreviousMove == fight.Boss.PreviousMove,
            "Dipsia transformed save state did not round-trip.");

        await RemovePowers<LibraryStrongPower>(fight.BossCreature);
        await ClearBlock(fight.BossCreature, fight.Player);
        await RemovePowers<LibraryWeakPower>(fight.Player);
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.OminousAura);
        Require(fight.BossCreature.Block
                == LanguageFloorDipsia.OminousAuraBlock,
            "Looming Presence did not grant 19 Block.");
        RequirePermanentDurationPower<LibraryWeakPower>(
            fight.Player,
            LanguageFloorDipsia.OminousAuraWeak);

        await ResetCreatureHpAndBlock(
            fight.Player,
            fight.BossCreature,
            clearBossBlock: false);
        decimal bossHpBefore = fight.BossCreature.CurrentHp;
        decimal playerHpBefore = fight.Player.CurrentHp;
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.ColdClaws);
        Require(fight.Player.CurrentHp == playerHpBefore - 14,
            "Merciless Gesture did not deal 7 damage twice.");
        Require(fight.BossCreature.CurrentHp == bossHpBefore + 70,
            "Merciless Gesture did not heal 20% max HP exactly once.");
        Require(fight.Boss.LastAttackAnimationTriggers.SequenceEqual(
                new[] { "AttackStrike", "AttackSlash" }),
            "Merciless Gesture did not alternate strike/slash per hit.");
        RequireLastTargetsAreLivingPlayers(fight);

        await ResetCreatureHpAndBlock(
            fight.Player,
            fight.BossCreature,
            clearBossBlock: false);
        playerHpBefore = fight.Player.CurrentHp;
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.ViolentGesture);
        Require(fight.Player.CurrentHp == playerHpBefore - 10,
            "Violent Gesture did not deal 2 damage five times.");
        Require(fight.Boss.LastAttackAnimationTriggers.SequenceEqual(
                new[]
                {
                    "AttackSlash",
                    "AttackStrike",
                    "AttackSlash",
                    "AttackStrike",
                    "AttackSlash"
                }),
            "Violent Gesture did not alternate slash/strike per hit.");
        RequireLastTargetsAreLivingPlayers(fight);

        await RemovePowers<LibraryOfRuinaConfusionPower>(fight.Player);
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.UnbearableThirst);
        Require(fight.Player.GetPower<
                    LibraryOfRuinaConfusionPower>()?.Amount == 4,
            "Unbearable Drought did not apply 4 Confusion.");

        await ResetCreatureHpAndBlock(
            fight.Player,
            fight.BossCreature,
            clearBossBlock: false);
        int s1Before = fight.Boss.GroupBreakTriggerCount;
        int s2Before = fight.Boss.GroupAttackTriggerCount;
        playerHpBefore = fight.Player.CurrentHp;
        await fight.Boss.DebugPerformMove(
            LanguageFloorDipsiaMove.ExtremeBloodthirst);
        Require(fight.Boss.GroupBreakTriggerCount == s1Before + 1
                && fight.Boss.GroupAttackTriggerCount == s2Before + 1
                && !fight.Boss.LastGroupAttackBrokeBlock,
            "Lust for Blood did not play S1->S2 without existing Block.");
        Require(fight.Player.CurrentHp == playerHpBefore - 33,
            "Lust for Blood did not deal its A0 33 damage.");
        RequireLastTargetsAreLivingPlayers(fight);
    }

    private static async Task VerifyDetachedCreatureNodeRecovery(
        PhaseFourContext fight)
    {
        NCombatRoom room = NCombatRoom.Instance
            ?? throw new InvalidOperationException(
                "NCombatRoom.Instance is null.");
        NCreature originalNode = room.GetCreatureNode(fight.BossCreature)
            ?? throw new InvalidOperationException(
                "Dipsia node was missing before fault injection.");
        decimal hpBefore = fight.BossCreature.CurrentHp;
        int enemyCountBefore = fight.CombatState.Enemies.Count;

        room.RemoveCreatureNode(originalNode);
        originalNode.ToggleIsInteractable(on: false);
        originalNode.Visible = false;
        originalNode.QueueFreeSafely();
        Require(room.GetCreatureNode(fight.BossCreature) == null
                && fight.BossCreature.IsAlive
                && fight.CombatState.Enemies.Contains(
                    fight.BossCreature),
            "Fault injection did not create a live state creature with no node.");

        await Hook.BeforeSideTurnStart(
            fight.CombatState,
            CombatSide.Enemy,
            fight.CombatState.Enemies);
        await WaitUntil(
            () => room.GetCreatureNode(fight.BossCreature) != null,
            "Dipsia node recovery");

        NCreature restoredNode = room.GetCreatureNode(fight.BossCreature)!;
        Require(restoredNode != originalNode
                && GodotObject.IsInstanceValid(restoredNode)
                && fight.BossCreature.CurrentHp == hpBefore
                && fight.CombatState.Enemies.Count == enemyCountBefore
                && fight.Encounter.CurrentPhase == 4
                && !fight.Encounter.TransitionPending,
            "Live Dipsia node recovery changed encounter state or failed to rebuild the node.");
    }

    private static async Task VerifyBossDeathAndPhaseFiveTransition(
        PhaseFourContext fight)
    {
        foreach ((string slot, _) in new[]
                 {
                     (
                         LanguageFloorLiberationEncounter.DipsiaLeftBatSlot,
                         0
                     ),
                     (
                         LanguageFloorLiberationEncounter.DipsiaRightBatSlot,
                         0
                     )
                 })
        {
            Creature bat = await CreatureCmd.Add(
                ModelDb.Monster<LanguageFloorBloodBat>().ToMutable(),
                fight.CombatState,
                CombatSide.Enemy,
                slot);
            bat.PrepareForNextTurn(fight.CombatState.PlayerCreatures);
        }

        Require(LivingPhaseBats(fight.CombatState).Length == 2,
            "Could not set up residual bats for phase-five cleanup.");
        await CreatureCmd.Kill(fight.BossCreature, force: true);
        await WaitUntil(
            () => fight.Encounter.CurrentPhase == 5
                && fight.Encounter.TransitionPending,
            "Dipsia death phase-five queue");
        Require(!fight.Encounter.PhaseComplete
                && fight.Encounter.ShouldKeepCombatOpen(
                    fight.CombatState),
            "Dipsia death incorrectly completed the encounter.");
        Require(fight.CombatState.Enemies.Contains(fight.BossCreature),
            "Dipsia was not retained for its transition intent.");
        Require(fight.Boss.NextMove.Id
                == LanguageFloorDipsia.ReviveAndEmpowerMoveId,
            "Dipsia did not expose the forced phase-transition intent.");
        Require(!fight.CombatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorBloodBat>()
            .Any(),
            "Dipsia death did not clear residual Blood Bats.");
        Require(!fight.CombatState.Enemies
                .Select(static enemy => enemy.Monster)
                .OfType<LanguageFloorMimicry>()
                .Any(),
            "Mimicry spawned before Dipsia performed its transition turn.");

        Dictionary<string, string> saved =
            fight.Encounter.SaveCustomState();
        Require(saved["CurrentPhase"] == "5"
                && saved["PhaseComplete"] == bool.FalseString
                && saved["TransitionPending"] == bool.TrueString,
            "Queued phase-five encounter state did not save correctly.");

        await fight.Boss.PerformMove();
        await WaitUntil(
            () => !fight.Encounter.TransitionPending
                && fight.CombatState.Enemies
                    .Select(static enemy => enemy.Monster)
                    .OfType<LanguageFloorMimicry>()
                    .Count() == 1,
            "phase-five spawn after Dipsia transition");
        Require(!fight.Encounter.PhaseComplete
                && !fight.CombatState.Enemies.Contains(
                    fight.BossCreature),
            "Phase-five transition completed combat or retained Dipsia.");
        RequireBackgroundTexture(
            LanguageFloorLiberationBackgroundController.PhaseFiveTexturePath);
    }

    private static async Task<PhaseThreeTransitionContext>
        StartPhaseThreeFightForTransition()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "LANGUAGEFLOORVERIFY_PHASE3_TO_PHASE4",
            GameMode.Standard,
            ascensionLevel: 0);
        await RemoveBurningBlood();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        var encounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "3",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False"
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
                    .OfType<LanguageFloorSmilingFace>()
                    .Any(static boss => boss.Initialized) == true,
            "direct phase-three Smiling Face start");
        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException(
                "Phase-three transition combat state was null.");
        LanguageFloorSmilingFace boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorSmilingFace>()
            .Single();
        var activeEncounter = combatState.Encounter
            as LanguageFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "Language Floor encounter was not active.");
        return new PhaseThreeTransitionContext(
            combatState,
            activeEncounter,
            boss);
    }

    private static async Task<PhaseFourContext> StartPhaseFourFight(
        string seed)
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            ascensionLevel: 0);
        await RemoveBurningBlood();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        var encounter = (LanguageFloorLiberationEncounter)ModelDb
            .Encounter<LanguageFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["CurrentPhase"] = "4",
            ["PhaseComplete"] = "False",
            ["TransitionPending"] = "False"
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
                    .OfType<LanguageFloorDipsia>()
                    .Count() == 1
                && CombatManager.Instance.DebugOnlyGetState()?.Enemies
                    .Select(static enemy => enemy.Monster)
                    .OfType<LanguageFloorBloodBat>()
                    .Count() == 2,
            "direct Language Floor phase-four combat start");
        await WaitFrames(10);

        CombatState combatState = CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException(
                "Phase-four combat state was null.");
        LanguageFloorDipsia boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorDipsia>()
            .Single();
        LanguageFloorBloodBat[] bats = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorBloodBat>()
            .ToArray();
        NCreature bossNode = NCombatRoom.Instance!.GetCreatureNode(
                boss.Creature)
            ?? throw new InvalidOperationException(
                "Dipsia visual node was missing.");
        var activeEncounter = combatState.Encounter
            as LanguageFloorLiberationEncounter
            ?? throw new InvalidOperationException(
                "Language Floor encounter was not active.");
        return new PhaseFourContext(
            combatState,
            activeEncounter,
            boss,
            boss.Creature,
            bats.Single(static bat =>
                bat.Creature.SlotName
                == LanguageFloorLiberationEncounter.DipsiaLeftBatSlot),
            bats.Single(static bat =>
                bat.Creature.SlotName
                == LanguageFloorLiberationEncounter.DipsiaRightBatSlot),
            combatState.PlayerCreatures.Single(),
            bossNode);
    }

    private static async Task RemoveBurningBlood()
    {
        Player player = RunManager.Instance.DebugOnlyGetState()?
            .Players.Single()
            ?? throw new InvalidOperationException("Run player was missing.");
        if (player.GetRelic<BurningBlood>() is { } burningBlood)
        {
            await RelicCmd.Remove(burningBlood);
        }
    }

    private static async Task EnsureHydrophobiaTriggered(
        MonsterModel monster,
        CombatState combatState)
    {
        if (monster.Creature.GetPowerInstances<LibraryStrongPower>()
            .Any(static power => power.TurnsRemaining > 0))
        {
            return;
        }

        await monster.BeforeSideTurnStart(
            new ThrowingPlayerChoiceContext(),
            CombatSide.Player,
            combatState.PlayerCreatures,
            combatState);
    }

    private static void RequireLastTargetsAreLivingPlayers(
        PhaseFourContext fight)
    {
        uint?[] expected = fight.CombatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .OrderBy(static player => player.CombatId)
            .Select(static player => player.CombatId)
            .ToArray();
        Require(fight.Boss.LastAttackTargetCombatIds.SequenceEqual(expected),
            "Dipsia did not explicitly collect every living player target.");
        HashSet<uint?> enemyIds = fight.CombatState.Enemies
            .Select(static enemy => enemy.CombatId)
            .ToHashSet();
        Require(!fight.Boss.LastAttackTargetCombatIds.Any(enemyIds.Contains),
            "Dipsia attack incorrectly targeted an enemy-side unit.");
    }

    private static async Task ResetCreatureHpAndBlock(
        Creature creature,
        Creature dealer,
        bool clearBossBlock = true)
    {
        await CreatureCmd.SetCurrentHp(creature, creature.MaxHp);
        await ClearBlock(creature, dealer);
        if (clearBossBlock && dealer.IsEnemy)
        {
            await ClearBlock(dealer, creature);
        }
    }

    private static async Task ClearBlock(
        Creature creature,
        Creature dealer)
    {
        if (creature.Block > 0)
        {
            await CreatureCmd.LoseBlock(
                new ThrowingPlayerChoiceContext(),
                creature,
                creature.Block,
                dealer);
        }
    }

    private static async Task RemovePhaseBats(CombatState combatState)
    {
        foreach (Creature bat in combatState.Enemies
                     .Where(static enemy =>
                         enemy.Monster is LanguageFloorBloodBat)
                     .ToArray())
        {
            await LiberationPhaseCleanup.RemoveTransitionCreature(
                bat,
                combatState);
        }
    }

    private static LanguageFloorBloodBat[] LivingPhaseBats(
        CombatState combatState) =>
        combatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<LanguageFloorBloodBat>()
            .OrderBy(static bat => bat.Creature.SlotName)
            .ToArray();

    private static async Task RemovePowers<TPower>(Creature creature)
        where TPower : PowerModel
    {
        foreach (TPower power in creature
                     .GetPowerInstances<TPower>()
                     .ToArray())
        {
            await PowerCmd.Remove(power);
        }
    }

    private static void RequireTemporaryStrong(
        Creature creature,
        decimal expectedAmount)
    {
        LibraryStrongPower[] strongPowers = creature
            .GetPowerInstances<LibraryStrongPower>()
            .Where(static power => power.TurnsRemaining > 0)
            .ToArray();
        LibraryStrongPower? strong = strongPowers.SingleOrDefault();
        Require(strong?.Amount == expectedAmount
                && strong.TurnsRemaining == 1,
            "Temporary Strong mismatch on " + creature.Name
            + ": expected amount=" + expectedAmount
            + " turns=1; actual="
            + string.Join(
                ",",
                strongPowers.Select(static power =>
                    "amount=" + power.Amount
                    + " turns=" + power.TurnsRemaining))
            + ".");
    }

    private static void RequirePermanentDurationPower<TPower>(
        Creature creature,
        decimal expectedAmount)
        where TPower : LibraryTurnsPowerModel
    {
        TPower? power = creature.GetPowerInstances<TPower>()
            .SingleOrDefault(static candidate => candidate.AmountPlan.Count == 0);
        Require(power?.Amount == expectedAmount,
            typeof(TPower).Name + " permanent amount mismatch on "
            + creature.Name + ".");
    }

    private static void VerifyResistancePattern(
        LibraryCreature creature,
        string label)
    {
        Require(creature.GetPhysicalResistanceLevel(
                    LibraryDamageType.Slash)
                == LibraryResistanceLevel.Endure
                && creature.GetPhysicalResistanceLevel(
                    LibraryDamageType.Pierce)
                == LibraryResistanceLevel.Normal
                && creature.GetPhysicalResistanceLevel(
                    LibraryDamageType.Blunt)
                == LibraryResistanceLevel.Endure
                && creature.GetChaosResistanceLevel(
                    LibraryDamageType.Slash)
                == LibraryResistanceLevel.Endure
                && creature.GetChaosResistanceLevel(
                    LibraryDamageType.Pierce)
                == LibraryResistanceLevel.Normal
                && creature.GetChaosResistanceLevel(
                    LibraryDamageType.Blunt)
                == LibraryResistanceLevel.Endure,
            label + " resistance pattern was not Endure/Normal/Endure.");
    }

    private static LibraryCreature RequireLibraryCreature(
        Creature creature,
        string label) =>
        creature as LibraryCreature
        ?? throw new InvalidOperationException(
            label + " was not a LibraryCreature.");

    private static void VerifySceneMarkers()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>(
            LanguageFloorLiberationEncounter.EncounterScenePath)
            ?? throw new InvalidOperationException(
                "Language Floor encounter scene was missing.");
        Control root = scene.Instantiate<Control>();
        try
        {
            RequireMarker(
                root,
                LanguageFloorLiberationEncounter.DipsiaLeftBatSlot,
                new Vector2(1080f, 755f));
            RequireMarker(
                root,
                LanguageFloorLiberationEncounter.DipsiaBossSlot,
                new Vector2(1450f, 755f));
            RequireMarker(
                root,
                LanguageFloorLiberationEncounter.DipsiaRightBatSlot,
                new Vector2(1870f, 755f));
        }
        finally
        {
            root.Free();
        }
    }

    private static void RequireMarker(
        Node root,
        string name,
        Vector2 expected)
    {
        Marker2D? marker = root.GetNodeOrNull<Marker2D>(name);
        Require(marker != null
                && marker.Position.IsEqualApprox(expected),
            name + " marker mismatch.");
    }

    private static void RequireTriggerTexture(
        SpriteAttackCreatureVisuals visuals,
        string trigger,
        string expectedPath)
    {
        Require(visuals.TryPlayTrigger(trigger),
            "Visual trigger failed: " + trigger);
        Sprite2D attack = visuals.GetNode<Sprite2D>("%AttackVisuals");
        Require(attack.Texture?.ResourcePath == expectedPath,
            trigger + " texture mismatch: "
            + (attack.Texture?.ResourcePath ?? "<null>"));
    }

    private static void RequireAnchoredTrigger(
        SpriteAttackCreatureVisuals visuals,
        string trigger,
        string expectedPath,
        Vector2 expectedScale,
        Vector2 expectedOffset)
    {
        Node2D motionRoot = visuals.GetNode<Node2D>("%MotionRoot");
        Sprite2D attack = visuals.GetNode<Sprite2D>("%AttackVisuals");
        Vector2 motionPosition = motionRoot.Position;
        RequireTriggerTexture(visuals, trigger, expectedPath);
        Require(motionRoot.Position == motionPosition
                && attack.Position == expectedOffset
                && attack.Scale == expectedScale,
            trigger + " scale or character anchor was incorrect.");
    }

    private static void RequireBackgroundTexture(string expectedPath)
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
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record PhaseThreeTransitionContext(
        CombatState CombatState,
        LanguageFloorLiberationEncounter Encounter,
        LanguageFloorSmilingFace Boss);

    private sealed record PhaseFourContext(
        CombatState CombatState,
        LanguageFloorLiberationEncounter Encounter,
        LanguageFloorDipsia Boss,
        Creature BossCreature,
        LanguageFloorBloodBat LeftBat,
        LanguageFloorBloodBat RightBat,
        Creature Player,
        NCreature BossNode);
}

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryLib.Light;
using LibraryLib.SpeedDice;
using LibraryOfRuina.content.acts;
using LibraryOfRuina.content.liberation.Philosophy;
using LibraryOfRuina.content.liberation.Social;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.features.ftue;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using DoomPower = MegaCrit.Sts2.Core.Models.Powers.DoomPower;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class SocialFloorLiberationVerificationPatch
{
    private const string VerifyArg = "lor-verify-social-floor";
    private const string LayoutVerifyArg = "lor-verify-social-floor-layout";
    private const string RoutingVerifyArg =
        "lor-verify-social-floor-routing";
    private const string VfxVerifyArg =
        "lor-verify-social-floor-vfx";
    private const string WoodsmanSummonsVerifyArg =
        "lor-verify-social-floor-woodsman-summons";
    private const string ExpectedChesedLogoPixelSha256 =
        "8030F40380067C112BC346625C965D741B900671BD3FF5F8D7F52FC3C38C65B6";
    private const string LogPrefix =
        "[LibraryOfRuina.SocialFloor.Verify] ";
    private const int DoubleBossSeedSamples = 4096;
    private const string EncounterScenePath =
        "res://scenes/encounters/social_floor_liberation_encounter.tscn";
    private const string BackgroundScenePath =
        "res://scenes/backgrounds/social_floor_liberation_encounter/"
        + "social_floor_liberation_encounter_background.tscn";
    private const string BackgroundLayerScenePath =
        "res://scenes/backgrounds/social_floor_liberation_encounter/"
        + "layers/social_floor_liberation_encounter_bg_00_a.tscn";

    private static readonly string[] ExpectedBgmTracks =
    [
        "res://audio/bgm/language_floor_liberation/"
            + "roland_liberation_phase_1.ogg",
        "res://audio/bgm/language_floor_liberation/"
            + "roland_liberation_phase_2.ogg",
        "res://audio/bgm/language_floor_liberation/"
            + "roland_liberation_phase_3.ogg"
    ];

    private static bool _started;

    internal static void Start()
    {
        if (_started
            || (!HasVerifyArg()
                && !HasLayoutVerifyArg()
                && !HasRoutingVerifyArg()
                && !HasVfxVerifyArg()
                && !HasWoodsmanSummonsVerifyArg()))
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
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static bool HasLayoutVerifyArg() =>
        CommandLineHelper.HasArg(LayoutVerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            LayoutVerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static bool HasRoutingVerifyArg() =>
        CommandLineHelper.HasArg(RoutingVerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            RoutingVerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static bool HasVfxVerifyArg() =>
        CommandLineHelper.HasArg(VfxVerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VfxVerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static bool HasWoodsmanSummonsVerifyArg() =>
        CommandLineHelper.HasArg(WoodsmanSummonsVerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            WoodsmanSummonsVerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        bool layoutOnly = HasLayoutVerifyArg();
        bool routingOnly = HasRoutingVerifyArg();
        bool vfxOnly = HasVfxVerifyArg();
        bool woodsmanSummonsOnly = HasWoodsmanSummonsVerifyArg();
        try
        {
            if (woodsmanSummonsOnly)
            {
                VerifyWoodsmanSummonBoundary();
                await Task.Yield();
                Log.Info(LogPrefix + "WOODSMAN_SUMMON_CLEANUP_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (vfxOnly)
            {
                VerifyOverflowingLightVfxContract();
                await Task.Yield();
                Log.Info(LogPrefix + "SOCIAL_FLOOR_VFX_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (routingOnly)
            {
                VerifyRegistrationAndActRouting();
                VerifyMapIconContract();
                await Task.Yield();
                Log.Info(LogPrefix + "SOCIAL_FLOOR_ROUTING_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            if (layoutOnly)
            {
                VerifyVisualLayoutContract();
                await Task.Yield();
                Log.Info(LogPrefix + "SOCIAL_FLOOR_LAYOUT_OK");
                NGame.Instance?.GetTree().Quit();
                return;
            }

            VerifyFalseThroneBadgeContract();
            Log.Info(LogPrefix + "FALSE_THRONE_BADGES_OK");
            VerifyRegistrationAndActRouting();
            VerifyConsoleCompletion();
            VerifyEncounterContractAndResources();
            VerifyMonsterAndMoveContract();
            await VerifyDoomAndDeathGate();
            VerifyTrialStateMachineSeams();
            VerifyBgmContract();
            VerifySpecialCardContract();
            VerifySavedPropertyRegistration();
            await VerifyPlayerMechanicsConstants();
            VerifyMultiplayerScaling();
            VerifyEncounterStateRoundTrip();

            await Task.Yield();
            Log.Info(LogPrefix + "SOCIAL_FLOOR_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            string failurePrefix = routingOnly
                ? "SOCIAL_FLOOR_ROUTING_FAILED: "
                : layoutOnly
                    ? "SOCIAL_FLOOR_LAYOUT_FAILED: "
                    : vfxOnly
                        ? "SOCIAL_FLOOR_VFX_FAILED: "
                        : "SOCIAL_FLOOR_FAILED: ";
            Log.Error(LogPrefix + failurePrefix + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyRegistrationAndActRouting()
    {
        Require(LibraryOfRuinaSettings.MonsterExtensionEnabled,
            "Monster extension must be enabled for the verifier.");
        Require(LiberationBossRegistry.RegisterSocialFloorLiberation,
            "Social floor liberation is not registered by default.");
        Require(
            LiberationBossRegistry
                .IsEncounterRegistered<SocialFloorLiberationEncounter>(),
            "Generic Social floor registration lookup failed.");

        EncounterModel social =
            ModelDb.Encounter<SocialFloorLiberationEncounter>();
        Require(LiberationBossRegistry.IsEncounterRegistered(social),
            "Instance Social floor registration lookup failed.");
        Require(LiberationBossRegistry.IsLiberationEncounter(social),
            "Social floor was omitted from liberation recognition.");

        EncounterModel? glorySelected =
            LibraryEncounterWeighting.ChooseLiberationEncounter(
                ModelDb.Act<Glory>(),
                2);
        Require(glorySelected == null,
            "Glory was incorrectly routed by Library encounter weighting.");

        EncounterModel? chesedSelected =
            LibraryEncounterWeighting.ChooseLiberationEncounter(
                ModelDb.Act<Chesed>(),
                2);
        Require(chesedSelected is SocialFloorLiberationEncounter,
            "Chesed did not keep its fixed Social floor boss.");
        Require(!AbnormalityEliteBgmController.IsEligibleAct(
                    ModelDb.Act<Glory>())
                && AbnormalityEliteBgmController.IsEligibleAct(
                    ModelDb.Act<Chesed>()),
            "Abnormality elite BGM act ownership boundary changed.");

        VerifyThirdActDoubleBossLiberationOnlyPool();
        VerifyThirdActDoubleBossSlotContract();
        VerifyVanillaThirdActBossPairIsPreserved();
        VerifySocialFloorDoesNotReplaceSinglePhilosophyBoss();
    }

    private static void VerifyThirdActDoubleBossSlotContract()
    {
        Require(LibraryEncounterWeighting
                .ShouldApplyThirdActDoubleBossRule(
                    actIndex: 2,
                    ascensionLevel: (int)AscensionLevel.DoubleBoss),
            "A10 Act 3 was excluded from the fixed double-boss slot.");
        Require(!LibraryEncounterWeighting
                .ShouldApplyThirdActDoubleBossRule(
                    actIndex: 2,
                    ascensionLevel: (int)AscensionLevel.DoubleBoss - 1),
            "A9 Act 3 was included in the double-boss slot.");
        Require(!LibraryEncounterWeighting
                .ShouldApplyThirdActDoubleBossRule(
                    actIndex: 1,
                    ascensionLevel: (int)AscensionLevel.DoubleBoss),
            "A10 Act 2 was included in the double-boss slot.");
        Require(!LibraryEncounterWeighting
                .ShouldApplyThirdActDoubleBossRule(
                    actIndex: 3,
                    ascensionLevel: (int)AscensionLevel.DoubleBoss),
            "A10 Act 4 was included in the double-boss slot.");
    }

    private static void VerifyThirdActDoubleBossLiberationOnlyPool()
    {
        bool previousPhilosophy =
            LiberationBossRegistry.RegisterPhilosophyFloorLiberation;
        bool previousSocial =
            LiberationBossRegistry.RegisterSocialFloorLiberation;
        try
        {
            LiberationBossRegistry.RegisterPhilosophyFloorLiberation = true;
            LiberationBossRegistry.RegisterSocialFloorLiberation = true;

            const string label =
                "social_floor_liberation_only_double_boss_verification";
            for (ulong seed = 0; seed < DoubleBossSeedSamples; seed++)
            {
                IReadOnlyList<EncounterModel> selected =
                    LibraryEncounterWeighting
                        .ChooseThirdActDoubleBossSelection(
                            seed,
                            label);
                Require(selected.Count == 2,
                    "Act-3 double-boss selection did not return two bosses.");
                Require(selected[0].Id != selected[1].Id,
                    "Act-3 double-boss selection returned a duplicate boss.");

                bool hasPhilosophy = selected.Any(
                    static encounter =>
                        encounter is PhilosophyFloorLiberationEncounter);
                bool hasSocial = selected.Any(
                    static encounter =>
                        encounter is SocialFloorLiberationEncounter);
                Require(hasPhilosophy && hasSocial,
                    "Act-3 double-boss selection must contain both registered "
                    + "Act-3 liberation encounters and no vanilla boss.");
                Require(selected.All(static encounter =>
                        encounter is PhilosophyFloorLiberationEncounter
                            or SocialFloorLiberationEncounter),
                    "Act-3 double-boss pool included a non-liberation boss.");
            }

            IReadOnlyList<EncounterModel> first =
                LibraryEncounterWeighting
                    .ChooseThirdActDoubleBossSelection(
                        705510,
                        label);
            IReadOnlyList<EncounterModel> repeated =
                LibraryEncounterWeighting
                    .ChooseThirdActDoubleBossSelection(
                        705510,
                        label);
            Require(first.Select(static encounter => encounter.Id)
                    .SequenceEqual(
                        repeated.Select(static encounter => encounter.Id)),
                "Act-3 liberation-only order changed for the same seed and label.");

            LiberationBossRegistry.RegisterPhilosophyFloorLiberation = false;
            IReadOnlyList<EncounterModel> socialOnly =
                LibraryEncounterWeighting
                    .ChooseThirdActDoubleBossSelection(
                        705511,
                        label);
            Require(socialOnly.Count == 0,
                "Act-3 double-boss selection must fail closed when fewer than "
                + "two liberation encounters are registered.");

            LiberationBossRegistry.RegisterPhilosophyFloorLiberation = true;
            LiberationBossRegistry.RegisterSocialFloorLiberation = false;
            IReadOnlyList<EncounterModel> philosophyOnly =
                LibraryEncounterWeighting
                    .ChooseThirdActDoubleBossSelection(
                        705512,
                        label);
            Require(philosophyOnly.Count == 0,
                "Act-3 double-boss selection must not use a vanilla fallback "
                + "when Social floor is unregistered.");
        }
        finally
        {
            LiberationBossRegistry.RegisterPhilosophyFloorLiberation =
                previousPhilosophy;
            LiberationBossRegistry.RegisterSocialFloorLiberation = previousSocial;
        }
    }

    private static EncounterModel GetVanillaThirdActBossCandidate() =>
        ModelDb.Act<Glory>().AllBossEncounters.First(
            static encounter =>
                !LibraryEncounterWeighting.IsModEncounter(encounter));

    private static void VerifyVanillaThirdActBossPairIsPreserved()
    {
        EncounterModel philosophy =
            ModelDb.Encounter<PhilosophyFloorLiberationEncounter>();
        EncounterModel social =
            ModelDb.Encounter<SocialFloorLiberationEncounter>();
        ActModel mutableGlory = ModelDb.Act<Glory>().ToMutable();
        mutableGlory.SetBossEncounter(philosophy);
        mutableGlory.SetSecondBossEncounter(social);

        LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(
            mutableGlory,
            2);

        Require(mutableGlory.BossEncounter.Id == philosophy.Id
                && mutableGlory.SecondBossEncounter?.Id == social.Id,
            "Library encounter weighting changed Glory's existing boss pair.");

        EncounterModel vanilla = GetVanillaThirdActBossCandidate();
        mutableGlory.SetBossEncounter(vanilla);
        mutableGlory.SetSecondBossEncounter(philosophy);
        LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(
            mutableGlory,
            2);
        Require(mutableGlory.BossEncounter.Id == vanilla.Id
                && mutableGlory.SecondBossEncounter?.Id == philosophy.Id,
            "Library encounter weighting changed Glory's vanilla boss pair.");
    }

    private static void VerifySocialFloorDoesNotReplaceSinglePhilosophyBoss()
    {
        ActModel mutableBinah = ModelDb.Act<Binah>().ToMutable();
        mutableBinah.SetBossEncounter(
            ModelDb.Encounter<SocialFloorLiberationEncounter>());
        mutableBinah.SetSecondBossEncounter(null);

        LibraryEncounterWeighting.ForceHistoryFloorFirstActBoss(
            mutableBinah,
            2);

        Require(mutableBinah.BossEncounter
                is PhilosophyFloorLiberationEncounter,
            "Social floor replaced Binah's fixed Philosophy floor boss.");
    }

    private static void VerifyConsoleCompletion()
    {
        var completion = new CompletionResult();
        FightConsoleLiberationCompletionPatch.Postfix(
            Array.Empty<string>(),
            ref completion);

        string socialId =
            ModelDb.Encounter<SocialFloorLiberationEncounter>().Id.Entry;
        Require(completion.Candidates.Contains(
                socialId,
                StringComparer.OrdinalIgnoreCase),
            "Fight-console completion omitted " + socialId + ".");
    }

    private static void VerifyEncounterContractAndResources()
    {
        var encounter = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();

        Require(encounter.ShouldGiveRewards,
            "Social floor must retain standard act-3 boss rewards.");
        Require(FtueGuard.IsLiberationEncounter(encounter),
            "Social floor was excluded from the standard liberation FTUE.");
        Require(encounter.BossNodePath ==
                SocialFloorLiberationEncounter.BossNodeResourcePath,
            "Unexpected Social floor boss-node icon path.");
        Require(encounter.HasScene,
            "Social floor encounter does not expose a custom scene.");
        string[] expectedSlots =
        [
            SocialFloorLiberationEncounter.FalseThroneSlot,
            ..SocialFloorLiberationEncounter.CrystalSlots,
            ..SocialFloorLiberationEncounter.FaceSlots
        ];
        Require(encounter.Slots.SequenceEqual(expectedSlots),
            "Social floor fixed-slot order changed: "
            + string.Join(",", encounter.Slots) + ".");

        MonsterModel[] possibleMonsters = encounter.AllPossibleMonsters.ToArray();
        Require(possibleMonsters.Select(static monster => monster.GetType())
                    .Distinct()
                    .Count() >= 3,
            "Social floor AllPossibleMonsters must include the throne, crystal, "
            + "and face models.");

        HashSet<string> assetPaths = encounter.ExtraAssetPaths
            .ToHashSet(StringComparer.Ordinal);
        string[] requiredIconPaths =
        [
            SocialFloorLiberationEncounter.BossNodeResourcePath + ".png",
            SocialFloorLiberationEncounter.BossNodeResourcePath
                + "_outline.png",
            "res://images/ui/run_history/"
                + "social_floor_liberation_encounter.png",
            "res://images/ui/run_history/"
                + "social_floor_liberation_encounter_outline.png"
        ];
        foreach (string requiredPath in ExpectedBgmTracks
                     .Append(EncounterScenePath)
                     .Append(BackgroundScenePath)
                     .Append(BackgroundLayerScenePath)
                     .Concat(requiredIconPaths))
        {
            Require(assetPaths.Contains(requiredPath),
                "Encounter asset list omitted " + requiredPath + ".");
        }

        foreach (string path in assetPaths)
        {
            Require(ResourceLoader.Exists(path),
                "Missing Social floor resource: " + path);
        }

        VerifyMapIconContract();

        encounter.GenerateMonstersWithSlots(NullRunState.Instance);
        var generated = encounter.MonstersWithSlots.ToArray();
        Require(generated.Length == 1,
            "Social floor must initially generate only the persistent throne.");
        Require(generated[0].Item2 != null
                && encounter.Slots.Contains(
                    generated[0].Item2!,
                    StringComparer.Ordinal),
            "Initial throne slot is not present in encounter Slots.");
        Require(generated[0].Item1 is FalseThrone
                && generated[0].Item2
                    == SocialFloorLiberationEncounter.FalseThroneSlot,
            "Social floor initial generation must contain only False Throne "
            + "in false_throne.");

        VerifyEncounterSceneMarkers(expectedSlots);
        VerifyBackgroundComposition();
        VerifyVisualAndVfxResources(assetPaths);
        VerifyPowerIconResources();
    }

    private static void VerifyMapIconContract()
    {
        LiberationBossMapIconVerificationPatch.VerifyFloor("social");

        string[] runHistoryPaths =
        [
            "res://images/ui/run_history/"
                + "social_floor_liberation_encounter.png",
            "res://images/ui/run_history/"
                + "social_floor_liberation_encounter_outline.png"
        ];
        foreach (string path in runHistoryPaths)
        {
            Require(ResourceLoader.Exists(path),
                "Missing Social floor run-history icon resource: " + path);
            Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
            Require(texture != null,
                "Failed to load Social floor run-history icon resource: "
                + path);
            Image? image = texture!.GetImage();
            Require(image != null
                    && image.GetWidth() > 0
                    && image.GetHeight() > 0,
                "Failed to read Social floor icon pixels: " + path);
            if (image!.IsCompressed())
            {
                Require(image.Decompress() == Error.Ok,
                    "Failed to decompress Social floor icon: " + path);
            }

            if (image.GetFormat() != Image.Format.Rgba8)
            {
                image.Convert(Image.Format.Rgba8);
            }

            string hash = Convert.ToHexString(
                SHA256.HashData(image.GetData()));
            Require(string.Equals(
                    hash,
                    ExpectedChesedLogoPixelSha256,
                    StringComparison.OrdinalIgnoreCase),
                "Social floor icon does not match Chesed_Logo.png: "
                + path + " (" + hash + ").");
        }
    }

    private static void VerifyEncounterSceneMarkers(
        IReadOnlyList<string> expectedSlots)
    {
        PackedScene packed = ResourceLoader.Load<PackedScene>(
                EncounterScenePath)
            ?? throw new InvalidOperationException(
                "Could not load Social floor encounter scene.");
        Control root = packed.Instantiate<Control>();
        try
        {
            Marker2D[] markers = expectedSlots
                .Select(slot => root.GetNodeOrNull<Marker2D>(slot)
                    ?? throw new InvalidOperationException(
                        "Social floor scene has no Marker2D named " + slot))
                .ToArray();
            Require(markers.Select(static marker => marker.Position)
                    .Distinct()
                    .Count() == markers.Length,
                "Social floor encounter markers overlap exactly.");

            Marker2D[] crystals = SocialFloorLiberationEncounter.CrystalSlots
                .Select(slot => root.GetNode<Marker2D>(slot))
                .ToArray();
            Marker2D[] faces = SocialFloorLiberationEncounter.FaceSlots
                .Select(slot => root.GetNode<Marker2D>(slot))
                .ToArray();
            Require(IsStrictlyIncreasingX(crystals)
                    && IsStrictlyIncreasingX(faces),
                "Social floor crystal/face markers are not ordered left-to-right.");
            Require(crystals.Select(static marker => marker.Position)
                        .SequenceEqual(
                        [
                            new Vector2(900f, 760f),
                            new Vector2(1220f, 760f),
                            new Vector2(1540f, 760f)
                        ])
                    && faces.Select(static marker => marker.Position)
                        .SequenceEqual(
                        [
                            new Vector2(800f, 740f),
                            new Vector2(1060f, 740f),
                            new Vector2(1320f, 740f),
                            new Vector2(1580f, 740f)
                        ]),
                "Social floor crystal/face marker offsets changed.");
            Marker2D throne = root.GetNode<Marker2D>(
                SocialFloorLiberationEncounter.FalseThroneSlot);
            Require(throne.Position == new Vector2(1980f, 755f)
                    && throne.Position.X
                    > markers.Where(marker => marker.Name !=
                            SocialFloorLiberationEncounter.FalseThroneSlot)
                        .Max(static marker => marker.Position.X),
                "False Throne marker position changed.");
        }
        finally
        {
            root.Free();
        }
    }

    private static bool IsStrictlyIncreasingX(
        IReadOnlyList<Marker2D> markers)
    {
        for (int index = 1; index < markers.Count; index++)
        {
            if (markers[index - 1].Position.X >= markers[index].Position.X)
            {
                return false;
            }
        }

        return true;
    }

    private static void VerifyBackgroundComposition()
    {
        PackedScene packed = ResourceLoader.Load<PackedScene>(
                BackgroundLayerScenePath)
            ?? throw new InvalidOperationException(
                "Could not load Social floor composite background layer.");
        Control root = packed.Instantiate<Control>();
        try
        {
            Require(Mathf.IsEqualApprox(root.OffsetLeft, -1280f)
                    && Mathf.IsEqualApprox(root.OffsetTop, -720f)
                    && Mathf.IsEqualApprox(root.OffsetRight, 1280f)
                    && Mathf.IsEqualApprox(root.OffsetBottom, 720f),
                "Social floor composite background is not 2560x1440.");

            string[] expectedLayers = ["Background", "CrystalFloor"];
            Require(root.GetChildCount() == expectedLayers.Length
                    && Enumerable.Range(0, expectedLayers.Length)
                        .All(index => root.GetChild(index).Name
                            == expectedLayers[index]),
                "Social floor composite background layer order changed.");

            TextureRect background = root.GetNode<TextureRect>("Background");
            TextureRect floor = root.GetNode<TextureRect>("CrystalFloor");
            Require(background.Texture != null
                    && floor.Texture != null,
                "Social floor composite background has an empty texture layer.");
            Require(background.ZIndex == -100
                    && floor.ZIndex == -80
                    && !background.ZAsRelative
                    && !floor.ZAsRelative,
                "Social floor composite background depth order changed.");
            Require(root.GetNodeOrNull<TextureRect>("Frame") == null,
                "Social floor black frame mask must remain removed.");
        }
        finally
        {
            root.Free();
        }

        string[] sourceTextures =
        [
            "res://images/backgrounds/social_floor_liberation_encounter/1_32.png",
            "res://images/backgrounds/social_floor_liberation_encounter/2_13.png"
        ];
        foreach (string texture in sourceTextures)
        {
            Require(ResourceLoader.Exists(texture),
                "Missing Social floor composite texture: " + texture);
        }
    }

    private static void VerifyVisualAndVfxResources(
        IReadOnlySet<string> encounterAssets)
    {
        VerifyVisualLayoutContract();

        Require(MonsterVisualCatalog.RegisteredIds.Contains(
                    "FALSE_THRONE",
                    StringComparer.Ordinal)
                && MonsterVisualCatalog.RegisteredIds.Contains(
                    "EMERALD_CRYSTAL",
                    StringComparer.Ordinal)
                && MonsterVisualCatalog.RegisteredIds.Contains(
                    "SCOWLING_FACE",
                    StringComparer.Ordinal),
            "Social floor monsters are missing from MonsterVisualCatalog.");

        SpriteVisualProfile throneProfile = FalseThroneCreatureVisuals.Profile;
        throneProfile.Validate();
        string[] expectedThroneFrames =
        [
            "@idle:default",
            "@idle:transformed",
            "damaged",
            "guard",
            "fire",
            "overflowing_light",
            "area",
            "polymorph",
            "rage"
        ];
        Require(throneProfile.Frames.Keys
                .Order(StringComparer.Ordinal)
                .SequenceEqual(expectedThroneFrames.Order(StringComparer.Ordinal)),
            "False Throne sprite frame set changed.");
        string[] requiredTriggers =
        [
            "Attack",
            "Fire",
            "Insolence",
            "Manners",
            "FriendlyGreeting",
            "OverflowingLight",
            "AllSilent",
            "BigMistake",
            "FunIsOver",
            "Transform",
            "Polymorph",
            "MagicalPowder"
        ];
        HashSet<string> actualTriggers = throneProfile.Animations
            .SelectMany(static animation => animation.TriggerNames)
            .ToHashSet(StringComparer.Ordinal);
        Require(requiredTriggers.All(actualTriggers.Contains),
            "False Throne sprite profile omitted an attack/transform trigger.");
        Require(throneProfile.Animations.All(static animation =>
                animation.TriggerType == SpriteVisualTriggerType.TimedSwap),
            "False Throne attacks must remain in-place timed sprite swaps.");

        (string Path, Vector2 Size)[] completeSpriteSizes =
        [
            (FalseThroneCreatureVisuals.DefaultTexturePath,
                new Vector2(436, 1072)),
            (FalseThroneCreatureVisuals.DamagedTexturePath,
                new Vector2(653, 1105)),
            (FalseThroneCreatureVisuals.GuardTexturePath,
                new Vector2(875, 1111)),
            (FalseThroneCreatureVisuals.FireTexturePath,
                new Vector2(975, 1089)),
            (FalseThroneCreatureVisuals.OverflowingLightTexturePath,
                new Vector2(1083, 1078)),
            (FalseThroneCreatureVisuals.AreaTexturePath,
                new Vector2(515, 1072)),
            (FalseThroneCreatureVisuals.RageTexturePath,
                new Vector2(1101, 1082)),
            (FalseThroneCreatureVisuals.PolymorphTexturePath,
                new Vector2(433, 455)),
            (ScowlingFaceCreatureVisuals.DefaultTexturePath,
                new Vector2(513, 507)),
            (ScowlingFaceCreatureVisuals.MoveTexturePath,
                new Vector2(923, 607)),
            (ScowlingFaceCreatureVisuals.DamagedTexturePath,
                new Vector2(667, 752)),
            (ScowlingFaceCreatureVisuals.HitTexturePath,
                new Vector2(944, 944))
        ];
        foreach ((string path, Vector2 expectedSize) in completeSpriteSizes)
        {
            Texture2D texture = ResourceLoader.Load<Texture2D>(path)
                ?? throw new InvalidOperationException(
                    "Could not load complete Social floor sprite: " + path);
            Require(texture.GetSize() == expectedSize,
                $"Social floor sprite {path} was {texture.GetSize()}, "
                + $"expected complete image {expectedSize}.");
        }

        EmeraldCrystalCreatureVisuals.Profile.Validate();
        ScowlingFaceCreatureVisuals.Profile.Validate();
        Require(!throneProfile.Variants.Values.Any(static variant =>
                    variant.FlipH)
                && !ScowlingFaceCreatureVisuals.Profile.Variants.Values
                    .Any(static variant => variant.FlipH),
            "False Throne/Scowling Face complete sprites must face left "
            + "without a second horizontal flip.");
        foreach (string path in throneProfile.AssetPaths
                     .Concat(EmeraldCrystalCreatureVisuals.Profile.AssetPaths)
                     .Concat(ScowlingFaceCreatureVisuals.Profile.AssetPaths)
                     .Concat(SocialFloorLiberationVfx.AssetPaths)
                     .Distinct(StringComparer.Ordinal))
        {
            Require(encounterAssets.Contains(path),
                "Encounter asset list omitted Social visual/VFX: " + path);
            Require(ResourceLoader.Exists(path),
                "Missing Social visual/VFX resource: " + path);
        }

        Require(SocialFloorLiberationVfx.AssetPaths.Length == 16
                && SocialFloorLiberationVfx.AssetPaths
                    .Distinct(StringComparer.Ordinal).Count() == 16,
            "Social floor VFX/SFX bundle must contain 9 textures and 7 sounds.");
    }

    private static void VerifyVisualLayoutContract()
    {
        CreatureVisualLayout face =
            MonsterVisualCatalog.GetLayout("SCOWLING_FACE");
        float faceBoundsCenterX =
            (face.BoundsLeft + face.BoundsRight) * 0.5f;
        Require(Mathf.IsEqualApprox(faceBoundsCenterX, face.SpritePos.X),
            "Scowling Face state display must follow its sprite X offset.");
        Require(Mathf.IsEqualApprox(face.IntentPos.X, face.SpritePos.X)
                && Mathf.IsEqualApprox(face.IntentPos.Y, -215f),
            "Scowling Face intent must remain centered above its sprite.");
        Require(Mathf.IsEqualApprox(
                face.BoundsRight - face.BoundsLeft,
                184f),
            "Scowling Face state-display width changed.");

        CreatureVisualLayout throne =
            MonsterVisualCatalog.GetLayout("FALSE_THRONE");
        Require(Mathf.IsEqualApprox(throne.SpriteScale.X, 0.504f)
                && Mathf.IsEqualApprox(throne.SpriteScale.Y, 0.504f),
            "False Throne visual scale must be 20% above the 0.42 baseline.");
        Require(Mathf.IsEqualApprox(throne.StateDisplayLiftY, 20f),
            "False Throne state display must use the lowered Y offset.");
    }

    private static void VerifyPowerIconResources()
    {
        string[] iconNames =
        [
            "false_throne_wizards_trial_power.png",
            "false_throne_show_your_warm_heart_power.png",
            "false_throne_empty_chest_power.png",
            "false_throne_show_your_wisdom_power.png",
            "false_throne_insignificant_wisdom_power.png",
            "false_throne_show_your_courage_power.png",
            "false_throne_truly_coward_power.png",
            "false_throne_what_can_you_do_power.png",
            "false_throne_rage_power.png",
            "social_floor_courage_power.png",
            "social_floor_scaredy_cat_power.png",
            "social_floor_coward_power.png",
            "social_floor_ozma_power.png"
        ];
        foreach (string iconName in iconNames)
        {
            string path = "res://images/powers/" + iconName;
            Require(ResourceLoader.Exists(path),
                "Missing Social floor power icon: " + path);
        }

        Type[] passivePowerTypes =
        [
            typeof(FalseThroneWizardsTrialPower),
            typeof(FalseThroneShowYourWarmHeartPower),
            typeof(FalseThroneEmptyChestPower),
            typeof(FalseThroneShowYourWisdomPower),
            typeof(FalseThroneInsignificantWisdomPower),
            typeof(FalseThroneShowYourCouragePower),
            typeof(FalseThroneTrulyCowardPower),
            typeof(FalseThroneWhatCanYouDoPower),
            typeof(FalseThroneRagePower)
        ];
        foreach (Type type in passivePowerTypes)
        {
            Require(ModelDb.AllPowers.Any(power => power.GetType() == type),
                "Social floor passive power was not registered: "
                + type.Name);
        }
    }

    private static void VerifyBgmContract()
    {
        int[] expectedTrackIndices = [0, 0, 1, 1, 2, 2];
        for (int phase = 1; phase <= expectedTrackIndices.Length; phase++)
        {
            Require(EncounterBgmController
                    .ResolveLiberationPhaseTrackIndex(phase)
                    == expectedTrackIndices[phase - 1],
                $"Unexpected BGM track mapping for Social phase {phase}.");
        }

        var encounter = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        Require(encounter is ILiberationPhaseBgmSource { CurrentPhase: 1 },
            "Social floor encounter must expose initial phase 1 through "
            + "ILiberationPhaseBgmSource.");

        FieldInfo configField = typeof(EncounterBgmController).GetField(
            "ConfigByEncounterType",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(
                typeof(EncounterBgmController).FullName,
                "ConfigByEncounterType");
        var configs = configField.GetValue(null) as IDictionary
            ?? throw new InvalidOperationException(
                "Encounter BGM configuration table is unavailable.");
        Type encounterType = typeof(SocialFloorLiberationEncounter);
        Require(configs.Contains(encounterType),
            "EncounterBgmController has no Social floor configuration.");

        object config = configs[encounterType]
            ?? throw new InvalidOperationException(
                "Social floor BGM configuration resolved to null.");
        Type configType = config.GetType();
        string[] tracks = ReadProperty<string[]>(config, configType, "TrackPaths");
        Require(tracks.SequenceEqual(ExpectedBgmTracks),
            "Social floor is not bound to all three Roland liberation tracks.");
        Require(string.Equals(
                ReadProperty<object>(config, configType, "ProgressionMode")
                    .ToString(),
                "Dynamic",
                StringComparison.Ordinal),
            "Social floor BGM is not phase-driven.");
        Require(ReadProperty<object?>(
                config,
                configType,
                "DynamicTrackResolver") != null,
            "Social floor phase-driven BGM resolver is missing.");
    }

    private static void VerifyMonsterAndMoveContract()
    {
        var throne = (FalseThrone)ModelDb.Monster<FalseThrone>().ToMutable();
        int expectedHp = throne.UsesToughValues
            ? FalseThrone.ToughHp
            : FalseThrone.NormalHp;
        Require(FalseThrone.NormalHp == 888
                && FalseThrone.ToughHp == 999
                && throne.MinInitialHp == expectedHp
                && throne.MaxInitialHp == expectedHp,
            "False Throne HP or ToughEnemies HP branch changed.");
        Require(FalseThrone.ChaoResistance == 250
                && throne.DefaultChaoResistance == 250,
            "False Throne chao resistance must be 150.");
        RequireNormalResistance(
            throne.DefaultPhysicalResistanceData,
            "False Throne physical");
        RequireNormalResistance(
            throne.DefaultChaoResistanceData,
            "False Throne chao");
        VerifyOverflowingLightVfxContract();

        MonsterMoveStateMachine stateMachine = BuildMoveStateMachine(throne);
        VerifyMoveBadgeValues(throne, stateMachine);
        Require(stateMachine.States.Count == 17,
            "False Throne must expose sixteen moves plus one router state.");
        HashSet<string> expectedMoveIds = new(StringComparer.Ordinal)
        {
            "FALSE_THRONE_INITIAL_SEQUENCE",
            "FALSE_THRONE_OVERFLOWING_LIGHT",
            "FALSE_THRONE_INSOLENCE",
            "FALSE_THRONE_ALL_SILENT",
            "FALSE_THRONE_MANNERS",
            "FALSE_THRONE_FRIENDLY_GREETING",
            "FALSE_THRONE_BIG_MISTAKE",
            "FALSE_THRONE_FUN_IS_OVER",
            "FALSE_THRONE_UNKNOWN_TRIAL",
            "FALSE_THRONE_STUN_TRIAL",
            "FALSE_THRONE_ROUTER"
        };
        expectedMoveIds.UnionWith(FalseThrone.HomeMovePlans.Select(
            static plan => plan.StateId));
        Require(stateMachine.States.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(expectedMoveIds),
            "False Throne move-state IDs changed.");

        int overflowing = throne.UsesToughValues
            ? FalseThrone.OverflowingLightHighDamage
            : FalseThrone.OverflowingLightLowDamage;
        int insolence = throne.UsesToughValues
            ? FalseThrone.InsolenceHighDamage
            : FalseThrone.InsolenceLowDamage;
        int allSilent = throne.UsesToughValues
            ? FalseThrone.AllSilentHighDamage
            : FalseThrone.AllSilentLowDamage;
        int manners = throne.UsesToughValues
            ? FalseThrone.MannersHighDamage
            : FalseThrone.MannersLowDamage;
        int mannersWounds = throne.UsesToughValues
            ? FalseThrone.MannersToughWounds
            : FalseThrone.MannersNormalWounds;
        int greeting = throne.UsesToughValues
            ? FalseThrone.FriendlyGreetingHighDamage
            : FalseThrone.FriendlyGreetingLowDamage;
        int bigMistake = throne.UsesToughValues
            ? FalseThrone.BigMistakeHighDamage
            : FalseThrone.BigMistakeLowDamage;
        int funIsOver = throne.UsesToughValues
            ? FalseThrone.FunIsOverHighDamage
            : FalseThrone.FunIsOverLowDamage;

        MoveState initial = GetMove(
            stateMachine,
            "FALSE_THRONE_INITIAL_SEQUENCE");
        Require(initial.Intents.Count == 3
                && initial.Intents[0] is IndiscriminateAttackIntent
                && initial.Intents[1]
                    is CombinedAttackDebuffIntent
                && initial.Intents[2]
                    is CombinedAttackDebuffIntent,
            "False Throne initial intent order changed.");
        Require(initial.Intents[0]
                    is IGroupAttackIntent { IsGroupAttack: true }
                && initial.Intents.Skip(1).All(static intent =>
                    intent is not IGroupAttackIntent
                    {
                        IsGroupAttack: true
                    }),
            "Only Friendly Greeting may show the indiscriminate intent "
            + "in the False Throne opening sequence.");
        RequireAttackIntent(initial.Intents[0], greeting, 1,
            "initial Friendly Greeting");
        RequireAttackIntent(initial.Intents[1], overflowing,
            FalseThrone.OverflowingLightHits,
            "initial Overflowing Light");
        RequireAttackIntent(initial.Intents[2], insolence, 1,
            "initial Insolence");

        RequireMoveAttack(
            stateMachine,
            "FALSE_THRONE_OVERFLOWING_LIGHT",
            overflowing,
            FalseThrone.OverflowingLightHits,
            typeof(CombinedAttackDebuffIntent),
            expectedGroupAttack: false);
        RequireMoveAttack(
            stateMachine,
            "FALSE_THRONE_INSOLENCE",
            insolence,
            1,
            typeof(CombinedAttackDebuffIntent),
            expectedGroupAttack: false);
        RequireMoveAttack(
            stateMachine,
            "FALSE_THRONE_ALL_SILENT",
            allSilent,
            1,
            typeof(CombinedAttackDebuffIntent),
            expectedGroupAttack: false);
        MoveState mannersMove = GetMove(
            stateMachine,
            "FALSE_THRONE_MANNERS");
        Require(mannersMove.Intents.Count == 2,
            "False Throne Manners must preview attack and status card separately.");
        RequireAttackIntent(
            mannersMove.Intents[0],
            manners,
            FalseThrone.MannersHits,
            "FALSE_THRONE_MANNERS attack");
        Require(mannersMove.Intents[0] is MultiAttackIntent
                && mannersMove.Intents[0]
                    is not IGroupAttackIntent { IsGroupAttack: true },
            "False Throne Manners attack intent classification changed.");
        Require(mannersMove.Intents[1]
                is TargetedDetailedStatusCardIntent<Wound>
                {
                    CardCount: var woundCount
                }
                && woundCount == mannersWounds,
            "False Throne Manners status intent no longer previews Wounds.");
        RequireMoveAttack(
            stateMachine,
            "FALSE_THRONE_FRIENDLY_GREETING",
            greeting,
            1,
            typeof(IndiscriminateAttackIntent),
            expectedGroupAttack: true);
        RequireMoveAttack(
            stateMachine,
            "FALSE_THRONE_BIG_MISTAKE",
            bigMistake,
            1,
            typeof(CombinedAttackDebuffIntent),
            expectedGroupAttack: true);
        RequireMoveAttack(
            stateMachine,
            "FALSE_THRONE_FUN_IS_OVER",
            funIsOver,
            1,
            typeof(CombinedAttackDebuffIntent),
            expectedGroupAttack: true);
        Require(GetMove(stateMachine, "FALSE_THRONE_UNKNOWN_TRIAL")
                    .Intents.Single() is UnknownIntent
                && GetMove(stateMachine, "FALSE_THRONE_STUN_TRIAL")
                    .Intents.Single() is StunIntent,
            "Scarecrow/Lion trial intent concealment changed.");

        VerifyHomeMovePlans(stateMachine);

        VerifySummonContract<EmeraldCrystal>(
            EmeraldCrystal.Hp,
            EmeraldCrystal.ChaoResistance,
            "Emerald Crystal");
        Require(EmeraldCrystal.Hp == 50
                && EmeraldCrystal.ChaoResistance == 50,
            "Emerald Crystal HP/chao must be 50/20.");
        VerifySummonContract<ScowlingFace>(
            ScowlingFace.BaseHp,
            ScowlingFace.ChaoResistance,
            "Scowling Face");
        Require(ScowlingFace.BaseHp == 50
                && ScowlingFace.ChaoResistance == 50,
            "Scowling Face base HP/chao must be 50/20.");
    }

    private static void VerifyOverflowingLightVfxContract()
    {
        Require(ResourceLoader.Exists(SceneHelper.GetScenePath(
                    FalseThrone.OverflowingLightHitVfx)),
            "False Throne Overflowing Light hit VFX is missing: "
            + FalseThrone.OverflowingLightHitVfx + ".");
    }

    private static void VerifyHomeMovePlans(
        MonsterMoveStateMachine stateMachine)
    {
        IReadOnlyList<FalseThroneHomeMovePlan> plans =
            FalseThrone.HomeMovePlans;
        FalseThroneMove[] normalMoves =
        [
            FalseThroneMove.OverflowingLight,
            FalseThroneMove.Insolence,
            FalseThroneMove.AllSilent,
            FalseThroneMove.Manners
        ];
        HashSet<FalseThroneMove> normalMoveSet = normalMoves.ToHashSet();
        HashSet<string> expectedPairs = new(StringComparer.Ordinal);
        for (int first = 0; first < normalMoves.Length; first++)
        {
            for (int second = first + 1; second < normalMoves.Length; second++)
            {
                expectedPairs.Add(
                    $"{normalMoves[first]}|{normalMoves[second]}");
            }
        }

        Require(plans.Count == expectedPairs.Count
                && plans.Select(static plan => plan.Sequence).Distinct().Count()
                    == plans.Count
                && plans.Select(static plan => plan.StateId)
                    .Distinct(StringComparer.Ordinal).Count() == plans.Count,
            "False Throne Home trial must expose six unique two-intent plans.");

        HashSet<string> actualPairs = new(StringComparer.Ordinal);
        foreach (FalseThroneHomeMovePlan plan in plans)
        {
            Require(plan.First != plan.Second
                    && normalMoveSet.Contains(plan.First)
                    && normalMoveSet.Contains(plan.Second),
                plan.StateId + " is not a distinct pair of normal intents.");
            int firstIndex = Array.IndexOf(normalMoves, plan.First);
            int secondIndex = Array.IndexOf(normalMoves, plan.Second);
            FalseThroneMove low = normalMoves[Math.Min(firstIndex, secondIndex)];
            FalseThroneMove high = normalMoves[Math.Max(firstIndex, secondIndex)];
            actualPairs.Add($"{low}|{high}");

            MoveState sequence = GetMove(stateMachine, plan.StateId);
            AbstractIntent[] expectedIntents =
            [
                .. GetMove(stateMachine, ResolveNormalMoveId(plan.First))
                    .Intents,
                .. GetMove(stateMachine, ResolveNormalMoveId(plan.Second))
                    .Intents
            ];
            Require(sequence.Intents.Count == expectedIntents.Length,
                plan.StateId + " flattened intent count changed.");
            for (int intentIndex = 0;
                 intentIndex < expectedIntents.Length;
                 intentIndex++)
            {
                RequireEquivalentIntent(
                    sequence.Intents[intentIndex],
                    expectedIntents[intentIndex],
                    plan.StateId + $" intent {intentIndex + 1}");
            }
        }

        Require(actualPairs.SetEquals(expectedPairs),
            "False Throne Home trial does not cover all six normal-intent pairs.");

        HashSet<FalseThroneMove> rolled = Enumerable.Range(0, 4096)
            .Select(static seed => FalseThrone.RollHomeMove(
                new Rng((ulong)seed)))
            .ToHashSet();
        Require(rolled.SetEquals(plans.Select(static plan => plan.Sequence)),
            "False Throne Home trial RNG cannot reach every two-intent plan.");
    }

    private static string ResolveNormalMoveId(FalseThroneMove move) =>
        move switch
        {
            FalseThroneMove.OverflowingLight =>
                "FALSE_THRONE_OVERFLOWING_LIGHT",
            FalseThroneMove.Insolence => "FALSE_THRONE_INSOLENCE",
            FalseThroneMove.AllSilent => "FALSE_THRONE_ALL_SILENT",
            FalseThroneMove.Manners => "FALSE_THRONE_MANNERS",
            _ => throw new ArgumentOutOfRangeException(
                nameof(move),
                move,
                "Not a False Throne normal move.")
        };

    private static void RequireEquivalentIntent(
        AbstractIntent actual,
        AbstractIntent expected,
        string label)
    {
        Require(actual.GetType() == expected.GetType(),
            label + " type changed.");
        if (actual is AttackIntent actualAttack
            && expected is AttackIntent expectedAttack)
        {
            Require(actualAttack.DamageCalc?.Invoke()
                    == expectedAttack.DamageCalc?.Invoke()
                    && actualAttack.Repeats == expectedAttack.Repeats,
                label + " attack values changed.");
        }
        if (actual is StatusIntent actualStatus
            && expected is StatusIntent expectedStatus)
        {
            Require(actualStatus.CardCount == expectedStatus.CardCount,
                label + " status-card count changed.");
        }
        Require((actual is IGroupAttackIntent { IsGroupAttack: true })
                == (expected is IGroupAttackIntent { IsGroupAttack: true }),
            label + " group-attack classification changed.");
    }

    private static void VerifyMoveBadgeValues(
        FalseThrone throne,
        MonsterMoveStateMachine stateMachine)
    {
        RequirePowerBadges(
            GetMove(stateMachine, "FALSE_THRONE_OVERFLOWING_LIGHT")
                .Intents.Single(),
            "Overflowing Light",
            (typeof(LibraryBleedingPower),
                throne.OverflowingLightBleedAmount));
        RequirePowerBadges(
            GetMove(stateMachine, "FALSE_THRONE_INSOLENCE").Intents.Single(),
            "Insolence",
            (typeof(LibraryBindingPower), throne.InsolenceBindAmount));
        RequirePowerBadges(
            GetMove(stateMachine, "FALSE_THRONE_ALL_SILENT").Intents.Single(),
            "All Silent",
            (typeof(StrengthPower),
                throne.AllSilentStrengthDelta),
            (typeof(DexterityPower),
                throne.AllSilentDexterityDelta));
        RequirePowerBadges(
            GetMove(stateMachine, "FALSE_THRONE_BIG_MISTAKE").Intents.Single(),
            "Big Mistake",
            (typeof(LibraryBleedingPower), throne.BigMistakeBleedAmount));
        RequirePowerBadges(
            GetMove(stateMachine, "FALSE_THRONE_FUN_IS_OVER").Intents.Single(),
            "Fun Is Over",
            (typeof(LibraryWeakPower), throne.FunIsOverWeakAmount),
            (typeof(LibraryDisarmPower), throne.FunIsOverDisarmAmount));
    }

    private static void RequirePowerBadges(
        AbstractIntent intent,
        string label,
        params (Type PowerType, int Amount)[] expected)
    {
        IntentBadge[] actual = IntentEffectCollection.Get(intent)
            .Where(static badge => badge.HasPower)
            .ToArray();
        Require(actual.Length == expected.Length,
            label + " power-badge count no longer matches its effects.");
        for (int index = 0; index < expected.Length; index++)
        {
            Require(actual[index].PowerType == expected[index].PowerType
                    && actual[index].Amount == expected[index].Amount,
                label + $" badge {index + 1} no longer matches its effect.");
        }
    }

    private static void VerifyFalseThroneBadgeContract()
    {
        var throne = (FalseThrone)ModelDb.Monster<FalseThrone>().ToMutable();
        VerifyMoveBadgeValues(throne, BuildMoveStateMachine(throne));
    }

    private static async Task VerifyDoomAndDeathGate()
    {
        foreach (SocialFloorTrial trial in new[]
                 { SocialFloorTrial.Initial, SocialFloorTrial.Home })
        {
            (SocialFloorLiberationEncounter encounter,
                FalseThrone boss,
                Creature creature) = CreateDetachedBossForTrial(trial);
            PowerModel canonicalDoom = ModelDb.Power<DoomPower>();
            Require(boss.TryModifyPowerAmountReceived(
                        canonicalDoom,
                        creature,
                        creature.MaxHp,
                        creature,
                        out decimal preventedDoom)
                    && preventedDoom == 0m,
                $"{trial} did not reject positive Doom before application.");
            Require(!boss.TryModifyPowerAmountReceived(
                        canonicalDoom,
                        creature,
                        -1m,
                        creature,
                        out decimal allowedReduction)
                    && allowedReduction == -1m,
                $"{trial} incorrectly blocked negative Doom adjustment.");

            DoomPower doom = (DoomPower)ModelDb.Power<DoomPower>()
                .ToMutable();
            doom.ApplyInternal(creature, creature.MaxHp, silent: true);

            Require(DoomPower.GetDoomedCreatures([creature])
                    .SequenceEqual([creature]),
                $"Doom construction did not mark the {trial} boss doomed.");
            Require(!boss.ShouldDisappearFromDoom
                    && !boss.ShouldDie(creature),
                $"{trial} incorrectly allowed Doom/death before Rage.");
            bool shouldReceiveChao = trial == SocialFloorTrial.Home;
            decimal chaoCap = boss.ModifyChaoDamageCap(
                creature,
                ValueProp.Move,
                creature,
                cardSource: null,
                cardPlay: null,
                type: LibraryDamageType.None);
            Require(boss.CanReceiveChaoDamage == shouldReceiveChao
                    && chaoCap == (shouldReceiveChao
                        ? decimal.MaxValue
                        : 0m),
                $"{trial} Chao damage gate was {chaoCap}; expected "
                + (shouldReceiveChao ? "open." : "closed."));

            creature.SetCurrentHpInternal(0);
            await boss.AfterPreventingDeath(creature);
            Require(creature.IsAlive
                    && creature.CurrentHp == creature.MaxHp
                    && encounter.Trial == trial
                    && creature.CombatState?.Enemies.Contains(creature) == true,
                $"{trial} Doom/death prevention removed the boss or skipped "
                + "the trial.");
        }

        (SocialFloorLiberationEncounter rageEncounter,
            FalseThrone rageBoss,
            Creature rageCreature) =
            CreateDetachedBossForTrial(SocialFloorTrial.Rage);
        DoomPower rageDoom = (DoomPower)ModelDb.Power<DoomPower>()
            .ToMutable();
        Require(!rageBoss.TryModifyPowerAmountReceived(
                    ModelDb.Power<DoomPower>(),
                    rageCreature,
                    rageCreature.MaxHp,
                    rageCreature,
                    out decimal rageDoomAmount)
                && rageDoomAmount == rageCreature.MaxHp,
            "Rage incorrectly blocked positive Doom application.");
        rageDoom.ApplyInternal(
            rageCreature,
            rageCreature.MaxHp,
            silent: true);
        Require(DoomPower.GetDoomedCreatures([rageCreature])
                .SequenceEqual([rageCreature])
                && rageBoss.ShouldDisappearFromDoom
                && rageBoss.ShouldDie(rageCreature),
            "Rage did not open the Doom and normal-death gates.");
        Require(rageBoss.CanReceiveChaoDamage
                && rageBoss.ModifyChaoDamageCap(
                    rageCreature,
                    ValueProp.Move,
                    rageCreature,
                    cardSource: null,
                    cardPlay: null,
                    type: LibraryDamageType.None) == decimal.MaxValue,
            "Rage unexpectedly closed the Chao damage gate.");
        rageCreature.SetCurrentHpInternal(0);
        Require(rageCreature.IsDead
                && rageEncounter.Trial == SocialFloorTrial.Rage,
            "Rage boss could not reach a normal dead state.");
    }

    private static (SocialFloorLiberationEncounter Encounter,
        FalseThrone Boss,
        Creature Creature) CreateDetachedBossForTrial(
            SocialFloorTrial trial)
    {
        var encounter = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["SocialFloorStateVersion"] = "2",
            ["SocialFloorTrial"] = ((int)trial).ToString()
        });
        var boss = (FalseThrone)ModelDb.Monster<FalseThrone>().ToMutable();
        var combatState = new CombatState(encounter);
        var creature = new Creature(
            boss,
            CombatSide.Enemy,
            SocialFloorLiberationEncounter.FalseThroneSlot)
        {
            CombatState = combatState
        };
        combatState.AddCreature(creature);
        return (encounter, boss, creature);
    }

    private static void VerifySummonContract<TMonster>(
        int expectedHp,
        int expectedChao,
        string label)
        where TMonster : MonsterModel
    {
        MonsterModel summon = ModelDb.Monster<TMonster>().ToMutable();
        Require(summon.MinInitialHp == expectedHp
                && summon.MaxInitialHp == expectedHp
                && summon is LibraryMonsterModel
                {
                    DefaultChaoResistance: var chao
                }
                && chao == expectedChao,
            label + " HP/chao contract changed.");
        var librarySummon = (LibraryMonsterModel)summon;
        RequireNormalResistance(
            librarySummon.DefaultPhysicalResistanceData,
            label + " physical");
        RequireNormalResistance(
            librarySummon.DefaultChaoResistanceData,
            label + " chao");

        MonsterMoveStateMachine stateMachine = BuildMoveStateMachine(summon);
        MoveState hidden = stateMachine.States.Values
            .OfType<MoveState>()
            .Single();
        Require(hidden.Intents.Count == 1
                && hidden.Intents[0] is HiddenIntent
                && ReferenceEquals(hidden.FollowUpState, hidden),
            label + " must retain a hidden, non-acting self-loop intent.");
    }

    private static void RequireNormalResistance(
        LibraryCreatureResistanceData.Resistance? resistance,
        string label)
    {
        Require(resistance is
            {
                Slash: LibraryResistanceLevel.Normal,
                Pierce: LibraryResistanceLevel.Normal,
                Blunt: LibraryResistanceLevel.Normal
            },
            label + " resistance is not Normal/Normal/Normal.");
    }

    private static MonsterMoveStateMachine BuildMoveStateMachine(
        MonsterModel monster)
    {
        MethodInfo method = monster.GetType().GetMethod(
                "GenerateMoveStateMachine",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                monster.GetType().FullName,
                "GenerateMoveStateMachine");
        return (MonsterMoveStateMachine)(method.Invoke(monster, null)
            ?? throw new InvalidOperationException(
                monster.GetType().Name + " returned no move state machine."));
    }

    private static MoveState GetMove(
        MonsterMoveStateMachine stateMachine,
        string id) =>
        stateMachine.States.TryGetValue(id, out MonsterState? state)
        && state is MoveState move
            ? move
            : throw new InvalidOperationException("Missing move state " + id);

    private static void RequireMoveAttack(
        MonsterMoveStateMachine stateMachine,
        string moveId,
        int damage,
        int repeats,
        Type expectedIntentType,
        bool expectedGroupAttack)
    {
        MoveState move = GetMove(stateMachine, moveId);
        Require(move.Intents.Count == 1,
            moveId + " intent count changed.");
        RequireAttackIntent(move.Intents[0], damage, repeats, moveId);
        Require(expectedIntentType.IsInstanceOfType(move.Intents[0]),
            moveId + " does not use the required combined intent type "
            + expectedIntentType.Name + ".");
        bool isGroupAttack = move.Intents[0]
            is IGroupAttackIntent { IsGroupAttack: true };
        Require(isGroupAttack == expectedGroupAttack,
            moveId + " indiscriminate intent classification changed.");
    }

    private static void RequireAttackIntent(
        AbstractIntent intent,
        int damage,
        int repeats,
        string label)
    {
        Require(intent is AttackIntent attack
                && attack.DamageCalc?.Invoke() == damage
                && attack.Repeats == repeats,
            label + $" attack mismatch; expected {damage}x{repeats}.");
    }

    private static void VerifyTrialStateMachineSeams()
    {
        SocialFloorTrial[] phases = Enum.GetValues<SocialFloorTrial>();
        Require(phases.SequenceEqual(
            [
                SocialFloorTrial.Initial,
                SocialFloorTrial.Woodsman,
                SocialFloorTrial.Scarecrow,
                SocialFloorTrial.Lion,
                SocialFloorTrial.Home,
                SocialFloorTrial.Rage
            ])
            && phases.Select(static phase => (int)phase)
                .SequenceEqual([1, 2, 3, 4, 5, 6]),
            "Social floor phase enum is not the ordered six-stage trial.");

        Require(ReadStaticField<int>(
                    typeof(SocialFloorLiberationEncounter),
                    "NormalWisdomCardCount") == 5
                && ReadStaticField<int>(
                    typeof(SocialFloorLiberationEncounter),
                    "ToughWisdomCardCount") == 4
                && ReadStaticField<int>(
                    typeof(SocialFloorLiberationEncounter),
                    "NormalWisdomRequirement") == 3
                && ReadStaticField<int>(
                    typeof(SocialFloorLiberationEncounter),
                    "ToughWisdomRequirement") == 2
                && ReadStaticField<int>(
                    typeof(SocialFloorLiberationEncounter),
                    "ScarecrowPenaltyHpLossPercent") == 60
                && ReadStaticField<int>(
                    typeof(SocialFloorLiberationEncounter),
                    "InitialPowderCost") == 10
                && ReadStaticField<int>(
                    typeof(SocialFloorLiberationEncounter),
                    "AllCrystalsMask") == 0b111
                && ReadStaticField<int>(
                    typeof(SocialFloorLiberationEncounter),
                    "AllFacesMask") == 0b1111,
            "Social floor trial threshold constants changed.");

        (SocialFloorTrial Trial, int Round, FalseThroneMove Move)[] cases =
        [
            (SocialFloorTrial.Initial, 0, FalseThroneMove.InitialSequence),
            (SocialFloorTrial.Initial, 1, FalseThroneMove.InitialSequence),
            (SocialFloorTrial.Woodsman, 0, FalseThroneMove.Manners),
            (SocialFloorTrial.Woodsman, 1, FalseThroneMove.Manners),
            (SocialFloorTrial.Woodsman, 2, FalseThroneMove.BigMistake),
            (SocialFloorTrial.Woodsman, 3, FalseThroneMove.BigMistake),
            (SocialFloorTrial.Scarecrow, 0, FalseThroneMove.UnknownTrial),
            (SocialFloorTrial.Scarecrow, 1, FalseThroneMove.UnknownTrial),
            (SocialFloorTrial.Lion, 0, FalseThroneMove.StunTrial),
            (SocialFloorTrial.Lion, 1, FalseThroneMove.StunTrial),
            (SocialFloorTrial.Home, 0, FalseThroneMove.StunTrial),
            (SocialFloorTrial.Home, 1, FalseThroneMove.FriendlyGreeting),
            (SocialFloorTrial.Home, 2,
                FalseThroneMove.HomeOverflowingLightInsolence),
            (SocialFloorTrial.Rage, 0, FalseThroneMove.FunIsOver),
            (SocialFloorTrial.Rage, 99, FalseThroneMove.FunIsOver)
        ];
        foreach ((SocialFloorTrial trial, int round, FalseThroneMove move)
                 in cases)
        {
            Require(InvokePrivateStatic<FalseThroneMove>(
                    typeof(SocialFloorLiberationEncounter),
                    "ResolveDefaultMove",
                    trial,
                    round) == move,
                $"Unexpected default move for {trial} round {round}.");

            var restored = (SocialFloorLiberationEncounter)ModelDb
                .Encounter<SocialFloorLiberationEncounter>()
                .ToMutable();
            restored.LoadCustomState(new Dictionary<string, string>
            {
                ["SocialFloorTrial"] = ((int)trial).ToString(),
                ["SocialFloorTrialRound"] = round.ToString()
            });
            Require(restored.PlannedMove == move,
                $"Save fallback move mismatch for {trial} round {round}.");
        }

        VerifyWoodsmanSummonBoundary();

        var transition = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        InvokePrivateInstance(
            transition,
            "QueueTrial",
            SocialFloorTrial.Woodsman);
        Require(transition.HasPendingTrial
                && transition.PendingTrial == SocialFloorTrial.Woodsman,
            "Initial -> Woodsman transition was not queued.");
        InvokePrivateInstance(
            transition,
            "QueueTrial",
            SocialFloorTrial.Initial);
        Require(transition.PendingTrial == SocialFloorTrial.Woodsman,
            "Trial queue allowed a backwards phase transition.");

        transition.LoadCustomState(new Dictionary<string, string>
        {
            ["SocialFloorTrial"] = ((int)SocialFloorTrial.Woodsman)
                .ToString(),
            ["SocialFloorHasPendingTrial"] = bool.FalseString
        });
        InvokePrivateInstance(
            transition,
            "QueueTrial",
            SocialFloorTrial.Woodsman);
        Require(!transition.HasPendingTrial,
            "Trial queue re-queued the current phase.");
        InvokePrivateInstance(
            transition,
            "QueueTrial",
            SocialFloorTrial.Scarecrow);
        Require(transition.HasPendingTrial
                && transition.PendingTrial == SocialFloorTrial.Scarecrow,
            "Woodsman -> Scarecrow transition was not queued.");

        MethodInfo turnBoundary = typeof(SocialFloorLiberationEncounter)
            .GetMethod(
                "OnAfterEnemyTurn",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(SocialFloorLiberationEncounter).FullName,
                "OnAfterEnemyTurn");
        Require(turnBoundary.ReturnType == typeof(Task),
            "Social floor transition boundary is no longer asynchronous.");
    }

    private static void VerifyWoodsmanSummonBoundary()
    {
        SocialFloorLiberationEncounter manners =
            CreateWoodsmanEncounterAtRound(
                round: 1,
                FalseThroneMove.Manners);
        SocialFloorLiberationEncounter bigMistake =
            CreateWoodsmanEncounterAtRound(
                round: 2,
                FalseThroneMove.BigMistake);

        Require(manners.ShouldKeepWoodsmanSummons
                && manners.MonstersWithSlots.Count(static entry =>
                    entry.Item1 is EmeraldCrystal) == 3,
            "Woodsman summons disappeared before the Big Mistake round.");
        Require(!bigMistake.ShouldKeepWoodsmanSummons
                && bigMistake.MonstersWithSlots.All(static entry =>
                    entry.Item1 is not EmeraldCrystal),
            "Emerald Crystals were restored during the Big Mistake round.");
    }

    private static SocialFloorLiberationEncounter
        CreateWoodsmanEncounterAtRound(
            int round,
            FalseThroneMove plannedMove)
    {
        var encounter = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        encounter.LoadCustomState(new Dictionary<string, string>
        {
            ["SocialFloorTrial"] =
                ((int)SocialFloorTrial.Woodsman).ToString(),
            ["SocialFloorTrialRound"] = round.ToString(),
            ["SocialFloorSetupComplete"] = bool.TrueString,
            ["SocialFloorPlannedMove"] = ((int)plannedMove).ToString()
        });
        encounter.GenerateMonstersWithSlots(NullRunState.Instance);
        return encounter;
    }

    private static void VerifyEncounterStateRoundTrip()
    {
        var source = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        source.GenerateMonstersWithSlots(NullRunState.Instance);
        Dictionary<string, string> saved = source.SaveCustomState();
        string[] expectedKeys =
        [
            "SocialFloorStateVersion",
            "SocialFloorTrial",
            "SocialFloorTrialRound",
            "SocialFloorSetupComplete",
            "SocialFloorPlayersHealed",
            "SocialFloorDestroyedCrystalMask",
            "SocialFloorDestroyedFaceMask",
            "SocialFloorHasPendingTrial",
            "SocialFloorPendingTrial",
            "SocialFloorScaredyCatPlayerNetId",
            "SocialFloorOzmaPlayerNetId",
            "SocialFloorCowardApplied",
            "SocialFloorOzmaReplacementPending",
            "SocialFloorPowderCost",
            "SocialFloorTransformed",
            "SocialFloorFinalStrikeTriggered",
            "SocialFloorPlannedMove",
            "SocialFloorParticipantCount",
            "SocialFloorBossHp",
            "SocialFloorBossChao",
            "SocialFloorSummonVitals",
            "SocialFloorWisdomStacks",
            "SocialFloorWisdomCards",
            "SocialFloorLionCardsSubmitted",
            "SocialFloorCouragePlayed",
            "SocialFloorCouragePowerPresent",
            "SocialFloorCouragePendingActivations",
            "SocialFloorCourageEnergyActive",
            "SocialFloorCourageRemoveAtTurnEnd"
        ];
        Require(saved.Count == 29
                && saved.Keys.ToHashSet(StringComparer.Ordinal)
                .SetEquals(expectedKeys),
            "Social floor custom-state key set changed: "
            + string.Join(",", saved.Keys.OrderBy(static key => key)));

        var lionValues = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["SocialFloorStateVersion"] = "2",
            ["SocialFloorTrial"] =
                ((int)SocialFloorTrial.Lion).ToString(),
            ["SocialFloorTrialRound"] = "1",
            ["SocialFloorSetupComplete"] = bool.TrueString,
            ["SocialFloorPlayersHealed"] = bool.TrueString,
            ["SocialFloorDestroyedCrystalMask"] = "7",
            ["SocialFloorDestroyedFaceMask"] = "5",
            ["SocialFloorHasPendingTrial"] = bool.FalseString,
            ["SocialFloorPendingTrial"] =
                ((int)SocialFloorTrial.Lion).ToString(),
            ["SocialFloorScaredyCatPlayerNetId"] = "101",
            ["SocialFloorOzmaPlayerNetId"] = string.Empty,
            ["SocialFloorCowardApplied"] = bool.FalseString,
            ["SocialFloorOzmaReplacementPending"] = bool.FalseString,
            ["SocialFloorPowderCost"] = "10",
            ["SocialFloorTransformed"] = bool.FalseString,
            ["SocialFloorFinalStrikeTriggered"] = bool.FalseString,
            ["SocialFloorPlannedMove"] =
                ((int)FalseThroneMove.StunTrial).ToString(),
            ["SocialFloorParticipantCount"] = "3",
            ["SocialFloorBossHp"] = "888",
            ["SocialFloorBossChao"] = "150",
            ["SocialFloorSummonVitals"] =
                "face_2:40:12;face_4:60:8",
            ["SocialFloorWisdomStacks"] = "101:4;202:0;303:5",
            ["SocialFloorWisdomCards"] = "101:1;202:4;303:0",
            ["SocialFloorLionCardsSubmitted"] = "4",
            ["SocialFloorCouragePlayed"] = bool.TrueString,
            ["SocialFloorCouragePowerPresent"] = bool.TrueString,
            ["SocialFloorCouragePendingActivations"] = "1",
            ["SocialFloorCourageEnergyActive"] = bool.TrueString,
            ["SocialFloorCourageRemoveAtTurnEnd"] = bool.FalseString
        };
        foreach ((string key, string value) in lionValues)
        {
            SetSavedValue(saved, key, value);
        }

        var restored = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        restored.LoadCustomState(new Dictionary<string, string>(saved));
        restored.GenerateMonstersWithSlots(NullRunState.Instance);
        Dictionary<string, string> roundTripped = restored.SaveCustomState();

        Require(restored.Trial == SocialFloorTrial.Lion
                && restored.CurrentPhase == 4
                && restored.TrialRound == 1,
            "Social floor save/load did not restore the Lion trial position.");
        Require(restored.SetupComplete
                && restored.PlayersHealed
                && restored.DestroyedCrystalMask == 0b111
                && restored.DestroyedCrystalCount == 3
                && restored.AreAllCrystalsDestroyed
                && restored.DestroyedFaceMask == 0b0101
                && restored.DestroyedFaceCount == 2
                && !restored.AreAllFacesDestroyed,
            "Social floor save/load did not restore setup/heal/summon masks.");
        Require(!restored.HasPendingTrial
                && restored.PendingTrial == SocialFloorTrial.Lion,
            "Social floor save/load did not restore the Lion transition state.");
        Require(restored.ScaredyCatPlayerNetId == 101
                && restored.OzmaPlayerNetId == null,
            "Social floor save/load did not restore per-player role NetIds.");
        Require(!restored.CowardApplied
                && !restored.OzmaReplacementPending
                && restored.PowderCost == 10
                && !restored.Transformed
                && !restored.FinalStrikeTriggered
                && restored.PlannedMove == FalseThroneMove.StunTrial
                && restored.ParticipantCount == 3
                && restored.SavedBossHp == 888
                && restored.SavedBossChao == 150
                && restored.SavedSummonVitals
                    == "face_2:40:12;face_4:60:8"
                && restored.SavedWisdomStacks == "101:4;202:0;303:5"
                && restored.SavedWisdomCards == "101:1;202:4;303:0"
                && restored.LionCardsSubmitted == 4
                && restored.CouragePlayed
                && restored.CouragePowerPresent
                && restored.CouragePendingActivations == 1
                && restored.CourageEnergyActive
                && !restored.CourageRemoveAtTurnEnd,
            "Social floor Lion save/load omitted authoritative v2 state.");

        var lionMonsters = restored.MonstersWithSlots.ToArray();
        Require(lionMonsters.Length == 3
                && lionMonsters[0].Item1 is FalseThrone
                && lionMonsters.Where(static entry =>
                        entry.Item1 is ScowlingFace)
                    .Select(static entry => entry.Item2)
                    .SequenceEqual(
                        new[]
                        {
                            SocialFloorLiberationEncounter.FaceSlotTwo,
                            SocialFloorLiberationEncounter.FaceSlotFour
                        })
                && lionMonsters.Where(static entry =>
                        entry.Item1 is ScowlingFace)
                    .All(static entry =>
                        entry.Item1.MinInitialHp == 65
                        && entry.Item1.MaxInitialHp == 65),
            "Lion load did not reconstruct only surviving three-player faces.");

        var runtimeValues = new Dictionary<string, string>(saved,
            StringComparer.Ordinal)
        {
            ["SocialFloorBossHp"] = "444",
            ["SocialFloorBossChao"] = "75"
        };
        var runtimeRestored = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        runtimeRestored.LoadCustomState(runtimeValues);
        runtimeRestored.GenerateMonstersWithSlots(NullRunState.Instance);
        var runtimeCombat = new CombatState(runtimeRestored);
        for (int playerIndex = 0; playerIndex < 3; playerIndex++)
        {
            runtimeCombat.AddPlayer(Player.CreateForNewRun<Ironclad>(
                UnlockState.none,
                (ulong)playerIndex + 101));
        }
        foreach ((MonsterModel monster, string? slot) in
                 runtimeRestored.MonstersWithSlots)
        {
            runtimeCombat.AddCreature(runtimeCombat.CreateCreature(
                monster,
                CombatSide.Enemy,
                slot));
        }
        FalseThrone runtimeBoss = runtimeCombat.Enemies
            .Select(static creature => creature.Monster)
            .OfType<FalseThrone>()
            .Single();
        InvokePrivateInstance(
            runtimeRestored,
            "RestoreEnemyVitalsAfterLoad",
            runtimeBoss,
            runtimeCombat);
        LibraryCreature restoredBossCreature = RequireLibraryCreature(
            runtimeBoss.Creature,
            "restored False Throne");
        LibraryCreature restoredFaceTwo = RequireLibraryCreature(
            runtimeCombat.Enemies.Single(static creature =>
                creature.SlotName
                    == SocialFloorLiberationEncounter.FaceSlotTwo),
            "restored face 2");
        LibraryCreature restoredFaceFour = RequireLibraryCreature(
            runtimeCombat.Enemies.Single(static creature =>
                creature.SlotName
                    == SocialFloorLiberationEncounter.FaceSlotFour),
            "restored face 4");
        Require(restoredBossCreature.MaxHp
                    == SocialFloorLiberationExactVitalsPatch.ResolveBossHp(
                        runtimeBoss.UsesToughValues)
                && restoredBossCreature.CurrentHp == 444
                && restoredBossCreature.MaxChaoValue
                    == FalseThrone.ChaoResistance
                && restoredBossCreature.CurrentChaoValue == 75
                && restoredFaceTwo.MaxHp == 65
                && restoredFaceTwo.CurrentHp == 40
                && restoredFaceTwo.MaxChaoValue
                    == ScowlingFace.ChaoResistance
                && restoredFaceTwo.CurrentChaoValue == 12
                && restoredFaceFour.MaxHp == 65
                && restoredFaceFour.CurrentHp == 60
                && restoredFaceFour.MaxChaoValue
                    == ScowlingFace.ChaoResistance
                && restoredFaceFour.CurrentChaoValue == 8,
            "Exact multiplayer normalization overwrote saved remaining HP/"
            + "Chao values instead of restoring them after creation.");

        Require(saved.Count == roundTripped.Count
                && saved.All(entry => roundTripped.TryGetValue(
                        entry.Key,
                        out string? value)
                    && string.Equals(
                        value,
                        entry.Value,
                        StringComparison.Ordinal)),
            "Social floor Lion state changed during v2 save/load round-trip.");

        var rageValues = new Dictionary<string, string>(saved,
            StringComparer.Ordinal)
        {
            ["SocialFloorTrial"] =
                ((int)SocialFloorTrial.Rage).ToString(),
            ["SocialFloorTrialRound"] = "2",
            ["SocialFloorDestroyedFaceMask"] = "15",
            ["SocialFloorPendingTrial"] =
                ((int)SocialFloorTrial.Rage).ToString(),
            ["SocialFloorOzmaPlayerNetId"] = "202",
            ["SocialFloorCowardApplied"] = bool.TrueString,
            ["SocialFloorPowderCost"] = "0",
            ["SocialFloorTransformed"] = bool.TrueString,
            ["SocialFloorFinalStrikeTriggered"] = bool.TrueString,
            ["SocialFloorPlannedMove"] =
                ((int)FalseThroneMove.FunIsOver).ToString(),
            ["SocialFloorParticipantCount"] = "4",
            ["SocialFloorBossHp"] = "321",
            ["SocialFloorBossChao"] = "77",
            ["SocialFloorSummonVitals"] = string.Empty,
            ["SocialFloorLionCardsSubmitted"] = "5",
            ["SocialFloorCouragePowerPresent"] = bool.FalseString,
            ["SocialFloorCouragePendingActivations"] = "0",
            ["SocialFloorCourageEnergyActive"] = bool.FalseString,
            ["SocialFloorCourageRemoveAtTurnEnd"] = bool.FalseString
        };
        var rage = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        rage.LoadCustomState(rageValues);
        rage.GenerateMonstersWithSlots(NullRunState.Instance);
        Dictionary<string, string> rageRoundTripped = rage.SaveCustomState();
        Require(rage.Trial == SocialFloorTrial.Rage
                && rage.TrialRound == 2
                && rage.OzmaPlayerNetId == 202
                && rage.PowderCost == 0
                && rage.Transformed
                && rage.FinalStrikeTriggered
                && rage.ParticipantCount == 4
                && rage.SavedBossHp == 321
                && rage.SavedBossChao == 77
                && rage.MonstersWithSlots.Count == 1,
            "Social floor Rage save/load did not preserve its valid final state.");
        Require(rageValues.Count == rageRoundTripped.Count
                && rageValues.All(entry => rageRoundTripped.TryGetValue(
                        entry.Key,
                        out string? value)
                    && string.Equals(
                        value,
                        entry.Value,
                        StringComparison.Ordinal)),
            "Social floor Rage state changed during v2 save/load round-trip.");

        var normalized = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        normalized.LoadCustomState(new Dictionary<string, string>
        {
            ["SocialFloorStateVersion"] = "2",
            ["SocialFloorTrial"] = "999",
            ["SocialFloorTrialRound"] = int.MaxValue.ToString(),
            ["SocialFloorDestroyedCrystalMask"] = "255",
            ["SocialFloorDestroyedFaceMask"] = "255",
            ["SocialFloorHasPendingTrial"] = bool.TrueString,
            ["SocialFloorPendingTrial"] =
                ((int)SocialFloorTrial.Initial).ToString(),
            ["SocialFloorScaredyCatPlayerNetId"] = "-1",
            ["SocialFloorOzmaPlayerNetId"] =
                "18446744073709551616",
            ["SocialFloorOzmaReplacementPending"] = bool.TrueString,
            ["SocialFloorPowderCost"] = "-3",
            ["SocialFloorTransformed"] = bool.TrueString,
            ["SocialFloorFinalStrikeTriggered"] = bool.TrueString,
            ["SocialFloorPlannedMove"] = "999",
            ["SocialFloorParticipantCount"] = "99",
            ["SocialFloorBossHp"] = "not-an-int",
            ["SocialFloorBossChao"] = "2147483648",
            ["SocialFloorLionCardsSubmitted"] = "99",
            ["SocialFloorCouragePendingActivations"] = "99"
        });
        Require(normalized.Trial == SocialFloorTrial.Initial
                && normalized.TrialRound == 1_000_000
                && normalized.DestroyedCrystalMask == 0b111
                && normalized.DestroyedFaceMask == 0b1111
                && !normalized.HasPendingTrial
                && normalized.PendingTrial == SocialFloorTrial.Initial
                && normalized.ScaredyCatPlayerNetId == null
                && normalized.OzmaPlayerNetId == null
                && !normalized.OzmaReplacementPending
                && normalized.PowderCost == 0
                && !normalized.Transformed
                && !normalized.FinalStrikeTriggered
                && normalized.PlannedMove == FalseThroneMove.InitialSequence
                && normalized.ParticipantCount == 4
                && normalized.SavedBossHp == -1
                && normalized.SavedBossChao == -1
                && normalized.LionCardsSubmitted == 5
                && normalized.CouragePendingActivations == 2,
            "Social floor v2 load no longer normalizes invalid values safely.");

        for (int index = 0; index < 7; index++)
        {
            int supplied = new[] { -10, 0, 1, 2, 3, 4, 99 }[index];
            int expected = new[] { 1, 1, 1, 2, 3, 4, 4 }[index];
            var participant = (SocialFloorLiberationEncounter)ModelDb
                .Encounter<SocialFloorLiberationEncounter>()
                .ToMutable();
            participant.LoadCustomState(new Dictionary<string, string>
            {
                ["SocialFloorParticipantCount"] = supplied.ToString(),
                ["SocialFloorTrialRound"] = "-9"
            });
            Require(participant.ParticipantCount == expected
                    && participant.TrialRound == 0,
                $"Participant-count/round clamp failed for {supplied}.");
        }

        var untransformedRage = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        untransformedRage.LoadCustomState(new Dictionary<string, string>
        {
            ["SocialFloorTrial"] =
                ((int)SocialFloorTrial.Rage).ToString(),
            ["SocialFloorTransformed"] = bool.FalseString,
            ["SocialFloorFinalStrikeTriggered"] = bool.TrueString
        });
        Require(!untransformedRage.Transformed
                && !untransformedRage.FinalStrikeTriggered,
            "Untransformed Rage load retained an impossible final strike.");

        Type encounterType = typeof(SocialFloorLiberationEncounter);
        IReadOnlyDictionary<ulong, int> playerValues =
            InvokePrivateStatic<IReadOnlyDictionary<ulong, int>>(
                encounterType,
                "ParsePlayerValues",
                "1:3;bad;2:-5;18446744073709551616:7;3:not;1:4;;");
        Require(playerValues.Count == 2
                && playerValues[1] == 4
                && playerValues[2] == 0,
            "Player-value parser did not safely reject disaster input.");

        IReadOnlyDictionary<string, (int Hp, int Chao)> summonValues =
            InvokePrivateStatic<
                IReadOnlyDictionary<string, (int Hp, int Chao)>>(
                encounterType,
                "ParseSummonVitals",
                "face_1:45:10;evil:99:1;face_2:-1:5;"
                + "face_3:20:-1;crystal_1:40:5;face_4:not:3;"
                + "face_1:50:20;;");
        Require(summonValues.Count == 2
                && summonValues["face_1"] == (50, 20)
                && summonValues["crystal_1"] == (40, 5),
            "Summon-vitals parser did not safely reject disaster input.");

        Require(encounterType.GetMethod(
                    "RestoreRuntimeStateAfterLoad",
                    BindingFlags.Instance | BindingFlags.NonPublic) != null
                && encounterType.GetMethod(
                    "RestoreEnemyVitalsAfterLoad",
                    BindingFlags.Instance | BindingFlags.NonPublic) != null
                && encounterType.GetMethod(
                    "RestoreChaoValueEarly",
                    BindingFlags.Static | BindingFlags.NonPublic) != null,
            "Social floor v2 runtime restore seams are missing.");
    }

    private static void VerifyMultiplayerScaling()
    {
        Require(
            SocialFloorLiberationEncounter.ResolveFaceHpForPlayerCount(1)
                == 50
            &&
            SocialFloorLiberationEncounter.ResolveFaceHpForPlayerCount(2)
                == 55
            && SocialFloorLiberationEncounter.ResolveFaceHpForPlayerCount(3)
                == 65
            && SocialFloorLiberationEncounter.ResolveFaceHpForPlayerCount(4)
                == 75
            && SocialFloorLiberationEncounter.ResolveFaceHpForPlayerCount(5)
                == 75
            && SocialFloorLiberationEncounter.ResolveFaceHpForPlayerCount(0)
                == 50,
            "Scowling Face HP must be 50/55/65/75 for 1/2/3/4+ players.");

        Require(SocialFloorLiberationExactVitalsPatch.ResolveBossHp(
                    usesToughValues: false) == FalseThrone.NormalHp
                && SocialFloorLiberationExactVitalsPatch.ResolveBossHp(
                    usesToughValues: true) == FalseThrone.ToughHp,
            "Exact-vitals patch lost the 888/999 ToughEnemies branch.");

        MethodInfo scaleMethod = AccessTools.Method(
            typeof(Creature),
            nameof(Creature.ScaleMonsterHpForMultiplayer));
        Patches? scalePatches = Harmony.GetPatchInfo(scaleMethod);
        Patch? exactVitalsPostfix = scalePatches?.Postfixes
            .SingleOrDefault(static patch =>
                patch.PatchMethod.DeclaringType
                    == typeof(SocialFloorLiberationExactVitalsPatch));
        Require(exactVitalsPostfix != null
                && exactVitalsPostfix.priority == Priority.Last
                && exactVitalsPostfix.after.Contains(
                    "LibraryOfRuinaLib",
                    StringComparer.Ordinal),
            "Exact-vitals postfix must run last and after LibraryOfRuinaLib's "
            + "Chao scaler.");

        for (int playerCount = 1; playerCount <= 4; playerCount++)
        {
            var encounter = (SocialFloorLiberationEncounter)ModelDb
                .Encounter<SocialFloorLiberationEncounter>()
                .ToMutable();
            var combatState = new CombatState(encounter);
            for (int playerIndex = 0;
                 playerIndex < playerCount;
                 playerIndex++)
            {
                combatState.AddPlayer(Player.CreateForNewRun<Ironclad>(
                    UnlockState.none,
                    (ulong)playerIndex + 1));
            }

            FalseThrone bossModel = (FalseThrone)ModelDb
                .Monster<FalseThrone>()
                .ToMutable();
            int expectedBossHp = SocialFloorLiberationExactVitalsPatch
                .ResolveBossHp(bossModel.UsesToughValues);
            LibraryCreature boss = RequireLibraryCreature(
                combatState.CreateCreature(
                    bossModel,
                    CombatSide.Enemy,
                    SocialFloorLiberationEncounter.FalseThroneSlot),
                "False Throne");
            RequireExactVitals(
                boss,
                expectedBossHp,
                FalseThrone.ChaoResistance,
                playerCount,
                "False Throne");

            LibraryCreature crystal = RequireLibraryCreature(
                combatState.CreateCreature(
                    ModelDb.Monster<EmeraldCrystal>().ToMutable(),
                    CombatSide.Enemy,
                    SocialFloorLiberationEncounter.CrystalSlotOne),
                "Emerald Crystal");
            RequireExactVitals(
                crystal,
                EmeraldCrystal.Hp,
                EmeraldCrystal.ChaoResistance,
                playerCount,
                "Emerald Crystal");

            var face = (ScowlingFace)ModelDb.Monster<ScowlingFace>()
                .ToMutable();
            int expected = SocialFloorLiberationEncounter
                .ResolveFaceHpForPlayerCount(playerCount);
            face.ConfigureInitialHp(expected);
            LibraryCreature faceCreature = RequireLibraryCreature(
                combatState.CreateCreature(
                    face,
                    CombatSide.Enemy,
                    SocialFloorLiberationEncounter.FaceSlotOne),
                "Scowling Face");
            Require(face.MinInitialHp == expected
                    && face.MaxInitialHp == expected,
                $"Scowling Face failed configured {playerCount}-player HP.");
            RequireExactVitals(
                faceCreature,
                expected,
                ScowlingFace.ChaoResistance,
                playerCount,
                "Scowling Face");
        }
    }

    private static LibraryCreature RequireLibraryCreature(
        Creature creature,
        string label) =>
        creature as LibraryCreature
        ?? throw new InvalidOperationException(
            label + " was not created through LibraryOfRuinaLib's creature "
            + "transpiler.");

    private static void RequireExactVitals(
        LibraryCreature creature,
        int expectedHp,
        int expectedChao,
        int playerCount,
        string label)
    {
        Require(creature.MaxHp == expectedHp
                && creature.CurrentHp == expectedHp
                && creature.MaxChaoValue == expectedChao
                && creature.CurrentChaoValue == expectedChao,
            $"{label} final {playerCount}-player vitals were "
            + $"HP {creature.CurrentHp}/{creature.MaxHp}, Chao "
            + $"{creature.CurrentChaoValue}/{creature.MaxChaoValue}; "
            + $"expected {expectedHp}/{expectedHp}, "
            + $"{expectedChao}/{expectedChao}.");
    }

    private static async Task VerifyPlayerMechanicsConstants()
    {
        Require(SocialFloorScaredyCatPower.CardLimit == 5,
            "Scaredy Cat card limit must be 5.");
        Require(SocialFloorCouragePower.EnergyMaximum == 5
                && SocialFloorCouragePower.StrongStacks == 10
                && SocialFloorCouragePower.TotalActivations == 2,
            "Courage must provide 5 energy/10 Strength for two activations.");
        Require(SocialFloorCowardPower.DebuffStacks == 9,
            "Coward debuff stacks must be 9.");
        Require(SocialFloorOzmaPower.AttackOrPowerCostReduction == 1
                && SocialFloorOzmaPower.SkillCostIncrease == 1,
            "Ozma powder-cost adjustment constants changed unexpectedly.");

        var courage = (SocialFloorCouragePower)ModelDb
            .Power<SocialFloorCouragePower>()
            .ToMutable();
        Require(courage.Type == PowerType.Buff
                && courage.StackType == PowerStackType.Single
                && courage.PendingTurnStartActivations == 0
                && !courage.IsEnergyOverrideActive
                && !courage.RemoveAtNextPlayerTurnEnd,
            "Courage default lifecycle state changed.");
        WriteProperty(
            courage,
            nameof(courage.SerializedHolderNetId),
            "100");
        WriteProperty(
            courage,
            nameof(courage.PendingTurnStartActivations),
            1);
        WriteProperty(
            courage,
            nameof(courage.IsEnergyOverrideActive),
            true);
        WriteProperty(
            courage,
            nameof(courage.RemoveAtNextPlayerTurnEnd),
            true);
        await courage.AfterRemoved(null!);
        Require(courage.HolderNetId == 100UL
                && courage.PendingTurnStartActivations == 0
                && !courage.IsEnergyOverrideActive
                && !courage.RemoveAtNextPlayerTurnEnd,
            "Courage removal did not clear its two-turn transient state.");

        Player detachedPlayer = Player.CreateForNewRun<Ironclad>(
            UnlockState.none,
            ulong.MaxValue);
        courage.RestoreState(
            detachedPlayer,
            int.MaxValue,
            isEnergyOverrideActive: false,
            removeAtNextPlayerTurnEnd: true);
        Require(courage.HolderNetId == ulong.MaxValue
                && courage.PendingTurnStartActivations == 2
                && !courage.IsEnergyOverrideActive
                && !courage.RemoveAtNextPlayerTurnEnd,
            "Courage RestoreState did not clamp/normalize invalid lifecycle "
            + "state.");
        courage.RestoreState(
            detachedPlayer,
            1,
            isEnergyOverrideActive: true,
            removeAtNextPlayerTurnEnd: true);
        Require(courage.PendingTurnStartActivations == 1
                && courage.IsEnergyOverrideActive
                && courage.RemoveAtNextPlayerTurnEnd,
            "Courage RestoreState did not restore a valid active lifecycle.");

        var cat = (SocialFloorScaredyCatPower)ModelDb
            .Power<SocialFloorScaredyCatPower>()
            .ToMutable();
        Require(cat.Type == PowerType.Buff
                && cat.StackType == PowerStackType.Single
                && cat.IsHolderActive
                && cat.CardsSubmittedThisTurn == 0
                && !cat.CourageCardGranted,
            "Scaredy Cat default lifecycle state changed.");
        cat.MarkCourageCardGranted();
        WriteProperty(cat, nameof(cat.CardsSubmittedThisTurn), 5);
        await cat.BeforeSideTurnStart(
            null!,
            CombatSide.Player,
            [],
            null!);
        Require(cat.CourageCardGranted
                && cat.CardsSubmittedThisTurn == 0,
            "Scaredy Cat did not reset its per-turn five-card counter.");
        cat.InvalidateHolder();
        Require(!cat.IsHolderActive,
            "Scaredy Cat holder invalidation did not end the trial limit.");
        cat.RestoreState(
            detachedPlayer,
            int.MaxValue,
            courageCardGranted: true,
            isHolderActive: false);
        Require(cat.HolderNetId == ulong.MaxValue
                && cat.CardsSubmittedThisTurn == 5
                && cat.CourageCardGranted
                && !cat.IsHolderActive,
            "Scaredy Cat RestoreState did not clamp/restore its lifecycle.");

        var coward = (SocialFloorCowardPower)ModelDb
            .Power<SocialFloorCowardPower>()
            .ToMutable();
        Require(coward.Type == PowerType.None
                && coward.StackType == PowerStackType.Single,
            "Coward must remain a persistent, non-cleansable passive power.");

        var ozma = (SocialFloorOzmaPower)ModelDb
            .Power<SocialFloorOzmaPower>()
            .ToMutable();
        Require(ozma.Type == PowerType.Buff
                && ozma.StackType == PowerStackType.Single
                && ozma.IsHolderActive,
            "Ozma default lifecycle state changed.");
        ozma.InvalidateHolder();
        Require(!ozma.IsHolderActive,
            "Ozma holder invalidation did not disable cost updates.");

        Require(await SocialFloorPlayerMechanics.RestoreScaredyCat(
                    detachedPlayer,
                    cardsSubmittedThisTurn: 4,
                    courageCardGranted: true,
                    isHolderActive: true,
                    ensureCourageCard: true) == null
                && await SocialFloorPlayerMechanics.RestoreCourage(
                    detachedPlayer,
                    pendingTurnStartActivations: 1,
                    isEnergyOverrideActive: true,
                    removeAtNextPlayerTurnEnd: true) == null
                && await SocialFloorPlayerMechanics.RestoreCoward(
                    detachedPlayer) == null
                && await SocialFloorPlayerMechanics.RestoreOzma(
                    detachedPlayer,
                    internalCost: 3,
                    ensurePowder: true) == null,
            "Detached-player Restore* APIs did not fail safely without combat "
            + "state.");

        await VerifyCourageRestoreLifecycle();

        foreach (PowerModel power in new PowerModel[]
                 { courage, cat, coward, ozma })
        {
            Require(ResourceLoader.Exists(power.PackedIconPath),
                "Missing Social player-power packed icon: "
                + power.PackedIconPath);
        }
    }

    private static async Task VerifyCourageRestoreLifecycle()
    {
        (Player pendingPlayer, CombatState pendingCombat) =
            CreateCourageRestoreCombat<Ironclad>(7001UL);
        PlayerCombatState pendingState = pendingPlayer.PlayerCombatState
            ?? throw new InvalidOperationException(
                "Pending Courage player has no combat state.");
        pendingState.Energy = 1;
        ApplyDetachedPower<LibraryWeakPower>(pendingPlayer.Creature, 9);
        ApplyDetachedPower<LibraryDisarmPower>(pendingPlayer.Creature, 9);

        SocialFloorCouragePower pending =
            await SocialFloorPlayerMechanics.RestoreCourage(
                pendingPlayer,
                pendingTurnStartActivations: 1,
                isEnergyOverrideActive: true,
                removeAtNextPlayerTurnEnd: false)
            ?? throw new InvalidOperationException(
                "Pending Courage restore returned null in combat.");
        Require(pending.PendingTurnStartActivations == 1
                && pending.IsEnergyOverrideActive
                && !pending.RemoveAtNextPlayerTurnEnd
                && pendingState.Energy == 1
                && pendingPlayer.Creature
                    .GetPowerAmount<LibraryWeakPower>() == 9
                && pendingPlayer.Creature
                    .GetPowerAmount<LibraryDisarmPower>() == 9
                && pendingPlayer.Creature
                    .GetPower<LibraryStrongPower>() == null,
            "Pending Courage restore replayed active effects before the next "
            + "normal turn start.");

        await pending.AfterSideTurnStart(
            CombatSide.Player,
            [pendingPlayer.Creature],
            pendingCombat);
        Require(pending.PendingTurnStartActivations == 0
                && pending.IsEnergyOverrideActive
                && pending.RemoveAtNextPlayerTurnEnd
                && pendingState.MaxEnergy
                    == SocialFloorCouragePower.EnergyMaximum
                && pendingState.Energy
                    == SocialFloorCouragePower.EnergyMaximum
                && pendingPlayer.Creature
                    .GetPower<LibraryWeakPower>() == null
                && pendingPlayer.Creature
                    .GetPower<LibraryDisarmPower>() == null
                && pendingPlayer.Creature
                    .GetPowerAmount<LibraryStrongPower>()
                    == SocialFloorCouragePower.StrongStacks,
            "Pending Courage did not activate exactly once at normal turn "
            + "start.");

        (Player finalPlayer, CombatState _) =
            CreateCourageRestoreCombat<Ironclad>(7002UL);
        PlayerCombatState finalState = finalPlayer.PlayerCombatState
            ?? throw new InvalidOperationException(
                "Final Courage player has no combat state.");
        Require(!LibraryLight.TryGetState(finalPlayer, out _),
            "No-Light Courage regression player unexpectedly had Light.");
        const int SurplusEnergy = 10;
        finalState.Energy = SurplusEnergy;
        ApplyDetachedPower<LibraryWeakPower>(finalPlayer.Creature, 9);
        ApplyDetachedPower<LibraryDisarmPower>(finalPlayer.Creature, 9);

        SocialFloorCouragePower final =
            await SocialFloorPlayerMechanics.RestoreCourage(
                finalPlayer,
                pendingTurnStartActivations: 0,
                isEnergyOverrideActive: true,
                removeAtNextPlayerTurnEnd: true)
            ?? throw new InvalidOperationException(
                "Final Courage restore returned null in combat.");
        Require(final.PendingTurnStartActivations == 0
                && final.IsEnergyOverrideActive
                && final.RemoveAtNextPlayerTurnEnd
                && finalState.MaxEnergy
                    == SocialFloorCouragePower.EnergyMaximum
                && finalState.Energy
                    == SurplusEnergy
                && finalPlayer.Creature
                    .GetPower<LibraryWeakPower>() == null
                && finalPlayer.Creature
                    .GetPower<LibraryDisarmPower>() == null
                && finalPlayer.Creature
                    .GetPowerAmount<LibraryStrongPower>()
                    == SocialFloorCouragePower.StrongStacks,
            "Final-turn Courage restore changed surplus Energy or failed to "
            + "replay its current-turn effects without extending lifecycle "
            + "state.");

        await final.AfterSideTurnEnd(
            new ThrowingPlayerChoiceContext(),
            CombatSide.Player,
            [finalPlayer.Creature]);
        Require(finalPlayer.Creature
                    .GetPower<SocialFloorCouragePower>() == null
                && final.PendingTurnStartActivations == 0
                && !final.IsEnergyOverrideActive
                && !final.RemoveAtNextPlayerTurnEnd,
            "Restored final-turn Courage was not removed at player turn end.");

        LibrarySpeedDice.ForCharacter<Silent>(
                "social_floor_verifier_courage_light",
                new LibrarySpeedDiceOptions(0, 1, 1))
            .WithLight(new LibraryLightOptions(
                starting: 1,
                baseMaximum: 4,
                maximumPerEmotionLevel: 0,
                recoveryPerTurn: 0,
                refillOnLevelIncrease: false))
            .Register();
        (Player lightPlayer, CombatState _) =
            CreateCourageRestoreCombat<Silent>(7003UL);
        Require(LibraryLight.TryGetState(
                    lightPlayer,
                    out LibraryLightState? light)
                && light != null,
            "Verifier could not safely construct a local LibraryLight state.");
        await light!.Set(0);
        PlayerCombatState lightPlayerState = lightPlayer.PlayerCombatState
            ?? throw new InvalidOperationException(
                "Light Courage player has no combat state.");
        lightPlayerState.Energy = 0;

        SocialFloorCouragePower lightFinal =
            await SocialFloorPlayerMechanics.RestoreCourage(
                lightPlayer,
                pendingTurnStartActivations: 0,
                isEnergyOverrideActive: true,
                removeAtNextPlayerTurnEnd: true)
            ?? throw new InvalidOperationException(
                "Light Courage restore returned null in combat.");
        Require(light.Current == light.Maximum
                && light.Maximum == 4
                && lightPlayerState.Energy
                    == SocialFloorCouragePower.EnergyMaximum
                && lightPlayer.Creature
                    .GetPowerAmount<LibraryStrongPower>()
                    == SocialFloorCouragePower.StrongStacks
                && lightFinal.PendingTurnStartActivations == 0
                && lightFinal.RemoveAtNextPlayerTurnEnd,
            "Final-turn Courage restore did not reset registered Light or "
            + "preserve removal state.");
    }

    private static (Player Player, CombatState CombatState)
        CreateCourageRestoreCombat<TCharacter>(ulong netId)
        where TCharacter : CharacterModel
    {
        Player player = Player.CreateForNewRun<TCharacter>(
            UnlockState.none,
            netId);
        player.ResetCombatState();
        var encounter = (SocialFloorLiberationEncounter)ModelDb
            .Encounter<SocialFloorLiberationEncounter>()
            .ToMutable();
        var combatState = new CombatState(encounter);
        combatState.AddPlayer(player);
        return (player, combatState);
    }

    private static TPower ApplyDetachedPower<TPower>(
        Creature owner,
        int amount)
        where TPower : PowerModel
    {
        var power = (TPower)ModelDb.Power<TPower>().ToMutable();
        power.ApplyInternal(owner, amount, silent: true);
        return power;
    }

    private static void VerifySpecialCardContract()
    {
        var courage = (SocialFloorCourageCard)ModelDb
            .Card<SocialFloorCourageCard>()
            .ToMutable();
        var powder = (SocialFloorMagicalPowderCard)ModelDb
            .Card<SocialFloorMagicalPowderCard>()
            .ToMutable();

        Require(courage.Type == CardType.Skill
                && courage.Rarity == CardRarity.Token
                && courage.TargetType == TargetType.Self
                && courage.EnergyCost.Canonical == 0
                && courage.MaxUpgradeLevel == 0,
            "Courage must be a 0-cost, self-targeted token Skill.");
        Require(powder.Type == CardType.Attack
                && powder.Rarity == CardRarity.Token
                && powder.TargetType == TargetType.AnyEnemy
                && powder.EnergyCost.Canonical
                    == SocialFloorMagicalPowderCard.DefaultInternalCost
                && powder.MaxUpgradeLevel == 0,
            "Magical Powder must be a 10-cost enemy-targeted token Attack.");
        Require(!courage.ShouldShowInCardLibrary
                && !powder.ShouldShowInCardLibrary
                && !courage.CanBeGeneratedInCombat
                && !powder.CanBeGeneratedInCombat,
            "Courage/Powder must stay hidden and encounter-generated only.");
        Require(courage.Pool is TokenCardPool
                && powder.Pool is TokenCardPool
                && courage.VisualCardPool is ColorlessCardPool
                && powder.VisualCardPool is ColorlessCardPool,
            "Courage/Powder token or visual card-pool routing changed.");

        HashSet<CardKeyword> expectedKeywords =
            [CardKeyword.Retain, CardKeyword.Exhaust];
        Require(courage.CanonicalKeywords.ToHashSet().SetEquals(
                    expectedKeywords)
                && powder.CanonicalKeywords.ToHashSet().SetEquals(
                    expectedKeywords),
            "Courage/Powder must have exactly Retain and Exhaust.");
        Require(ResourceLoader.Exists(courage.PortraitPath)
                && ResourceLoader.Exists(powder.PortraitPath),
            "Courage or Magical Powder portrait is missing.");

        Require(SocialFloorMagicalPowderCard.DefaultInternalCost == 10
                && powder.InternalCost == 10,
            "Magical Powder must begin at internal cost 10.");
        Require(SocialFloorPlayerMechanics
                .VerifyHolderNetIdSerializationRoundTrip(ulong.MaxValue),
            "Social floor NetId string conversion did not preserve ulong.MaxValue.");
        Require(powder.IsHolderValid && !powder.IsPowderReady,
            "Magical Powder must begin valid but unplayable until cost reaches 0.");

        WriteProperty(powder, nameof(powder.InternalCost), 0);
        Require(powder.IsPowderReady,
            "Magical Powder did not become ready at internal cost 0.");
        WriteProperty(powder, nameof(powder.IsHolderValid), false);
        Require(!powder.IsPowderReady,
            "Invalidated Magical Powder remained ready at internal cost 0.");

        powder = (SocialFloorMagicalPowderCard)ModelDb
            .Card<SocialFloorMagicalPowderCard>()
            .ToMutable();
        Require(!ReadProtectedCardPlayable(powder),
            "10-cost Magical Powder bypassed its internal play gate.");
        powder.AdjustInternalCost(-7);
        Require(powder.InternalCost == 3
                && !powder.IsPowderReady
                && !ReadProtectedCardPlayable(powder),
            "Magical Powder -7 adjustment did not produce locked cost 3.");
        powder.AdjustInternalCost(-99);
        Require(powder.InternalCost == 0
                && powder.IsPowderReady
                && ReadProtectedCardPlayable(powder),
            "Magical Powder cost did not clamp to playable 0.");
        powder.AdjustInternalCost(2);
        Require(powder.InternalCost == 2
                && !powder.IsPowderReady,
            "Magical Powder Skill adjustment did not increase cost by 2.");

        powder.EnergyCost.SetThisCombat(0);
        Require(powder.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0,
            "Verifier could not simulate an unrelated external cost reduction.");
        Require(powder.TryModifyEnergyCostInCombatLate(
                    powder,
                    0m,
                    out decimal lateCost)
                && lateCost == powder.InternalCost,
            "Magical Powder card-local late hook did not restore internal cost.");

        MethodInfo postfix = typeof(SocialFloorMagicalPowderCostPatch)
            .GetMethod(
                "Postfix",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(SocialFloorMagicalPowderCostPatch).FullName,
                "Postfix");
        object?[] args = [powder, 0m];
        postfix.Invoke(null, args);
        Require(args[1] is decimal patchedCost
                && patchedCost == powder.InternalCost,
            "Final Harmony cost hook allowed an external reduction to bypass "
            + "Magical Powder's internal cost.");

        WriteProperty(
            powder,
            nameof(powder.SerializedHolderNetId),
            ulong.MaxValue.ToString());
        WriteProperty(powder, nameof(powder.InternalCost), 7);
        WriteProperty(powder, nameof(powder.IsHolderValid), false);
        SerializableCard snapshot = powder.ToSerializable();
        Require(snapshot.Props != null
                && snapshot.Props.strings?.Any(entry =>
                    entry.name == nameof(powder.SerializedHolderNetId)
                    && entry.value == ulong.MaxValue.ToString()) == true,
            "Magical Powder ToSerializable omitted its string NetId.");
        var powderClone = (SocialFloorMagicalPowderCard)CardModel
            .FromSerializable(snapshot);
        Require(powderClone.HolderNetId == ulong.MaxValue
                && powderClone.InternalCost == 7
                && !powderClone.IsHolderValid
                && powderClone.DynamicVars["InternalCost"].BaseValue == 7m,
            "Magical Powder card serialization did not round-trip all saved "
            + "fields or refresh its AfterDeserialized dynamic cost.");
    }

    private static bool ReadProtectedCardPlayable(CardModel card)
    {
        PropertyInfo property = card.GetType().GetProperty(
                "IsPlayable",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(
                card.GetType().FullName,
                "IsPlayable");
        return (bool)property.GetValue(card)!;
    }

    private static void VerifySavedPropertyRegistration()
    {
        var expected = new Dictionary<Type, string[]>
        {
            [typeof(SocialFloorMagicalPowderCard)] =
                ["InternalCost", "SerializedHolderNetId", "IsHolderValid"]
        };
        // 能力状态不进存档也不同步（原版 NetFullCombatState 只带能力的 id 与层数），守住它们不再带 [SavedProperty]。
        var combatStateOnly = new Dictionary<Type, string[]>
        {
            [typeof(SocialFloorCouragePower)] =
            [
                "SerializedHolderNetId",
                "PendingTurnStartActivations",
                "IsEnergyOverrideActive",
                "RemoveAtNextPlayerTurnEnd"
            ],
            [typeof(SocialFloorScaredyCatPower)] =
            [
                "SerializedHolderNetId",
                "CardsSubmittedThisTurn",
                "CourageCardGranted",
                "IsHolderActive"
            ],
            [typeof(SocialFloorCowardPower)] = ["SerializedHolderNetId"],
            [typeof(SocialFloorOzmaPower)] =
                ["SerializedHolderNetId", "IsHolderActive"]
        };

        SavedPropertiesTypeCacheCompat.ClearAutoDiscoveryCache();
        Type[] registered = SavedPropertiesTypeCacheCompat
            .GetAllModSavedPropertyTypes();
        string schema = SavedPropertiesTypeCacheCompat
            .BuildSchemaFingerprintMaterial();
        foreach ((Type type, string[] properties) in expected)
        {
            Require(registered.Contains(type),
                "SavedProperty auto-discovery omitted " + type.Name + ".");
            Require(!SavedPropertiesTypeCacheCompat.ModSavedPropertyTypes
                    .Contains(type),
                type.Name + " changed the stable manual NetId ordering.");
            foreach (string propertyName in properties)
            {
                PropertyInfo property = type.GetProperty(
                        propertyName,
                        BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic)
                    ?? throw new MissingMemberException(
                        type.FullName,
                        propertyName);
                Require(property.IsDefined(
                            typeof(SavedPropertyAttribute),
                            inherit: false)
                        && schema.Contains(
                            "saved|" + type.FullName + "|" + propertyName + "|",
                            StringComparison.Ordinal),
                    type.Name + "." + propertyName
                    + " is missing from SavedProperty schema.");
            }
        }

        foreach ((Type type, string[] properties) in combatStateOnly)
        {
            Require(CombatStateProperties.IsTransient(type)
                    && properties.All(name => CombatStateProperties.IsListed(type, name)),
                type.Name + " is a SavedProperty carrier again, or a field is missing from the reload list.");
        }

        VerifyPlayerPowerSavedPropertiesRoundTrip();
    }

    private static void VerifyPlayerPowerSavedPropertiesRoundTrip()
    {
        const string netId = "18446744073709551615";

        var courage = (SocialFloorCouragePower)ModelDb
            .Power<SocialFloorCouragePower>().ToMutable();
        WriteProperty(
            courage,
            nameof(courage.SerializedHolderNetId),
            netId);
        WriteProperty(
            courage,
            nameof(courage.PendingTurnStartActivations),
            1);
        WriteProperty(
            courage,
            nameof(courage.IsEnergyOverrideActive),
            true);
        WriteProperty(
            courage,
            nameof(courage.RemoveAtNextPlayerTurnEnd),
            true);
        SavedProperties courageSaved = CombatStateProperties.From(courage)
            ?? throw new InvalidOperationException(
                "Courage SavedProperties were empty.");
        var courageClone = (SocialFloorCouragePower)ModelDb
            .Power<SocialFloorCouragePower>().ToMutable();
        courageSaved.Fill(courageClone);
        Require(courageClone.HolderNetId == ulong.MaxValue
                && courageClone.PendingTurnStartActivations == 1
                && courageClone.IsEnergyOverrideActive
                && courageClone.RemoveAtNextPlayerTurnEnd,
            "Courage SavedProperties did not round-trip.");

        var cat = (SocialFloorScaredyCatPower)ModelDb
            .Power<SocialFloorScaredyCatPower>().ToMutable();
        WriteProperty(cat, nameof(cat.SerializedHolderNetId), netId);
        WriteProperty(cat, nameof(cat.CardsSubmittedThisTurn), 5);
        WriteProperty(cat, nameof(cat.CourageCardGranted), true);
        WriteProperty(cat, nameof(cat.IsHolderActive), false);
        SavedProperties catSaved = CombatStateProperties.From(cat)
            ?? throw new InvalidOperationException(
                "Scaredy Cat SavedProperties were empty.");
        var catClone = (SocialFloorScaredyCatPower)ModelDb
            .Power<SocialFloorScaredyCatPower>().ToMutable();
        catSaved.Fill(catClone);
        Require(catClone.HolderNetId == ulong.MaxValue
                && catClone.CardsSubmittedThisTurn == 5
                && catClone.CourageCardGranted
                && !catClone.IsHolderActive,
            "Scaredy Cat SavedProperties did not round-trip.");

        var coward = (SocialFloorCowardPower)ModelDb
            .Power<SocialFloorCowardPower>().ToMutable();
        WriteProperty(coward, nameof(coward.SerializedHolderNetId), netId);
        SavedProperties cowardSaved = CombatStateProperties.From(coward)
            ?? throw new InvalidOperationException(
                "Coward SavedProperties were empty.");
        var cowardClone = (SocialFloorCowardPower)ModelDb
            .Power<SocialFloorCowardPower>().ToMutable();
        cowardSaved.Fill(cowardClone);
        Require(cowardClone.HolderNetId == ulong.MaxValue,
            "Coward SavedProperties did not round-trip.");

        var ozma = (SocialFloorOzmaPower)ModelDb
            .Power<SocialFloorOzmaPower>().ToMutable();
        WriteProperty(ozma, nameof(ozma.SerializedHolderNetId), netId);
        WriteProperty(ozma, nameof(ozma.IsHolderActive), false);
        SavedProperties ozmaSaved = CombatStateProperties.From(ozma)
            ?? throw new InvalidOperationException(
                "Ozma SavedProperties were empty.");
        var ozmaClone = (SocialFloorOzmaPower)ModelDb
            .Power<SocialFloorOzmaPower>().ToMutable();
        ozmaSaved.Fill(ozmaClone);
        Require(ozmaClone.HolderNetId == ulong.MaxValue
                && !ozmaClone.IsHolderActive,
            "Ozma SavedProperties did not round-trip.");
    }

    private static T ReadProperty<T>(
        object instance,
        Type instanceType,
        string propertyName)
    {
        PropertyInfo property = instanceType.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(
                instanceType.FullName,
                propertyName);
        return (T)property.GetValue(instance)!;
    }

    private static T ReadStaticField<T>(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(type.FullName, fieldName);
        return (T)field.GetValue(null)!;
    }

    private static T InvokePrivateStatic<T>(
        Type type,
        string methodName,
        params object?[] arguments)
    {
        MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, methodName);
        return (T)(method.Invoke(null, arguments)
            ?? throw new InvalidOperationException(
                type.Name + "." + methodName + " returned null."));
    }

    private static void InvokePrivateInstance(
        object instance,
        string methodName,
        params object?[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                instance.GetType().FullName,
                methodName);
        method.Invoke(instance, arguments);
    }

    private static void WriteProperty<T>(
        object instance,
        string propertyName,
        T value)
    {
        PropertyInfo property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(
                instance.GetType().FullName,
                propertyName);
        property.SetValue(instance, value);
    }

    private static void SetSavedValue(
        IDictionary<string, string> state,
        string key,
        string value)
    {
        Require(state.ContainsKey(key),
            "Social floor save state omitted " + key + ".");
        state[key] = value;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

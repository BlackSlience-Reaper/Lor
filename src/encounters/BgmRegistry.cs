using System;
using System.Diagnostics.CodeAnalysis;
using LibraryOfRuina.encounters.AddictedEmployee;
using LibraryOfRuina.encounters.AllAroundHelper;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.BigBadWolf;
using LibraryOfRuina.encounters.BigBird;
using LibraryOfRuina.encounters.BlueStar;
using LibraryOfRuina.encounters.BrotherhoodOfIron;
using LibraryOfRuina.encounters.BurrowingHeaven;
using LibraryOfRuina.encounters.CosmicFragment;
using LibraryOfRuina.encounters.DawnOffice;
using LibraryOfRuina.encounters.DeadButterfly;
using LibraryOfRuina.encounters.DespairKnight;
using LibraryOfRuina.encounters.FairyFestival;
using LibraryOfRuina.encounters.ForsakenMurderer;
using LibraryOfRuina.encounters.FuneralOfTheDeadButterflies;
using LibraryOfRuina.encounters.GalaxyChild;
using LibraryOfRuina.encounters.HappyTeddy;
using LibraryOfRuina.encounters.HeartOfAspiration;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.HookOffice;
using LibraryOfRuina.encounters.JudgementBird;
using LibraryOfRuina.encounters.KuroKumo;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.encounters.MusiciansOfBremen;
using LibraryOfRuina.encounters.PhilosophyFloorLiberation;
using LibraryOfRuina.encounters.PriceOfSilence;
using LibraryOfRuina.encounters.PunishingBird;
using LibraryOfRuina.encounters.QueenOfHatred;
using LibraryOfRuina.encounters.RedMist;
using LibraryOfRuina.encounters.RedShoes;
using LibraryOfRuina.encounters.ScarecrowSearchingForWisdom;
using LibraryOfRuina.encounters.SmilingBodies;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.encounters.SpiderBud;
using LibraryOfRuina.encounters.SpinyBus;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.encounters.TodaysShyLook;
using LibraryOfRuina.encounters.Tomerry;
using LibraryOfRuina.encounters.WarmheartedWoodsman;
using LibraryOfRuina.encounters.WedgeOffice;
using LibraryOfRuina.encounters.WrathServant;
using LibraryOfRuina.encounters.YunOffice;
using LibraryOfRuina.specialguests.Kali;
using LibraryOfRuina.specialguests.Rnfmabj;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.encounters;

/// <summary>
/// 遭遇与接待层到 BGM 配置的查找，以及接待遭遇按所抽背景层改用该层配置的规则。
/// </summary>
internal static class BgmRegistry
{
    private static readonly Dictionary<Type, EncounterBgmConfig> ConfigByEncounterType = new()
    {
        [typeof(KuroKumoNormal)] = EncounterBgmConfig.DeathBased(
            "KuroKumoBGM",
            "res://audio/bgm/kurokumo/kurokumo_guest_battle_1.ogg",
            "res://audio/bgm/kurokumo/kurokumo_guest_battle_2.ogg",
            "res://audio/bgm/kurokumo/kurokumo_guest_battle_3.ogg"),
        [typeof(MusiciansOfBremenNormal)] = EncounterBgmConfig.DeathBased(
            "MusiciansOfBremenBGM",
            "res://audio/bgm/musicians_of_bremen/musicians_of_bremen_battle_1.ogg",
            "res://audio/bgm/musicians_of_bremen/musicians_of_bremen_battle_2.ogg",
            "res://audio/bgm/musicians_of_bremen/musicians_of_bremen_battle_3.ogg"),
        [typeof(DawnOfficeNormal)] = EncounterBgmConfig.DeathBased(
            "DawnOfficeBGM",
            "res://audio/bgm/dawn_office/dawn_office_battle_1.ogg",
            "res://audio/bgm/dawn_office/dawn_office_battle_2.ogg",
            "res://audio/bgm/dawn_office/dawn_office_battle_3.ogg"),
        [typeof(WedgeOfficeNormal)] = EncounterBgmConfig.RoundBased(
            "WedgeOfficeBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(YunOfficeNormal)] = EncounterBgmConfig.RoundBased(
            "YunOfficeBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(HookOfficeStrong)] = EncounterBgmConfig.RoundBased(
            "HookOfficeBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BrotherhoodOfIronStrong)] = EncounterBgmConfig.RoundBased(
            "BrotherhoodOfIronBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(FinnWeak)] = EncounterBgmConfig.DeathBased(
            "FinnBGM",
            "res://audio/bgm/finn/finn_battle_1.ogg"),
        [typeof(HappyTeddyWeak)] = EncounterBgmConfig.RoundBased(
            "HappyTeddyBGM",
            new[]
            {
                "res://audio/bgm/scorched_girl/scorched_girl_battle_1.ogg",
                "res://audio/bgm/scorched_girl/scorched_girl_battle_2.ogg",
                "res://audio/bgm/scorched_girl/scorched_girl_battle_3.ogg"
            },
            volumeScale: 0.85f,
            4,
            7),
        [typeof(FairyFestivalStrong)] = EncounterBgmConfig.RoundBased(
            "FairyFestivalBGM",
            new[]
            {
                "res://audio/bgm/fairy_festival/history_layer_1.ogg",
                "res://audio/bgm/fairy_festival/history_layer_2.ogg",
                "res://audio/bgm/fairy_festival/history_layer_3.ogg"
            },
            volumeScale: 0.85f,
            4,
            7),
        [typeof(HistoryFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "AngelaLiberationBGM",
            HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(LiteratureFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "LiteratureFloorLiberationBGM",
            HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(NaturalFloorLiberation.NaturalFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "NaturalFloorLiberationBGM",
            LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(TechnologyFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "TechnologyFloorLiberationBGM",
            HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(ArtFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "ArtFloorLiberationBGM",
            HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(LanguageFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "LanguageFloorLiberationBGM",
            LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(PhilosophyFloorLiberationEncounter)] = EncounterBgmConfig.DynamicSourceBased(
            "PhilosophyFloorLiberationBGM",
            LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(SocialFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "SocialFloorLiberationBGM",
            LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(QueenOfHatredStrong)] = EncounterBgmConfig.RoundBased(
            "QueenOfHatredBGM",
            GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(RedMistElite)] = EncounterBgmConfig.DeathBased(
            "RedMistBGM",
            "res://audio/bgm/language_reception_floor/GeburaBattle1.ogg"),
        [typeof(KaliSpecialGuestEncounter)] = EncounterBgmConfig.DeathBased(
            "RedMistSpecialGuestBGM",
            "res://audio/bgm/language_reception_floor/GeburaBattle1.ogg"),
        [typeof(RnfmabjSpecialGuestEncounter)] = EncounterBgmConfig.DeathBased(
            "RnfmabjSpecialGuestBGM",
            RnfmabjSpecialGuestIds.BattleBgm),
        [typeof(ForsakenMurdererWeak)] = EncounterBgmConfig.RoundBased(
            "ForsakenMurdererBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(AllAroundHelperWeak)] = EncounterBgmConfig.RoundBased(
            "AllAroundHelperBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(AllAroundHelperStrong)] = EncounterBgmConfig.RoundBased(
            "AllAroundHelperBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(TodaysShyLookStrong)] = EncounterBgmConfig.RoundBased(
            "TodaysShyLookBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(SpiderBudStrong)] = EncounterBgmConfig.RoundBased(
            "SpiderBudBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(RedShoesStrong)] = EncounterBgmConfig.RoundBased(
            "RedShoesBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(DeadButterflyWeak)] = EncounterBgmConfig.RoundBased(
            "DeadButterflyBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(DeadButterflyStrong)] = EncounterBgmConfig.RoundBased(
            "DeadButterflyBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(FuneralOfTheDeadButterfliesEncounter)] = EncounterBgmConfig.DeathBased(
            "FuneralOfTheDeadButterfliesBGM",
            "res://audio/bgm/literature_reception_floor/literature_reception_floor_1.ogg"),
        [typeof(AddictedEmployeeWeak)] = EncounterBgmConfig.RoundBased(
            "AddictedEmployeeBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(AddictedEmployeeStrong)] = EncounterBgmConfig.RoundBased(
            "AddictedEmployeeBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(TomerryEncounter)] = EncounterBgmConfig.DeathBased(
            "TomerryBGM",
            "res://audio/bgm/warp_train/from_a_place_of_love.ogg"),
        [typeof(CosmicFragmentWeak)] = EncounterBgmConfig.RoundBased(
            "CosmicFragmentBGM",
            GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(GalaxyChildWeak)] = EncounterBgmConfig.RoundBased(
            "GalaxyChildBGM",
            GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(SpinyBusWeak)] = EncounterBgmConfig.RoundBased(
            "SpinyBusBGM",
            GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BigBadWolfWeak)] = EncounterBgmConfig.RoundBased(
            "BigBadWolfBGM",
            GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(SmilingBodiesStrong)] = EncounterBgmConfig.RoundBased(
            "SmilingBodiesBGM",
            GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(DespairKnightStrong)] = EncounterBgmConfig.RoundBased(
            "DespairKnightBGM",
            GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(WrathServantStrong)] = EncounterBgmConfig.RoundBased(
            "WrathServantBGM",
            GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(ScarecrowSearchingForWisdomWeak)] = EncounterBgmConfig.RoundBased(
            "ScarecrowSearchingForWisdomBGM",
            GuestReceptionPoolRegistry.SocialSciencesReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(HeartOfAspirationWeak)] = EncounterBgmConfig.RoundBased(
            "HeartOfAspirationBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BurrowingHeavenWeak)] = EncounterBgmConfig.RoundBased(
            "BurrowingHeavenBGM",
            GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(WarmheartedWoodsmanStrong)] = EncounterBgmConfig.RoundBased(
            "WarmheartedWoodsmanBGM",
            GuestReceptionPoolRegistry.SocialSciencesReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(PriceOfSilenceStrong)] = EncounterBgmConfig.RoundBased(
            "PriceOfSilenceBGM",
            GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BigBirdStrong)] = EncounterBgmConfig.RoundBased(
            "BigBirdBGM",
            GuestReceptionPoolRegistry.PhilosophyReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BlueStarStrong)] = EncounterBgmConfig.RoundBased(
            "BlueStarBGM",
            GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(PunishingBirdStrong)] = EncounterBgmConfig.RoundBased(
            "PunishingBirdBGM",
            GuestReceptionPoolRegistry.PhilosophyReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(JudgementBirdElite)] = EncounterBgmConfig.RoundBased(
            "JudgementBirdBGM",
            GuestReceptionPoolRegistry.PhilosophyReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
    };

    private static readonly Dictionary<string, EncounterBgmConfig> GuestLayerConfigByScenePath =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [NormalizeResPath(GuestReceptionPoolRegistry.GeneralReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "GeneralReceptionFloorBGM",
                GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
                volumeScale: 0.90f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.ReligionReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "ReligionReceptionFloorBGM",
                GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.LiteratureReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "LiteratureReceptionFloorBGM",
                GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.NaturalReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "NaturalReceptionFloorBGM",
                GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.LanguageReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "LanguageReceptionFloorBGM",
                GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.YesodReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "YesodReceptionFloorBGM",
                GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds)
        };

    /// <summary>精确按遭遇的运行时类型查表：子类不继承父类的配置。</summary>
    internal static bool HasEncounterConfig(EncounterModel? encounter)
    {
        return encounter != null && ConfigByEncounterType.ContainsKey(encounter.GetType());
    }

    internal static bool TryGetEncounterConfig(
        EncounterModel? encounter,
        [NotNullWhen(true)] out EncounterBgmConfig? config)
    {
        config = null;
        return encounter != null && ConfigByEncounterType.TryGetValue(encounter.GetType(), out config);
    }

    internal static EncounterBgmConfig ResolveConfigForEncounter(
        CombatStateLike CombatState,
        EncounterBgmConfig defaultConfig,
        out string? matchedLayerPath)
    {
        matchedLayerPath = null;
        if (CombatState.Encounter == null)
        {
            return defaultConfig;
        }

        Type encounterType = CombatState.Encounter.GetType();

        if (!GuestReceptionPoolRegistry.IsGuestEncounterType(encounterType))
        {
            return defaultConfig;
        }

        if (TryResolveGuestLayerConfig(CombatState, out EncounterBgmConfig? layerConfig, out string? matchedGuestLayerPath))
        {
            matchedLayerPath = matchedGuestLayerPath;
            return layerConfig!;
        }

        return defaultConfig;
    }

    private static bool TryResolveGuestLayerConfig(
        CombatStateLike CombatState,
        out EncounterBgmConfig? config,
        out string? matchedLayerPath)
    {
        config = null;
        matchedLayerPath = null;

        HashSet<string> selectedLayerPaths = ResolveSelectedLayerPaths(CombatState);
        if (selectedLayerPaths.Count == 0)
        {
            return false;
        }

        foreach (string selectedLayerPath in selectedLayerPaths)
        {
            if (!GuestLayerConfigByScenePath.TryGetValue(selectedLayerPath, out EncounterBgmConfig? layerConfig))
            {
                continue;
            }

            config = layerConfig;
            matchedLayerPath = selectedLayerPath;
            return true;
        }

        return false;
    }

    private static HashSet<string> ResolveSelectedLayerPaths(CombatStateLike CombatState)
    {
        var selectedLayerPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (CombatState.Encounter == null)
        {
            return selectedLayerPaths;
        }

        try
        {
            IEnumerable<string> assetPaths = CombatState.Encounter.GetAssetPaths(CombatState.RunState);
            foreach (string assetPath in assetPaths)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                string normalizedPath = NormalizeResPath(assetPath);
                if (!normalizedPath.Contains("/layers/", StringComparison.OrdinalIgnoreCase) ||
                    !normalizedPath.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                selectedLayerPaths.Add(normalizedPath);
            }
        }
        catch (Exception ex)
        {
            string encounterName = CombatState.Encounter.Id.Entry;
            Log.Warn(
                "[EncounterBGM] Failed to resolve selected background layers for encounter " +
                encounterName + ": " + ex.Message);
        }

        return selectedLayerPaths;
    }

    internal static string NormalizeResPath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }
}

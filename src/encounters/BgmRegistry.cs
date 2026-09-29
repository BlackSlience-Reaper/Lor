using System;
using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.encounters;

/// <summary>
/// 遭遇 BGM 配置的查找：遭遇自己通过 <see cref="IEncounterBgmSource"/> 声明；
/// 接待遭遇（<see cref="LibraryOfRuina.guests.IGuestReceptionEncounter"/>）抽到登记过的接待层背景时，改用该层的配置。
/// </summary>
internal static class BgmRegistry
{
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

    internal static bool HasEncounterConfig(EncounterModel? encounter)
    {
        return encounter is IEncounterBgmSource;
    }

    internal static bool TryGetEncounterConfig(
        EncounterModel? encounter,
        [NotNullWhen(true)] out EncounterBgmConfig? config)
    {
        config = (encounter as IEncounterBgmSource)?.Bgm;
        return config != null;
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

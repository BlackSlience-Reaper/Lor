using System;
using System.Linq;
using System.Text;
using HarmonyLib;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(ModManager), nameof(ModManager.GetGameplayRelevantModNameList))]
public static class MultiplayerConfigFingerprintPatch
{
    private const string ThisModIdPrefix = "LibraryOfRuina-";
    private static readonly string[] FingerprintSegmentPrefixes =
    [
        "+设置.",
        "+compat.",
        "+dll.",
        "+schema.",
        "+cfg."
    ];

    private static bool _fingerprintInjectionLogged;

    [HarmonyPostfix]
    public static void Postfix(ref List<string>? __result)
    {
        if (__result == null || __result.Count == 0)
        {
            return;
        }

        string compatibilityFingerprintSuffix = LibraryOfRuinaCompatibilityFingerprint.GetGameplayRelevantSuffix();
        bool updated = false;

        for (int i = 0; i < __result.Count; i++)
        {
            string? entry = __result[i];
            if (string.IsNullOrWhiteSpace(entry)
                || !entry.StartsWith(ThisModIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string baseEntry = StripFingerprintSegments(entry);
            string nextEntry = baseEntry + compatibilityFingerprintSuffix;
            if (!string.Equals(nextEntry, entry, StringComparison.Ordinal))
            {
                __result[i] = nextEntry;
                updated = true;
            }
        }

        if (updated && !_fingerprintInjectionLogged)
        {
            _fingerprintInjectionLogged = true;
            Log.Info("[LibraryOfRuina.Multiplayer] Added multiplayer settings to gameplay-relevant mod list: "
                + compatibilityFingerprintSuffix
                + ".");
        }
    }

    private static string StripFingerprintSegments(string entry)
    {
        int firstFingerprintIndex = -1;
        foreach (string prefix in FingerprintSegmentPrefixes)
        {
            int index = entry.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            firstFingerprintIndex = firstFingerprintIndex < 0 ? index : Math.Min(firstFingerprintIndex, index);
        }

        return firstFingerprintIndex >= 0 ? entry[..firstFingerprintIndex] : entry;
    }
}

[HarmonyPatch(typeof(NetErrorInfo), nameof(NetErrorInfo.GetErrorString))]
public static class MultiplayerConfigMismatchErrorPatch
{
    private const string ThisModIdPrefix = "LibraryOfRuina-";
    private const string SettingsSegmentPrefix = "+设置.";
    private static readonly string[] FingerprintSegmentPrefixes =
    [
        SettingsSegmentPrefix,
        "+compat.",
        "+dll.",
        "+schema.",
        "+cfg."
    ];

    public static void Postfix(NetErrorInfo __instance, ref string __result)
    {
        if (__instance.GetReason() != NetError.ModMismatch
            || __instance.ConnectionExtraInfo is not { } extraInfo)
        {
            return;
        }

        List<string> missingOnLocal = extraInfo.GetMissingModsOnLocal(nonGameplay: false);
        List<string> missingOnRemote = extraInfo.GetMissingModsOnRemote(nonGameplay: false);
        List<string> hostOnly = extraInfo.localIsHost
            ? missingOnRemote
            : missingOnLocal;
        List<string> joiningOnly = extraInfo.localIsHost
            ? missingOnLocal
            : missingOnRemote;

        string? hostEntry = FindThisModEntry(hostOnly);
        string? joiningEntry = FindThisModEntry(joiningOnly);
        if (hostEntry == null && joiningEntry == null)
        {
            return;
        }

        __result = BuildFriendlyMismatchText(
            hostEntry,
            joiningEntry,
            hostOnly,
            joiningOnly);
    }

    private static string BuildFriendlyMismatchText(
        string? hostEntry,
        string? joiningEntry,
        List<string> hostOnly,
        List<string> joiningOnly)
    {
        StringBuilder builder = new();
        builder.AppendLine("LibraryOfRuina 联机不匹配：");

        if (hostEntry == null)
        {
            builder.AppendLine("- 主机没有启用 LibraryOfRuina，或主机的模组列表没有携带本模组。");
        }
        else if (joiningEntry == null)
        {
            builder.AppendLine("- 加入方没有启用 LibraryOfRuina，或加入方的模组列表没有携带本模组。");
        }
        else
        {
            AppendEntryDifferences(builder, hostEntry, joiningEntry);
        }

        AppendOtherModDifferences(
            builder,
            "房主有，但是加入方没有的其他模组",
            hostOnly);
        AppendOtherModDifferences(
            builder,
            "加入方有，但是房主没有的其他模组",
            joiningOnly);
        return builder.ToString().TrimEnd();
    }

    private static void AppendEntryDifferences(StringBuilder builder, string hostEntry, string joiningEntry)
    {
        bool foundDifference = false;

        string hostBase = StripFingerprintSegments(hostEntry);
        string joiningBase = StripFingerprintSegments(joiningEntry);
        if (!string.Equals(hostBase, joiningBase, StringComparison.Ordinal))
        {
            builder.AppendLine("- 模组版本不一致：主机 " + hostBase + "，加入方 " + joiningBase + "。");
            foundDifference = true;
        }

        foundDifference |= AppendSettingsDifference(builder, hostEntry, joiningEntry);

        if (!foundDifference)
        {
            builder.AppendLine("- LibraryOfRuina 联机设置不一致。");
        }
    }

    private static bool AppendSettingsDifference(StringBuilder builder, string hostEntry, string joiningEntry)
    {
        string? hostSettings = ExtractSegment(hostEntry, SettingsSegmentPrefix);
        string? joiningSettings = ExtractSegment(joiningEntry, SettingsSegmentPrefix);
        if (string.Equals(hostSettings, joiningSettings, StringComparison.Ordinal))
        {
            return false;
        }

        builder.AppendLine("- 联机设置不一致：主机 "
            + FormatSettings(hostSettings)
            + "；加入方 "
            + FormatSettings(joiningSettings)
            + "。");
        return true;
    }

    private static void AppendOtherModDifferences(StringBuilder builder, string label, List<string>? entries)
    {
        List<string> otherEntries = entries?
            .Where(entry => !IsThisModEntry(entry))
            .Select(StripFingerprintSegments)
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct()
            .ToList() ?? [];

        if (otherEntries.Count > 0)
        {
            builder.AppendLine("- " + label + "：" + string.Join(", ", otherEntries));
        }
    }

    private static string? FindThisModEntry(List<string>? entries) =>
        entries?.FirstOrDefault(IsThisModEntry);

    private static bool IsThisModEntry(string? entry) =>
        !string.IsNullOrWhiteSpace(entry)
        && entry.StartsWith(ThisModIdPrefix, StringComparison.OrdinalIgnoreCase);

    private static string? ExtractSegment(string entry, string prefix)
    {
        int start = entry.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += prefix.Length;
        int end = entry.IndexOf('+', start);
        return end < 0 ? entry[start..] : entry[start..end];
    }

    private static string StripFingerprintSegments(string entry)
    {
        int firstFingerprintIndex = -1;
        foreach (string prefix in FingerprintSegmentPrefixes)
        {
            int index = entry.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            firstFingerprintIndex = firstFingerprintIndex < 0 ? index : Math.Min(firstFingerprintIndex, index);
        }

        return firstFingerprintIndex >= 0 ? entry[..firstFingerprintIndex] : entry;
    }

    private static string FormatSettings(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "未提供"
            : value.Replace('.', '、');
}

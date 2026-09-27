using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.localization;

/// <summary>
/// 为本模组正文里以颜色标签包住的 Power 正式名称追加对应图标。
/// 名称覆盖本模组、基础库与原版的 Buff/Debuff。
/// zhs 为参考语言：eng/jpn/kor 只在同一条目的 zhs 正文也解析到同一 Power 时装饰，避免各语言与原版或招式同名造成误配。
/// </summary>
[HarmonyPatch(typeof(LocTable), nameof(LocTable.GetRawText))]
internal static class PowerIconLocalizationPatch
{
    private const string ReferenceLanguage = "zhs";

    private static readonly string[] SupportedLanguages = ["zhs", "eng", "jpn", "kor"];

    // 紫色按本地化规范用于附魔名称，不视为 Power 名称。
    private static readonly Regex Keyword = new(
        @"\[(?<color>gold|red|green|blue)\](?<name>[^\[\]{}]+)\[/\k<color>\](?!\[img)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // 每种语言的正式名称 -> 候选 Power；同名候选由 Resolve 按条目归属区分。
    private static readonly Dictionary<string, Dictionary<string, PowerNameMap.Entry[]>> NamesByLanguage = new(StringComparer.Ordinal);

    private static PowerNameMap.Entry[]? vanillaEntries;

    // 读取 Type 抛错的 Power 类型与装饰失败的条目各只记录一次，避免刷屏。
    private static readonly HashSet<Type> ReportedTypeFailures = [];

    private static readonly HashSet<(string Table, string Key)> ReportedDecorateFailures = [];

    private static Dictionary<string, PowerNameMap.Entry[]> GetNames(string language)
    {
        if (NamesByLanguage.TryGetValue(language, out var names))
        {
            return names;
        }

        // 首次处理本模组正文时才读取原版模型，避免 Harmony 初始化期间提前访问 ModelDb。
        vanillaEntries ??= LoadVanillaEntries();
        names = PowerNameMap.Entries
            .Concat(vanillaEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name(language)))
            .GroupBy(entry => entry.Name(language)!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        NamesByLanguage.Add(language, names);
        return names;
    }

    private static PowerNameMap.Entry[] LoadVanillaEntries()
    {
        // 直接读取四语原文，避免标题查询再次进入本补丁，也不把其他模组纳入映射。
        var titles = SupportedLanguages.ToDictionary(
            language => language,
            language => JsonSerializer.Deserialize<Dictionary<string, string>>(
                FileAccess.GetFileAsString($"res://localization/{language}/powers.json"))!);
        var entries = new List<PowerNameMap.Entry>();
        foreach (PowerModel power in ModelDb.AllPowers)
        {
            if (power.GetType().Assembly != typeof(PowerModel).Assembly
                || !TryGetPowerType(power, out PowerType type)
                || type is not (PowerType.Buff or PowerType.Debuff))
            {
                continue;
            }

            string title = power.Id.Entry + ".title";
            entries.Add(new PowerNameMap.Entry(title, () => power,
                titles["zhs"].GetValueOrDefault(title, string.Empty),
                titles["eng"].GetValueOrDefault(title, string.Empty),
                titles["jpn"].GetValueOrDefault(title, string.Empty),
                titles["kor"].GetValueOrDefault(title, string.Empty)));
        }

        return entries.ToArray();
    }

    // 这里只在规范模型上读取 Type。原版 Type 不依赖 Owner，但其他补丁可能让它访问运行时状态并抛出
    // CanonicalModelException；这类 Power 只是不加图标，不能让 LocTable.GetRawText 抛错拖垮界面。
    private static bool TryGetPowerType(PowerModel power, out PowerType type)
    {
        try
        {
            type = power.Type;
            return true;
        }
        catch (Exception exception)
        {
            type = PowerType.None;
            if (ReportedTypeFailures.Add(power.GetType()))
            {
                Log.Warn("[LibraryOfRuina.PowerIcon] Skipped name icon for "
                    + power.Id
                    + ": reading Type on the canonical model threw "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
            }

            return false;
        }
    }

    private static readonly Dictionary<(string Language, string Table), Dictionary<string, string>> SourceTables = new();

    private static readonly Dictionary<(string Language, string Table, string Key, string Text), string> Cache = new();

    // 参考语言各条目解析出的 Power，供其他语言核对；键为 (表名, 键名)。
    private static readonly Dictionary<(string Table, string Key), HashSet<string>> ReferenceResolutions = new();

    // 下列正文实际施加原版 WeakPower，不能套用基础库同名“虚弱”的图标。
    private static readonly HashSet<string> VanillaWeakEntries = new(StringComparer.Ordinal)
    {
        "LETICIA_PAGE_MISCHIEF_CHOICE_CARD",
        "LETICIA_PAGE_RELIC",
        "MAGIC_BULLET_SILENCE_EGO_CARD",
        "MAGIC_BULLET_SOUL_STEAL_EGO_CARD",
        "DEAD_BUTTERFLY_ANGRY_RELEASE",
        "SMILING_BODIES_SCREAM",
        "ART_FLOOR_FINAL_DA_CAPO_BOSS",
        "NOSFERATU_OMINOUS_AURA",
        "PHILOSOPHY_FLOOR_TWILIGHT_PROTECT_BLACK_FOREST",
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_SMILING_FACE",
        "SCORCHED_GIRL_ASHES_POWER"
    };

    // “混乱”同时是混乱状态的名称；只有下列条目里“N层混乱”指本模组的混乱 Power。
    private static readonly HashSet<string> ConfusionPowerEntries = new(StringComparer.Ordinal)
    {
        "LIBRARY_OF_RUINA_CONFUSION_POWER",
        "FORGOTTEN_LONGING_EMBRACE_EGO_CARD",
        "PUNISHMENT_STRIKE_EGO_CARD",
        "REGRET_EGO_CARD",
        "BLUE_STAR_NOVA_VOICE",
        "BLUE_STAR_NOVA_VOICE_POWER",
        "KING_OF_GREED_GOLDEN_PATH",
        "LANGUAGE_FLOOR_DIPSIA_UNBEARABLE_THIRST",
        "LANGUAGE_FLOOR_MIMICRY_IMITATE",
        "LITERATURE_FLOOR_BLACK_SWAN_SWAN_SONG",
        "LITERATURE_FLOOR_TODAYS_EXPRESSION_WAVERING_FEELINGS",
        "LITTLE_RED_HOLLOW_POINT_SHELL",
        "LITTLE_RED_STRIKE_WITHOUT_HESITATION",
        "NATURAL_FLOOR_GOLD_RUSH_GOLDEN_PATH",
        "PHILOSOPHY_FLOOR_TWILIGHT_FOREST_LIGHT",
        "ROAD_HOME_FRIEND_HOME",
        "SMILING_BODIES_SIT",
        "SMILING_BODIES_SIT_PLAYER"
    };

    private static void Postfix(string key, string ____name, ref string __result)
    {
        string language = LocManager.Instance.Language;
        if (Array.IndexOf(SupportedLanguages, language) < 0 || key.EndsWith(".title", StringComparison.Ordinal))
        {
            return;
        }

        // 同时核对键与原文，避免处理原版、其他模组或被其他补丁替换的文字。
        if (!LoadSourceTable(language, ____name).TryGetValue(key, out string? original) || original != __result)
        {
            return;
        }

        var cacheKey = (language, ____name, key, __result);
        if (!Cache.TryGetValue(cacheKey, out string? decorated))
        {
            decorated = DecorateText(language, ____name, key, __result);
            Cache.Add(cacheKey, decorated);
        }

        __result = decorated;
    }

    // 本补丁处于 LocTable.GetRawText 热路径：装饰失败时保留原文并只记录一次，不能把异常抛回界面代码。
    private static string DecorateText(string language, string table, string key, string text)
    {
        try
        {
            // 不含 zhs 的发布版本没有参考正文，此时按当前语言直接解析。
            HashSet<string>? reference = language == ReferenceLanguage || !ReferenceTableExists(table)
                ? null
                : GetReferenceResolutions(table, key);
            return Keyword.Replace(text, match => Decorate(language, key, match, reference));
        }
        catch (Exception exception)
        {
            if (ReportedDecorateFailures.Add((table, key)))
            {
                Log.Warn("[LibraryOfRuina.PowerIcon] Kept undecorated text for "
                    + table
                    + "."
                    + key
                    + " ("
                    + language
                    + "): "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message);
            }

            return text;
        }
    }

    private static readonly Dictionary<string, bool> ReferenceTablePresence = new(StringComparer.Ordinal);

    private static bool ReferenceTableExists(string table)
    {
        if (!ReferenceTablePresence.TryGetValue(table, out bool exists))
        {
            exists = FileAccess.FileExists(GetSourceTablePath(ReferenceLanguage, table));
            ReferenceTablePresence.Add(table, exists);
        }

        return exists;
    }

    private static string GetSourceTablePath(string language, string table)
    {
        return $"res://LibraryOfRuina/localization/{language}/{table}.json";
    }

    private static Dictionary<string, string> LoadSourceTable(string language, string table)
    {
        if (SourceTables.TryGetValue((language, table), out Dictionary<string, string>? source))
        {
            return source;
        }

        string path = GetSourceTablePath(language, table);
        source = FileAccess.FileExists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(FileAccess.GetFileAsString(path))!
            : new Dictionary<string, string>();
        SourceTables.Add((language, table), source);
        return source;
    }

    private static HashSet<string> GetReferenceResolutions(string table, string key)
    {
        if (ReferenceResolutions.TryGetValue((table, key), out HashSet<string>? resolved))
        {
            return resolved;
        }

        resolved = new HashSet<string>(StringComparer.Ordinal);
        if (LoadSourceTable(ReferenceLanguage, table).TryGetValue(key, out string? text))
        {
            foreach (Match match in Keyword.Matches(text))
            {
                PowerNameMap.Entry? entry = ResolveName(ReferenceLanguage, key, match.Groups["name"].Value);
                if (entry != null)
                {
                    resolved.Add(entry.TitleKey);
                }
            }
        }

        ReferenceResolutions.Add((table, key), resolved);
        return resolved;
    }

    private static string Decorate(string language, string key, Match match, HashSet<string>? reference)
    {
        PowerNameMap.Entry? entry = ResolveName(language, key, match.Groups["name"].Value, reference);
        if (entry == null || (reference != null && !reference.Contains(entry.TitleKey)))
        {
            return match.Value;
        }

        var power = entry.GetPower();
        if (!TryGetPowerType(power, out PowerType type)
            || type is not (PowerType.Buff or PowerType.Debuff)
            || !PowerIconResolver.TryResolve(power, out ResolvedPowerIcon icon))
        {
            return match.Value;
        }

        return match.Value + PowerInlineIconSizing.CreateMarkup(icon);
    }

    private static PowerNameMap.Entry? ResolveName(string language, string key, string name, HashSet<string>? reference = null)
    {
        if (!GetNames(language).TryGetValue(name, out PowerNameMap.Entry[]? candidates))
        {
            return null;
        }

        if (reference != null)
        {
            candidates = candidates.Where(entry => reference.Contains(entry.TitleKey)).ToArray();
        }

        string ownerKey = key.Split('.')[0];
        string ownerTitle = ownerKey + ".title";
        var own = candidates.FirstOrDefault(entry => entry.TitleKey == ownerTitle);
        if (own != null)
        {
            return own;
        }

        string? title = Resolve(key, ownerKey, candidates.Select(entry => entry.TitleKey).ToArray());
        return title == null ? null : candidates.FirstOrDefault(entry => entry.TitleKey == title);
    }

    // 按条目归属在同名候选中选择；规则以本地化键表达，与显示语言无关。
    private static string? Resolve(string key, string ownerKey, string[] candidates)
    {
        bool Has(string title) => Array.IndexOf(candidates, title) >= 0;

        if (VanillaWeakEntries.Contains(ownerKey) && (Has("WEAK_POWER.title") || Has("LIBRARY_WEAK_POWER.title")))
        {
            return Has("WEAK_POWER.title") ? "WEAK_POWER.title" : null;
        }

        if (Has("LIBRARY_WEAK_POWER.title"))
        {
            return "LIBRARY_WEAK_POWER.title";
        }

        if (Has("LIBRARY_OF_RUINA_CONFUSION_POWER.title"))
        {
            return ConfusionPowerEntries.Contains(ownerKey) ? "LIBRARY_OF_RUINA_CONFUSION_POWER.title" : null;
        }

        if (Has("ART_FLOOR_IMBALANCED_POWER.title"))
        {
            return "ART_FLOOR_IMBALANCED_POWER.title";
        }

        // 同名机制按实际使用范围选择；附魔“束缚”、原版“再生”、其他恐惧/朋友类 Power 及书页模式名保持各自语义。
        if (Has("LIBRARY_BINDING_POWER.title") || Has("QUEEN_BIND_POWER.title"))
        {
            if (key.StartsWith("ART_FLOOR_DA_CAPO_SOUL_BINDING", StringComparison.Ordinal))
            {
                return null;
            }

            return key.StartsWith("QUEEN_", StringComparison.Ordinal)
                ? "QUEEN_BIND_POWER.title"
                : "LIBRARY_BINDING_POWER.title";
        }

        if (Has("LIBRARY_QUICKNESS_POWER.title") || Has("ALL_AROUND_HELPER_SWIFT_POWER.title")
            || Has("ART_FLOOR_QUICKNESS_POWER.title"))
        {
            if (key.StartsWith("ALL_AROUND_HELPER_", StringComparison.Ordinal))
            {
                return "ALL_AROUND_HELPER_SWIFT_POWER.title";
            }

            return key.StartsWith("ART_FLOOR_", StringComparison.Ordinal)
                ? "ART_FLOOR_QUICKNESS_POWER.title"
                : "LIBRARY_QUICKNESS_POWER.title";
        }

        if (Has("HISTORY_FLOOR_WASP_PARALYSIS_POWER.title") || Has("LIBRARY_OF_RUINA_PARALYSIS_POWER.title"))
        {
            return key.StartsWith("HISTORY_FLOOR_WASP_", StringComparison.Ordinal)
                ? "HISTORY_FLOOR_WASP_PARALYSIS_POWER.title"
                : "LIBRARY_OF_RUINA_PARALYSIS_POWER.title";
        }

        if (Has("WRATH_SERVANT_CORROSION_POWER.title") || Has("RNFMABJ_CORROSION_POWER.title"))
        {
            return key.StartsWith("RNFMABJ_", StringComparison.Ordinal)
                ? "RNFMABJ_CORROSION_POWER.title"
                : "WRATH_SERVANT_CORROSION_POWER.title";
        }

        if (Has("BIG_BAD_WOLF_TEMPORARY_THORNS_POWER.title") || Has("WARMHEARTED_WOODSMAN_TEMPORARY_THORNS_POWER.title"))
        {
            if (key.StartsWith("BIG_BAD_WOLF_", StringComparison.Ordinal))
            {
                return "BIG_BAD_WOLF_TEMPORARY_THORNS_POWER.title";
            }

            return key.StartsWith("WARMHEARTED_WOODSMAN_", StringComparison.Ordinal)
                ? "WARMHEARTED_WOODSMAN_TEMPORARY_THORNS_POWER.title"
                : null;
        }

        if (Has("JUDGEMENT_BIRD_SIN_POWER.title") || Has("PHILOSOPHY_FLOOR_TWILIGHT_SIN_POWER.title"))
        {
            if (key.StartsWith("PHILOSOPHY_FLOOR_TWILIGHT_", StringComparison.Ordinal))
            {
                return "PHILOSOPHY_FLOOR_TWILIGHT_SIN_POWER.title";
            }

            return key.StartsWith("JUDGEMENT_BIRD_", StringComparison.Ordinal)
                || key.StartsWith("ESCAPED_BIRD_", StringComparison.Ordinal)
                ? "JUDGEMENT_BIRD_SIN_POWER.title"
                : null;
        }

        if (Has("NEXT_TURN_STRENGTH_POWER.title") || Has("QUEEN_BEE_NEXT_TURN_STRONG_POWER.title"))
        {
            return key.StartsWith("QUEEN_BEE_", StringComparison.Ordinal)
                ? "QUEEN_BEE_NEXT_TURN_STRONG_POWER.title"
                : "NEXT_TURN_STRENGTH_POWER.title";
        }

        if (Has("LANGUAGE_FLOOR_MIMICRY_FORM_TWO_REGENERATION_POWER.title"))
        {
            if (key.StartsWith("LANGUAGE_FLOOR_MIMICRY_", StringComparison.Ordinal))
            {
                return "LANGUAGE_FLOOR_MIMICRY_FORM_TWO_REGENERATION_POWER.title";
            }

            return Has("REGEN_POWER.title") ? "REGEN_POWER.title" : null;
        }

        if (Has("PHILOSOPHY_FLOOR_TWILIGHT_FEAR_POWER.title"))
        {
            return key.StartsWith("PHILOSOPHY_FLOOR_TWILIGHT_", StringComparison.Ordinal)
                ? "PHILOSOPHY_FLOOR_TWILIGHT_FEAR_POWER.title"
                : null;
        }

        if (Has("WRATH_SERVANT_FRIEND_POWER.title"))
        {
            return key.StartsWith("WRATH_SERVANT_", StringComparison.Ordinal)
                ? "WRATH_SERVANT_FRIEND_POWER.title"
                : null;
        }

        if (Has("NATURAL_FLOOR_NIHIL_HATRED_STATUS.title"))
        {
            return key.StartsWith("NATURAL_", StringComparison.Ordinal) || key.StartsWith("NIHIL_", StringComparison.Ordinal)
                ? "NATURAL_FLOOR_NIHIL_HATRED_STATUS.title"
                : null;
        }

        if (Has("SOCIAL_FLOOR_COURAGE_POWER.title"))
        {
            return key.StartsWith("SOCIAL_FLOOR_", StringComparison.Ordinal)
                || key.StartsWith("FALSE_THRONE_", StringComparison.Ordinal)
                ? "SOCIAL_FLOOR_COURAGE_POWER.title"
                : null;
        }

        return candidates.Length == 1 ? candidates[0] : null;
    }
}

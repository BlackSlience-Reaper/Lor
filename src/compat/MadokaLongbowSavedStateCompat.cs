using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Utils;

namespace LibraryOfRuina.compat;

internal static class MadokaLongbowSavedStateCompat
{
    private const string LongbowTypeName = "MadokaMod.MadokaEndOfLoopsLongbow";

    // 轮回终结长弓：跨战斗保留的累计释放次数，决定每第三次释放的额外重放。
    private static readonly SavedAttachedState<RelicModel, string?> ReleasedCards =
        new("LibraryOfRuina_MadokaLongbowReleasedCards", static () => null);

    private static FieldInfo? _releasedCardsField;

    internal static void Initialize()
    {
        // 在 RitsuLib 确定存档字段网络编号前注册；两端加载模组的先后顺序不影响字段表。
        _ = ReleasedCards;
    }

    internal static void Capture(object model)
    {
        if (model is RelicModel relic && ResolveCounterField(relic) is { } field)
        {
            int count = (int)field.GetValue(relic)!;
            ReleasedCards[relic] = count.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal static void Restore(RelicModel relic)
    {
        if (ResolveCounterField(relic) is not { } field
            || !ReleasedCards.TryGetValue(relic, out string? saved)
            || !int.TryParse(saved, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
        {
            return;
        }

        field.SetValue(relic, Math.Max(0, count));
    }

    private static FieldInfo? ResolveCounterField(RelicModel relic)
    {
        Type type = relic.GetType();
        if (type.FullName != LongbowTypeName)
        {
            return null;
        }

        // 仅桥接已确认缺失保存的外部私有字段，不扫描或改写其他模组状态。
        _releasedCardsField ??= AccessTools.DeclaredField(type, "_releasedCards");
        return _releasedCardsField?.FieldType == typeof(int) ? _releasedCardsField : null;
    }
}

[HarmonyPatch(typeof(SavedProperties), nameof(SavedProperties.FromInternal))]
internal static class MadokaLongbowSavePatch
{
    private static void Prefix(object model)
    {
        MadokaLongbowSavedStateCompat.Capture(model);
    }
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.FromSerializable))]
internal static class MadokaLongbowRestorePatch
{
    private static void Postfix(RelicModel __result)
    {
        MadokaLongbowSavedStateCompat.Restore(__result);
    }
}

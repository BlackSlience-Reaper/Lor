using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.patches;

/// <summary>
/// Resolves custom run-history icons at the shared ImageHelper seam.  BaseLib
/// and RitsuLib expose equivalent properties through different interfaces, so
/// this adapter keeps both contracts working without depending on either
/// implementation's concrete base class.
/// <para>
/// 这里处理的是其他模组的内容（本模组的图标走原版约定路径，不需要它），所以只作兜底：排在 BaseLib 与 RitsuLib
/// 自己的前缀之后，它们已经给出图标（跳过原方法）时本前缀不再执行，外部覆盖与点位过滤按它们的规则生效。
/// </para>
/// </summary>
internal static class RoomIconPathCustomOverride
{
    private static readonly ConcurrentDictionary<Type, IconProperties> PropertiesByType = new();

    public static bool TryResolve(
        ModelId? modelId,
        bool outline,
        ref string? result)
    {
        if (modelId == null
            || ModelDb.GetByIdOrNull<AbstractModel>(modelId) is not { } model)
        {
            return false;
        }

        IconProperties? properties;
        if (!PropertiesByType.TryGetValue(model.GetType(), out properties))
        {
            properties = IconProperties.Create(model.GetType());
            if (properties == null)
            {
                return false;
            }

            PropertiesByType.TryAdd(model.GetType(), properties);
        }

        string? path;
        try
        {
            path = (outline
                ? properties.Outline
                : properties.Main)?.GetValue(model) as string;
        }
        catch (Exception)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
        {
            return false;
        }

        result = path;
        return true;
    }

    private sealed record IconProperties(
        PropertyInfo? Main,
        PropertyInfo? Outline)
    {
        public static IconProperties? Create(Type type)
        {
            PropertyInfo? main;
            PropertyInfo? outline;
            try
            {
                main = type.GetProperty(
                    "CustomRunHistoryIconPath",
                    BindingFlags.Instance | BindingFlags.Public);
                outline = type.GetProperty(
                    "CustomRunHistoryIconOutlinePath",
                    BindingFlags.Instance | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // 派生类用 new 隐藏了同名属性；不猜用哪个，交还原版。
                return null;
            }
            if (main == null && outline == null)
            {
                return null;
            }

            return new IconProperties(main, outline);
        }
    }
}

[HarmonyPatch(typeof(ImageHelper), nameof(ImageHelper.GetRoomIconPath))]
[HarmonyPriority(Priority.Last)]
[HarmonyAfter("BaseLib", "com.ritsukage.sts2-RitsuLib.framework-content-assets")]
[LibraryPatch(Reason = "兼容其他模组（BaseLib/RitsuLib 内容）的对局历史图标属性，本模组图标走原版约定路径；作为兜底排在两者之后，它们已给出结果时本前缀不执行。")]
internal static class RoomIconPathCustomOverridePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ModelId? modelId, ref string? __result) =>
        !RoomIconPathCustomOverride.TryResolve(
            modelId,
            outline: false,
            ref __result);
}

[HarmonyPatch(typeof(ImageHelper), nameof(ImageHelper.GetRoomIconOutlinePath))]
[HarmonyPriority(Priority.Last)]
[HarmonyAfter("BaseLib", "com.ritsukage.sts2-RitsuLib.framework-content-assets")]
[LibraryPatch(Reason = "兼容其他模组（BaseLib/RitsuLib 内容）的对局历史图标描边属性，本模组图标走原版约定路径；作为兜底排在两者之后，它们已给出结果时本前缀不执行。")]
internal static class RoomIconOutlinePathCustomOverridePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ModelId? modelId, ref string? __result) =>
        !RoomIconPathCustomOverride.TryResolve(
            modelId,
            outline: true,
            ref __result);
}

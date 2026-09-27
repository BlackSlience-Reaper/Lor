using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.patches;

/// <summary>
/// Resolves custom run-history icons at the shared ImageHelper seam.  BaseLib
/// and RitsuLib expose equivalent properties through different interfaces, so
/// this adapter keeps both contracts working without depending on either
/// implementation's concrete base class.
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
            PropertyInfo? main = type.GetProperty(
                "CustomRunHistoryIconPath",
                BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo? outline = type.GetProperty(
                "CustomRunHistoryIconOutlinePath",
                BindingFlags.Instance | BindingFlags.Public);
            if (main == null && outline == null)
            {
                return null;
            }

            return new IconProperties(main, outline);
        }
    }
}

[HarmonyPatch(typeof(ImageHelper), nameof(ImageHelper.GetRoomIconPath))]
[HarmonyPriority(Priority.First)]
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
[HarmonyPriority(Priority.First)]
internal static class RoomIconOutlinePathCustomOverridePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ModelId? modelId, ref string? __result) =>
        !RoomIconPathCustomOverride.TryResolve(
            modelId,
            outline: true,
            ref __result);
}

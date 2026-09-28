using Godot;
using HarmonyLib;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

internal static class PowerIconFallbackScope
{
    // Vanilla and third-party powers keep their own icons: vanilla ships powers/<id>.png as the big
    // art, so resolving it for Icon would swap every vanilla small icon for the large image.
    internal static bool IsLibraryPower(PowerModel power) =>
        ModOwnership.IsOwn(power) || power is LibraryPowerModel;
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.Icon), MethodType.Getter)]
[HarmonyAfter("LibraryOfRuinaLib")]
internal static class PowerIconPngFallbackPatch
{
    private static bool Prefix(
        PowerModel __instance,
        ref Texture2D? __result,
        out Texture2D? __state)
    {
        __state = null;
        if (!PowerIconFallbackScope.IsLibraryPower(__instance)
            || !PowerIconResolver.TryResolve(__instance, out ResolvedPowerIcon resolved))
        {
            return true;
        }

        __state = resolved.Texture;
        __result = resolved.Texture;
        return false;
    }

    private static void Postfix(Texture2D? __state, ref Texture2D? __result)
    {
        if (GodotTextureSafety.IsValid(__state))
        {
            __result = __state;
        }
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.BigIcon), MethodType.Getter)]
[HarmonyAfter("LibraryOfRuinaLib")]
internal static class PowerBigIconPngFallbackPatch
{
    private static bool Prefix(
        PowerModel __instance,
        ref Texture2D? __result,
        out Texture2D? __state)
    {
        __state = null;
        if (!PowerIconFallbackScope.IsLibraryPower(__instance)
            || !PowerIconResolver.TryResolve(__instance, out ResolvedPowerIcon resolved))
        {
            return true;
        }

        __state = resolved.Texture;
        __result = resolved.Texture;
        return false;
    }

    private static void Postfix(Texture2D? __state, ref Texture2D? __result)
    {
        if (GodotTextureSafety.IsValid(__state))
        {
            __result = __state;
        }
    }
}

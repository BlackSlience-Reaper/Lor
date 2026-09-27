using Godot;
using HarmonyLib;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

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
        if (!PowerIconResolver.TryResolve(__instance, out ResolvedPowerIcon resolved))
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
        if (!PowerIconResolver.TryResolve(__instance, out ResolvedPowerIcon resolved))
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

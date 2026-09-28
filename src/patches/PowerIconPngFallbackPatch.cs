using Godot;
using HarmonyLib;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.patches;

internal static class PowerIconFallbackScope
{
    // Vanilla and third-party powers keep their own icons: vanilla ships powers/<id>.png as the big
    // art, so resolving it for Icon would swap every vanilla small icon for the large image.
    // Checked by defining assembly: third-party powers may derive from LibraryPowerModel too.
    internal static bool IsLibraryPower(PowerModel power) =>
        ModOwnership.IsOwn(power) || power.GetType().Assembly == typeof(LibraryPowerModel).Assembly;
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.Icon), MethodType.Getter)]
[HarmonyAfter("LibraryOfRuinaLib")]
[LibraryPatch(Reason = "PowerModel.Icon 非虚，本模组能力没有 power_atlas 资源，走原版会报资源缺失；只对本模组与 LibraryOfRuinaLib 的能力用 png。可改用 RitsuLib 纹理 provider，暂缓。")]
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
[LibraryPatch(Reason = "PowerModel.BigIcon 非虚；只对本模组与 LibraryOfRuinaLib 的能力用 png 大图。可改用 RitsuLib 纹理 provider，需要处理基础库图标后缀，暂缓。")]
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

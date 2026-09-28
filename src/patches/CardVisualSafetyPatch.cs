using System;
using System.Threading;
using HarmonyLib;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NCard), nameof(NCard._EnterTree))]
internal static class CardEnterTreeDisposedResourceSafetyPatch
{
    [HarmonyFinalizer]
    private static Exception? Finalizer(NCard __instance, Exception? __exception) =>
        CardVisualDisposedResourceSafety.Finalize(__instance, __exception, "NCard._EnterTree");
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals), typeof(PileType), typeof(CardPreviewMode))]
internal static class CardUpdateVisualsDisposedResourceSafetyPatch
{
    [HarmonyFinalizer]
    private static Exception? Finalizer(NCard __instance, Exception? __exception) =>
        CardVisualDisposedResourceSafety.Finalize(__instance, __exception, "NCard.UpdateVisuals");
}

internal static class CardVisualDisposedResourceSafety
{
    private const string LogTag = "LibraryOfRuina.CardVisualSafety";
    private static int _suppressedCount;

    public static Exception? Finalize(NCard card, Exception? exception, string surface)
    {
        // Only this mod's cards: their textures are the ones the disposed-resource issue affects,
        // and other cards' failures must stay visible.
        if (exception == null || !DisposedGodotResourceSafety.IsKnown(exception) || !IsOwnCard(card))
        {
            return exception;
        }

        if (Interlocked.Increment(ref _suppressedCount) <= 8)
        {
            Log.Warn(
                "[" + LogTag + "] suppressed disposed card visual resource"
                + " surface=" + surface
                + " card=" + ResolveCardId(card)
                + " reason=" + DisposedGodotResourceSafety.Describe(exception));
        }

        return null;
    }

    private static bool IsOwnCard(NCard card)
    {
        try
        {
            return ModOwnership.IsOwn(card.Model);
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveCardId(NCard card)
    {
        try
        {
            return card.Model?.Id.Entry ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}

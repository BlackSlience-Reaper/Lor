using System;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NRelicInventoryHolder), "OnFocus")]
internal static class RelicInventoryHolderOnFocusSafetyPatch
{
    private const string LogTag = "LibraryOfRuina.RelicHoverSafety";
    private static int _suppressedCount;

    [HarmonyFinalizer]
    private static Exception? Finalizer(
        NRelicInventoryHolder __instance,
        Exception? __exception)
    {
        if (__exception is not ObjectDisposedException objectDisposedException
            || !DisposedGodotResourceSafety.IsKnown(objectDisposedException)
            || !IsOwnRelic(__instance))
        {
            return __exception;
        }

        TryRemoveHoverTip(__instance);

        if (Interlocked.Increment(ref _suppressedCount) <= 3)
        {
            Log.Warn(
                "[" + LogTag + "] suppressed disposed relic hover tip"
                + " relic=" + ResolveRelicId(__instance)
                + " object=" + objectDisposedException.ObjectName
                + " reason=" + objectDisposedException.Message);
        }

        return null;
    }

    private static void TryRemoveHoverTip(NRelicInventoryHolder holder)
    {
        try
        {
            NHoverTipSet.Remove(holder);
        }
        catch
        {
        }
    }

    private static bool IsOwnRelic(NRelicInventoryHolder holder)
    {
        try
        {
            return ModOwnership.IsOwn(holder.Relic?.Model);
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveRelicId(NRelicInventoryHolder holder)
    {
        try
        {
            return holder.Relic?.Model?.Id.Entry ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}

[HarmonyPatch(typeof(NCardGrid), "InitGrid", typeof(Task))]
internal static class CardGridInitDisposedResourceSafetyPatch
{
    private const string LogTag = "LibraryOfRuina.CardGridSafety";
    private static int _suppressedCount;

    private static void Postfix(ref Task __result)
    {
        if (__result.IsCompletedSuccessfully)
        {
            return;
        }

        __result = SuppressDisposedResource(__result);
    }

    private static async Task SuppressDisposedResource(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (DisposedGodotResourceSafety.IsKnown(exception))
        {
            if (Interlocked.Increment(ref _suppressedCount) <= 3)
            {
                Log.Warn(
                    "[" + LogTag + "] suppressed disposed card grid UI resource"
                    + " reason=" + DisposedGodotResourceSafety.Describe(exception));
            }
        }
    }
}

internal static class DisposedGodotResourceSafety
{
    public static bool IsKnown(Exception? exception) =>
        exception is ObjectDisposedException objectDisposedException
        && objectDisposedException.ObjectName?.StartsWith("Godot.", StringComparison.Ordinal) == true;

    public static string Describe(Exception? exception)
    {
        if (exception is ObjectDisposedException objectDisposedException)
        {
            return objectDisposedException.Message + " object=" + objectDisposedException.ObjectName;
        }

        return exception?.Message ?? "unknown disposed-resource failure";
    }
}

using System.Runtime.CompilerServices;
using HarmonyLib;
using LibraryOfRuina.content.acts;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace LibraryOfRuina.framework.audio;

internal static class NeowEntrySfxController
{
    public const string EntrySfxPath = "res://audio/sfx/neow/neow_entry.ogg";

    private const float VolumeDb = -2f;

    private static readonly HashSet<(int RunStateHash, ulong PlayerNetId)> PlayedRunEntries = new();
    private static readonly object Sync = new();

    public static void PlayOnEntry(Neow neow, bool isPreFinished)
    {
        if (isPreFinished
            || neow.Owner == null
            || !LocalContext.IsMe(neow.Owner)
            || neow.Owner.RunState.Act is not LibraryOfRuinaActModel)
        {
            return;
        }

        var key = (RuntimeHelpers.GetHashCode(neow.Owner.RunState), neow.Owner.NetId);
        lock (Sync)
        {
            if (!PlayedRunEntries.Add(key))
            {
                return;
            }
        }

        Log.Info("[LibraryOfRuina] Playing Neow entry SFX once for current run.");
        LocalOggOneShotPlayer.Play(EntrySfxPath, VolumeDb);
    }
}

[HarmonyPatch(typeof(AncientEventModel), "SetInitialEventState")]
internal static class NeowEntrySfxSetInitialEventStatePatch
{
    private static void Prefix(AncientEventModel __instance, bool isPreFinished)
    {
        if (__instance is Neow neow)
        {
            NeowEntrySfxController.PlayOnEntry(neow, isPreFinished);
        }
    }
}

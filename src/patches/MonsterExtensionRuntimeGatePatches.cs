using System.Linq;
using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.features.settings;
using LibraryOfRuina.relics.BookShadow;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;






internal static class MonsterExtensionRuntimeGate
{
    private static readonly Assembly ModAssembly = typeof(LibraryOfRuinaInitializer).Assembly;

    public static bool IsInjectedByThisMod(AbstractModel model) =>
        model.GetType().Assembly == ModAssembly;
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
[HarmonyPriority(Priority.Last)]
internal static class MonsterExtensionPullNextEncounterGatePatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, RoomType roomType, ref EncounterModel __result)
    {
        if (LibraryOfRuinaSettings.MonsterExtensionEnabled)
        {
            if (BookShadowEncounterReplacement.TryReplaceActiveGuestEncounter(
                    __instance,
                    roomType,
                    __result,
                    out EncounterModel bookShadowReplacement))
            {
                __result = bookShadowReplacement;
            }
            else if (LibraryEncounterWeighting.TrySelectRuntimeModReplacement(
                    __instance,
                    roomType,
                    __result,
                    out EncounterModel replacement))
            {
                __result = replacement;
            }

            __result = LibraryEncounterWeighting.ApplyEncounterCycleRule(
                __instance,
                roomType,
                __result);
            return;
        }

        if (!MonsterExtensionRuntimeGate.IsInjectedByThisMod(__result))
        {
            return;
        }

        LibraryEncounterWeighting.RestoreVanillaEncounters(RunManager.Instance.DebugOnlyGetState());
        if (LibraryEncounterWeighting.TryGetScheduledVanillaEncounter(
                __instance,
                roomType,
                out EncounterModel fallback))
        {
            __result = fallback;
        }
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEvent))]
internal static class MonsterExtensionPullNextEventGatePatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, RunState runState, ref EventModel __result)
    {
        if (LibraryOfRuinaSettings.MonsterExtensionEnabled
            || !MonsterExtensionRuntimeGate.IsInjectedByThisMod(__result))
        {
            return;
        }

        EventModel? fallback = __instance.AllEvents
            .Concat(ModelDb.AllSharedEvents)
            .FirstOrDefault(eventModel =>
                !MonsterExtensionRuntimeGate.IsInjectedByThisMod(eventModel)
                && eventModel.IsAllowed(runState)
                && !runState.VisitedEventIds.Contains(eventModel.Id));

        fallback ??= __instance.AllEvents
            .Concat(ModelDb.AllSharedEvents)
            .FirstOrDefault(eventModel =>
                !MonsterExtensionRuntimeGate.IsInjectedByThisMod(eventModel)
                && eventModel.IsAllowed(runState));

        if (fallback != null)
        {
            runState.AddVisitedEvent(fallback);
            __result = fallback;
        }
    }
}

[HarmonyPatch(typeof(RelicPoolModel), nameof(RelicPoolModel.GetUnlockedRelics))]
internal static class MonsterExtensionEventRelicPoolGatePatch
{
    [HarmonyPostfix]
    public static void Postfix(RelicPoolModel __instance, ref IEnumerable<RelicModel> __result)
    {
        if (LibraryOfRuinaSettings.MonsterExtensionEnabled || __instance is not EventRelicPool)
        {
            return;
        }

        __result = __result.Where(relic => !MonsterExtensionRuntimeGate.IsInjectedByThisMod(relic));
    }
}

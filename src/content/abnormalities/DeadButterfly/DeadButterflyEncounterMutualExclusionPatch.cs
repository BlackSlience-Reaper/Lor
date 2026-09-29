using System.Linq;
using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.DeadButterfly;

internal static class DeadButterflyEncounterMutualExclusion
{

    public static RoomSet? GetRoomSet(ActModel act) =>
        VanillaPrivate.ActModelRooms.Get(act) as RoomSet;

    public static bool IsButterflyWeak(EncounterModel encounter) =>
        encounter is DeadButterflyWeak;

    public static bool IsButterflyStrong(EncounterModel encounter) =>
        encounter is DeadButterflyStrong;

    public static bool IsButterflyVariant(EncounterModel encounter) =>
        IsButterflyWeak(encounter) || IsButterflyStrong(encounter);

    public static void ReplaceStrongIfWeakIsScheduled(ActModel act, RoomSet rooms)
    {
        if (!rooms.normalEncounters.Any(IsButterflyWeak))
        {
            return;
        }

        for (int i = 0; i < rooms.normalEncounters.Count; i++)
        {
            if (!IsButterflyStrong(rooms.normalEncounters[i]))
            {
                continue;
            }

            EncounterModel? replacement = SelectReplacement(
                act.AllRegularEncounters,
                rooms.normalEncounters,
                i);
            if (replacement != null)
            {
                rooms.normalEncounters[i] = replacement;
            }
        }
    }

    public static EncounterModel? SelectRuntimeReplacement(ActModel act, EncounterModel current, RoomSet rooms)
    {
        IReadOnlyList<EncounterModel> previousNormalEncounters = rooms.normalEncounters
            .Take(rooms.normalEncountersVisited)
            .ToList();

        bool weakSeen = previousNormalEncounters.Any(IsButterflyWeak);
        bool strongSeen = previousNormalEncounters.Any(IsButterflyStrong);
        if (IsButterflyStrong(current) && weakSeen)
        {
            return SelectReplacement(
                act.AllRegularEncounters,
                rooms.normalEncounters,
                GetCurrentNormalEncounterIndex(rooms));
        }

        if (IsButterflyWeak(current) && strongSeen)
        {
            return SelectReplacement(
                act.AllWeakEncounters,
                rooms.normalEncounters,
                GetCurrentNormalEncounterIndex(rooms));
        }

        return null;
    }

    private static int GetCurrentNormalEncounterIndex(RoomSet rooms)
    {
        return rooms.normalEncounters.Count == 0
            ? 0
            : rooms.normalEncountersVisited % rooms.normalEncounters.Count;
    }

    private static EncounterModel? SelectReplacement(
        IEnumerable<EncounterModel> pool,
        IReadOnlyList<EncounterModel> scheduledEncounters,
        int replacementIndex)
    {
        return LibraryEncounterWeighting.SelectModFirstReplacement(
            pool,
            scheduledEncounters,
            replacementIndex,
            IsButterflyVariant);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
internal static class DeadButterflyEncounterMutualExclusionGenerateRoomsPatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance)
    {
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !LibraryOfRuinaActModel.IsFirstFamily(__instance))
        {
            return;
        }

        RoomSet? rooms = DeadButterflyEncounterMutualExclusion.GetRoomSet(__instance);
        if (rooms == null)
        {
            return;
        }

        DeadButterflyEncounterMutualExclusion.ReplaceStrongIfWeakIsScheduled(__instance, rooms);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
internal static class DeadButterflyEncounterMutualExclusionPullNextPatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, RoomType roomType, ref EncounterModel __result)
    {
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !LibraryOfRuinaActModel.IsFirstFamily(__instance)
            || roomType != RoomType.Monster
            || !DeadButterflyEncounterMutualExclusion.IsButterflyVariant(__result))
        {
            return;
        }

        RoomSet? rooms = DeadButterflyEncounterMutualExclusion.GetRoomSet(__instance);
        if (rooms == null)
        {
            return;
        }

        EncounterModel? replacement =
            DeadButterflyEncounterMutualExclusion.SelectRuntimeReplacement(__instance, __result, rooms);
        if (replacement != null)
        {
            __result = replacement;
        }
    }
}

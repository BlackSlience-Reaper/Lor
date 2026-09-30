using System.Linq;
using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.encounters.AllAroundHelper;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.patches.AllAroundHelper;

internal static class AllAroundHelperEncounterMutualExclusion
{

    public static RoomSet? GetRoomSet(ActModel act) =>
        VanillaPrivate.ActModelRooms.Get(act) as RoomSet;

    public static bool IsHelperWeak(EncounterModel encounter) =>
        encounter is AllAroundHelperWeak;

    public static bool IsHelperStrong(EncounterModel encounter) =>
        encounter is AllAroundHelperStrong;

    public static bool IsHelperVariant(EncounterModel encounter) =>
        IsHelperWeak(encounter) || IsHelperStrong(encounter);

    public static void ReplaceStrongIfWeakIsScheduled(ActModel act, RoomSet rooms)
    {
        if (!rooms.normalEncounters.Any(IsHelperWeak))
        {
            return;
        }

        for (int i = 0; i < rooms.normalEncounters.Count; i++)
        {
            if (!IsHelperStrong(rooms.normalEncounters[i]))
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

        bool weakSeen = previousNormalEncounters.Any(IsHelperWeak);
        bool strongSeen = previousNormalEncounters.Any(IsHelperStrong);
        if (IsHelperStrong(current) && weakSeen)
        {
            return SelectReplacement(
                act.AllRegularEncounters,
                rooms.normalEncounters,
                GetCurrentNormalEncounterIndex(rooms));
        }

        if (IsHelperWeak(current) && strongSeen)
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
            IsHelperVariant);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
internal static class AllAroundHelperEncounterMutualExclusionGenerateRoomsPatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance)
    {
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !LibraryOfRuinaActModel.IsSecondFamily(__instance))
        {
            return;
        }

        RoomSet? rooms = AllAroundHelperEncounterMutualExclusion.GetRoomSet(__instance);
        if (rooms == null)
        {
            return;
        }

        AllAroundHelperEncounterMutualExclusion.ReplaceStrongIfWeakIsScheduled(__instance, rooms);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
internal static class AllAroundHelperEncounterMutualExclusionPullNextPatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, RoomType roomType, ref EncounterModel __result)
    {
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !LibraryOfRuinaActModel.IsSecondFamily(__instance)
            || roomType != RoomType.Monster
            || !AllAroundHelperEncounterMutualExclusion.IsHelperVariant(__result))
        {
            return;
        }

        RoomSet? rooms = AllAroundHelperEncounterMutualExclusion.GetRoomSet(__instance);
        if (rooms == null)
        {
            return;
        }

        EncounterModel? replacement =
            AllAroundHelperEncounterMutualExclusion.SelectRuntimeReplacement(__instance, __result, rooms);
        if (replacement != null)
        {
            __result = replacement;
        }
    }
}

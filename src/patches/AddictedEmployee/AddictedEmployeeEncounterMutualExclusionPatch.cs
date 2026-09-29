using System.Linq;
using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.encounters.AddictedEmployee;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.patches.AddictedEmployee;

internal static class AddictedEmployeeEncounterMutualExclusion
{

    public static RoomSet? GetRoomSet(ActModel act) =>
        VanillaPrivate.ActModelRooms.Get(act) as RoomSet;

    public static bool IsEmployeeWeak(EncounterModel encounter) =>
        encounter is AddictedEmployeeWeak;

    public static bool IsEmployeeStrong(EncounterModel encounter) =>
        encounter is AddictedEmployeeStrong;

    public static bool IsEmployeeVariant(EncounterModel encounter) =>
        IsEmployeeWeak(encounter) || IsEmployeeStrong(encounter);

    public static void ReplaceStrongIfWeakIsScheduled(ActModel act, RoomSet rooms)
    {
        if (!rooms.normalEncounters.Any(IsEmployeeWeak))
        {
            return;
        }

        for (int i = 0; i < rooms.normalEncounters.Count; i++)
        {
            if (!IsEmployeeStrong(rooms.normalEncounters[i]))
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

        bool weakSeen = previousNormalEncounters.Any(IsEmployeeWeak);
        bool strongSeen = previousNormalEncounters.Any(IsEmployeeStrong);
        if (IsEmployeeStrong(current) && weakSeen)
        {
            return SelectReplacement(
                act.AllRegularEncounters,
                rooms.normalEncounters,
                GetCurrentNormalEncounterIndex(rooms));
        }

        if (IsEmployeeWeak(current) && strongSeen)
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
            IsEmployeeVariant);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
internal static class AddictedEmployeeEncounterMutualExclusionGenerateRoomsPatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance)
    {
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !LibraryOfRuinaActModel.IsFirstFamily(__instance))
        {
            return;
        }

        RoomSet? rooms = AddictedEmployeeEncounterMutualExclusion.GetRoomSet(__instance);
        if (rooms == null)
        {
            return;
        }

        AddictedEmployeeEncounterMutualExclusion.ReplaceStrongIfWeakIsScheduled(__instance, rooms);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
internal static class AddictedEmployeeEncounterMutualExclusionPullNextPatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, RoomType roomType, ref EncounterModel __result)
    {
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !LibraryOfRuinaActModel.IsFirstFamily(__instance)
            || roomType != RoomType.Monster
            || !AddictedEmployeeEncounterMutualExclusion.IsEmployeeVariant(__result))
        {
            return;
        }

        RoomSet? rooms = AddictedEmployeeEncounterMutualExclusion.GetRoomSet(__instance);
        if (rooms == null)
        {
            return;
        }

        EncounterModel? replacement =
            AddictedEmployeeEncounterMutualExclusion.SelectRuntimeReplacement(__instance, __result, rooms);
        if (replacement != null)
        {
            __result = replacement;
        }
    }
}


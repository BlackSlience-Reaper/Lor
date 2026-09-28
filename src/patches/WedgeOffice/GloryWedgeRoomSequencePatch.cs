using System;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.encounters.DawnOffice;
using LibraryOfRuina.encounters.WedgeOffice;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.patches.WedgeOffice;

internal static class GloryWedgeRoomSequenceNormalizer
{
    public static void Normalize(ActModel actModel, bool preserveVisitedPrefix)
    {
        if (!LibraryOfRuinaSettings.MonsterExtensionEnabled
            || !LibraryOfRuinaActModel.IsThirdFamily(actModel))
        {
            return;
        }

        RoomSet rooms = VanillaPrivate.ActModelRooms.GetRequired(actModel);
        List<EncounterModel> normalEncounters = rooms.normalEncounters;
        if (normalEncounters.Count < 2)
        {
            return;
        }

        int visitedCutoff = preserveVisitedPrefix
            ? Math.Clamp(rooms.normalEncountersVisited, 0, normalEncounters.Count)
            : 0;

        List<int> regularIndices = GetRegularIndices(normalEncounters);
        if (regularIndices.Count < 2)
        {
            return;
        }

        var mutableRegularIndices = new List<int>(regularIndices.Count);
        int previousVisitedRegularIndex = -1;
        for (int i = 0; i < regularIndices.Count; i++)
        {
            int index = regularIndices[i];
            if (index < visitedCutoff)
            {
                previousVisitedRegularIndex = index;
                continue;
            }

            mutableRegularIndices.Add(index);
        }

        if (mutableRegularIndices.Count == 0)
        {
            return;
        }

        IReadOnlyList<EncounterModel> replacementPool = BuildRegularReplacementPool(actModel);
        bool previousVisitedWasDawn =
            previousVisitedRegularIndex >= 0
            && normalEncounters[previousVisitedRegularIndex] is DawnOfficeNormal;

        if (previousVisitedWasDawn)
        {
            int forcedWedgeIndex = mutableRegularIndices[0];
            normalEncounters[forcedWedgeIndex] = ModelDb.Encounter<WedgeOfficeNormal>();
            RemoveIllegalWedges(normalEncounters, mutableRegularIndices, forcedWedgeIndex, replacementPool);
            return;
        }

        RemoveIllegalWedges(normalEncounters, mutableRegularIndices, allowedWedgeIndex: -1, replacementPool);

        int dawnPosition = FindRegularPosition<DawnOfficeNormal>(normalEncounters, mutableRegularIndices);
        if (dawnPosition < 0)
        {
            return;
        }

        if (dawnPosition == mutableRegularIndices.Count - 1)
        {
            if (dawnPosition == 0)
            {
                return;
            }

            int dawnIndex = mutableRegularIndices[dawnPosition];
            int previousRegularIndex = mutableRegularIndices[dawnPosition - 1];
            Swap(normalEncounters, dawnIndex, previousRegularIndex);
            dawnPosition--;
        }

        int wedgeTargetIndex = mutableRegularIndices[dawnPosition + 1];
        normalEncounters[wedgeTargetIndex] = ModelDb.Encounter<WedgeOfficeNormal>();
        RemoveIllegalWedges(normalEncounters, mutableRegularIndices, wedgeTargetIndex, replacementPool);
    }

    private static List<EncounterModel> BuildRegularReplacementPool(ActModel actModel)
    {
        var replacementPool = new List<EncounterModel>();
        foreach (EncounterModel encounter in actModel.AllRegularEncounters)
        {
            if (encounter is DawnOfficeNormal or WedgeOfficeNormal)
            {
                continue;
            }

            replacementPool.Add(encounter);
        }

        return replacementPool;
    }

    private static void RemoveIllegalWedges(
        List<EncounterModel> normalEncounters,
        IReadOnlyList<int> mutableRegularIndices,
        int allowedWedgeIndex,
        IReadOnlyList<EncounterModel> replacementPool)
    {
        if (replacementPool.Count == 0)
        {
            return;
        }

        for (int i = 0; i < mutableRegularIndices.Count; i++)
        {
            int index = mutableRegularIndices[i];
            if (index == allowedWedgeIndex || normalEncounters[index] is not WedgeOfficeNormal)
            {
                continue;
            }

            normalEncounters[index] = PickDeterministicReplacement(normalEncounters, replacementPool);
        }
    }

    private static EncounterModel PickDeterministicReplacement(
        IReadOnlyList<EncounterModel> normalEncounters,
        IReadOnlyList<EncounterModel> replacementPool)
    {
        return FindReplacement(normalEncounters, replacementPool, LibraryEncounterWeighting.IsModEncounter)
            ?? FindReplacement(normalEncounters, replacementPool, encounter => !LibraryEncounterWeighting.IsModEncounter(encounter))
            ?? replacementPool[0];
    }

    private static EncounterModel? FindReplacement(
        IReadOnlyList<EncounterModel> normalEncounters,
        IReadOnlyList<EncounterModel> replacementPool,
        Func<EncounterModel, bool> predicate)
    {
        for (int i = 0; i < replacementPool.Count; i++)
        {
            EncounterModel candidate = replacementPool[i];
            if (predicate(candidate) && !ContainsEncounterId(normalEncounters, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool ContainsEncounterId(
        IReadOnlyList<EncounterModel> normalEncounters,
        EncounterModel encounter)
    {
        for (int i = 0; i < normalEncounters.Count; i++)
        {
            if (normalEncounters[i].Id == encounter.Id)
            {
                return true;
            }
        }

        return false;
    }

    private static List<int> GetRegularIndices(IReadOnlyList<EncounterModel> normalEncounters)
    {
        var regularIndices = new List<int>(normalEncounters.Count);
        for (int i = 0; i < normalEncounters.Count; i++)
        {
            if (!normalEncounters[i].IsWeak)
            {
                regularIndices.Add(i);
            }
        }

        return regularIndices;
    }

    private static int FindRegularPosition<TEncounter>(
        IReadOnlyList<EncounterModel> normalEncounters,
        IReadOnlyList<int> regularIndices)
        where TEncounter : EncounterModel
    {
        for (int i = 0; i < regularIndices.Count; i++)
        {
            int index = regularIndices[i];
            if (normalEncounters[index] is TEncounter)
            {
                return i;
            }
        }

        return -1;
    }

    private static void Swap(List<EncounterModel> list, int firstIndex, int secondIndex)
    {
        if (firstIndex == secondIndex)
        {
            return;
        }

        (list[firstIndex], list[secondIndex]) = (list[secondIndex], list[firstIndex]);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
internal static class GloryWedgeRoomSequencePatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance)
    {
        GloryWedgeRoomSequenceNormalizer.Normalize(__instance, preserveVisitedPrefix: false);
    }
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.ValidateRoomsAfterLoad))]
internal static class GloryWedgeRoomSequenceAfterLoadPatch
{
    [HarmonyPostfix]
    private static void Postfix(ActModel __instance)
    {
        GloryWedgeRoomSequenceNormalizer.Normalize(__instance, preserveVisitedPrefix: true);
    }
}

using System.Linq;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.events.AltarEnchantment;
using LibraryOfRuina.events.FuneralOfTheDeadButterflies;
using LibraryOfRuina.events.SongMachine;
using LibraryOfRuina.events.WarpTrain;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

internal static class LibraryOfRuinaEventPool
{
    public static IEnumerable<EventModel> ForLibraryAct(
        LibraryActFamily family,
        IEnumerable<EventModel> baseEvents)
    {
        if (!LibraryOfRuinaSettings.MonsterExtensionEnabled)
        {
            return baseEvents;
        }

        EventModel eventForFamily = family switch
        {
            LibraryActFamily.First =>
                ModelDb.Event<FuneralOfTheDeadButterfliesEvent>(),
            LibraryActFamily.Second => ModelDb.Event<WarpTrainEvent>(),
            _ => ModelDb.Event<SingingMachineEvent>()
        };

        return baseEvents.Append(eventForFamily).DistinctBy(static model => model.Id);
    }

    public static IEnumerable<EventModel> AppendCatalogEvents(
        IEnumerable<EventModel> events)
    {
        return events.Concat(
        [
            ModelDb.Event<AncientMagicAltarEvent>(),
            ModelDb.Event<WarpTrainEvent>(),
            ModelDb.Event<SingingMachineEvent>(),
            ModelDb.Event<FuneralOfTheDeadButterfliesEvent>()
        ]).DistinctBy(static model => model.Id);
    }
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllEvents), MethodType.Getter)]
public static class LibraryOfRuinaAllEventsPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref IEnumerable<EventModel> __result)
    {
        __result = LibraryOfRuinaEventPool.AppendCatalogEvents(__result);
    }
}

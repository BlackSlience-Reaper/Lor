using Godot;
using HarmonyLib;
using LibraryOfRuina.content.acts;
using LibraryOfRuina.framework.encounters;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NBossMapPoint), "RefreshColorInstantly")]
internal static class LibraryBossMapColorPatch
{
    private static void Postfix(
        NBossMapPoint __instance,
        bool ____usesSpine,
        IRunState ____runState,
        TextureRect ____placeholderImage,
        TextureRect ____placeholderOutline)
    {
        EncounterModel? encounter =
            __instance.Point == ____runState.Map.SecondBossMapPoint
                ? ____runState.Act.SecondBossEncounter
                : ____runState.Act.BossEncounter;
        LibraryOfRuinaActModel? paletteAct = ResolvePaletteAct(encounter);
        if (____usesSpine
            || paletteAct == null
            || !GodotObject.IsInstanceValid(____placeholderImage)
            || !GodotObject.IsInstanceValid(____placeholderOutline))
        {
            return;
        }

        bool usesTraveledColor = __instance.State is
            MapPointState.Travelable or MapPointState.Traveled;
        ____placeholderImage.SelfModulate = usesTraveledColor
            ? paletteAct.BossMapTraveledColor
            : paletteAct.BossMapUntraveledColor;
        ____placeholderOutline.SelfModulate = paletteAct.BossMapOutlineColor;
    }

    private static LibraryOfRuinaActModel? ResolvePaletteAct(
        EncounterModel? encounter)
    {
        return (encounter as IFloorLiberationEncounter)?.LiberationFloorId
            switch
            {
                LiberationFloorIds.History => ModelDb.Act<Malkuth>(),
                LiberationFloorIds.Technology => ModelDb.Act<Yesod>(),
                LiberationFloorIds.Literature => ModelDb.Act<Hod>(),
                LiberationFloorIds.Art => ModelDb.Act<NetZech>(),
                LiberationFloorIds.Language => ModelDb.Act<Gebura>(),
                LiberationFloorIds.Natural => ModelDb.Act<Tiphereth>(),
                LiberationFloorIds.Social => ModelDb.Act<Chesed>(),
                LiberationFloorIds.Philosophy => ModelDb.Act<Binah>(),
                LiberationFloorIds.Religion => ModelDb.Act<Hokma>(),
                _ => null
            };
    }
}

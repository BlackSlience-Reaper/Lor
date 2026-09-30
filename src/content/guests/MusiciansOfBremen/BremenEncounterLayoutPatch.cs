using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.guests.MusiciansOfBremen;

[HarmonyPatch(typeof(NCreatureStateDisplay), nameof(NCreatureStateDisplay.SetCreature))]
public static class BremenEncounterLayoutPatch
{
    private const string MuMuSlotName = "mumu";
    private const float MuMuStateDisplayLiftY = 24f;


    [HarmonyPostfix]
    public static void Postfix(Creature creature, NCreatureStateDisplay __instance)
    {
        if (creature.Monster is not MuMu || creature.SlotName != MuMuSlotName)
        {
            return;
        }

        Vector2 liftedPosition = __instance.Position + Vector2.Up * MuMuStateDisplayLiftY;
        __instance.Position = liftedPosition;

        VanillaPrivate.CreatureStateDisplayOriginalPosition.Set(__instance, liftedPosition);
    }
}

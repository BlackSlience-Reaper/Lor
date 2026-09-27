using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.guests.MusiciansOfBremen;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.MusiciansOfBremen;

[HarmonyPatch(typeof(NCreatureStateDisplay), nameof(NCreatureStateDisplay.SetCreature))]
public static class BremenEncounterLayoutPatch
{
    private const string MuMuSlotName = "mumu";
    private const float MuMuStateDisplayLiftY = 24f;

    private static readonly FieldInfo? OriginalPositionField =
        AccessTools.Field(typeof(NCreatureStateDisplay), "_originalPosition");

    [HarmonyPostfix]
    public static void Postfix(Creature creature, NCreatureStateDisplay __instance)
    {
        if (creature.Monster is not MuMu || creature.SlotName != MuMuSlotName)
        {
            return;
        }

        Vector2 liftedPosition = __instance.Position + Vector2.Up * MuMuStateDisplayLiftY;
        __instance.Position = liftedPosition;

        OriginalPositionField?.SetValue(__instance, liftedPosition);
    }
}

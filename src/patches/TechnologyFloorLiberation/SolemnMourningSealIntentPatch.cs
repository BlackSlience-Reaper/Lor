using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.helpers;
using LibraryOfRuina.powers.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.TechnologyFloorLiberation;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.UpdateIntent))]
internal static class SolemnMourningSealIntentPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        try
        {
            ApplySealedIntentVisuals(__instance);
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "SolemnMourningSealIntent.UpdateIntent",
                exception);
        }
    }

    private static void ApplySealedIntentVisuals(NCreature creatureNode)
    {
        if (creatureNode.Entity?.Monster == null)
        {
            return;
        }

        var sealPower = creatureNode.Entity.GetPower<SolemnMourningSealOnEnemyPower>();
        int sealCount = sealPower?.Amount ?? 0;

        int index = 0;
        foreach (var child in creatureNode.IntentContainer.GetChildren())
        {
            if (child is NIntent intentNode)
            {
                intentNode.Modulate = index < sealCount
                    ? new Color(1f, 1f, 1f, 0.5f)
                    : Colors.White;
                index++;
            }
        }
    }
}

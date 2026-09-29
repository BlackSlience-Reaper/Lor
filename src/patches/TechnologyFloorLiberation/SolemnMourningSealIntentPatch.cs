using System;
using Godot;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents.rendering;
using LibraryOfRuina.powers.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.TechnologyFloorLiberation;

internal static class SolemnMourningSealIntentPatch
{
    internal static IntentDecoratorOutcome OnUpdateIntent(NCreature __instance)
    {
        try
        {
            return ApplySealedIntentVisuals(__instance)
                ? IntentDecoratorOutcome.Applied
                : IntentDecoratorOutcome.Skipped;
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "SolemnMourningSealIntent.UpdateIntent",
                exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    private static bool ApplySealedIntentVisuals(NCreature creatureNode)
    {
        if (creatureNode.Entity?.Monster == null)
        {
            return false;
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

        return true;
    }
}

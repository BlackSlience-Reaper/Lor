using System;
using Godot;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Technology;

internal static class SolemnMourningSealIntentPatch
{
    internal static IntentDecoratorOutcome OnUpdateVisuals(NIntent intentNode, Creature owner)
    {
        try
        {
            return ApplySealedIntentVisual(intentNode, owner)
                ? IntentDecoratorOutcome.Applied
                : IntentDecoratorOutcome.Skipped;
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "SolemnMourningSealIntent.UpdateVisuals",
                exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    private static bool ApplySealedIntentVisual(NIntent intentNode, Creature owner)
    {
        if (owner.Monster == null)
        {
            return false;
        }

        var sealPower = owner.GetPower<SolemnMourningSealOnEnemyPower>();
        int sealCount = sealPower?.Amount ?? 0;

        // 原版及本模组的意图容器只放 NIntent；重绘与封印移除时先恢复此节点，再由和弦追加变暗。
        intentNode.Modulate = intentNode.GetIndex() < sealCount
            ? new Color(1f, 1f, 1f, 0.5f)
            : Colors.White;

        return true;
    }
}

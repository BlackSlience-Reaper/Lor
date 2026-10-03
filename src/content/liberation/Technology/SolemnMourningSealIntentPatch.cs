using System;
using Godot;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Technology;

internal static class SolemnMourningSealIntentPatch
{
    /// <summary>默认画法：意图节点与招式意图一一对应（复合简化后的列表），按节点下标判断。</summary>
    internal static IntentDecoratorOutcome OnUpdateVisuals(NIntent intentNode, Creature owner) =>
        OnUpdateVisuals(intentNode, owner, static node => node.GetIndex());

    /// <summary>
    /// 原版风格画法：一个源意图可能拆成几个节点，节点下标会错位，所以由调用方给出该节点对应的源意图下标；
    /// 封印按“招式里的前 N 个意图”计算，同一个源意图拆出来的节点一起变暗。
    /// </summary>
    internal static IntentDecoratorOutcome OnUpdateVisuals(NIntent intentNode, Creature owner, Func<NIntent, int> sourceIndexOf)
    {
        try
        {
            return ApplySealedIntentVisual(intentNode, owner, sourceIndexOf)
                ? IntentDecoratorOutcome.Applied
                : IntentDecoratorOutcome.Skipped;
        }
        catch (Exception exception)
        {
            LorLog.PatchFailure(
                "SolemnMourningSealIntent.UpdateVisuals",
                exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    private static bool ApplySealedIntentVisual(NIntent intentNode, Creature owner, Func<NIntent, int> sourceIndexOf)
    {
        if (owner.Monster == null)
        {
            return false;
        }

        var sealPower = owner.GetPower<SolemnMourningSealOnEnemyPower>();
        int sealCount = sealPower?.Amount ?? 0;

        // 原版及本模组的意图容器只放 NIntent；重绘与封印移除时先恢复此节点，再由和弦追加变暗。
        intentNode.Modulate = sourceIndexOf(intentNode) < sealCount
            ? new Color(1f, 1f, 1f, 0.5f)
            : Colors.White;

        return true;
    }
}

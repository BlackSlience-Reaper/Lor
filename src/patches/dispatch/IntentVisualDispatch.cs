using HarmonyLib;
using LibraryOfRuina.patches.TechnologyFloorLiberation;
using LibraryOfRuina.ui;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.dispatch;

/// <summary>
/// 意图显示相关的补丁。每个嵌套类对应原来同一目标、同一优先级的一段补丁，按原执行顺序调用各功能的处理函数。
/// 几种复合意图之间的先后是隐式约定（例如 Badged 看到 Combined 就让出），调整顺序前先读各处理函数。
/// </summary>
internal static class IntentVisualDispatch
{
    /// <summary>先把多个意图合并成复合意图、再追加反击意图；必须早于下面其他装饰。</summary>
    [HarmonyPatch(typeof(NCreature), nameof(NCreature.UpdateIntent))]
    private static class UpdateIntentFirst
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void Postfix(NCreature __instance, IEnumerable<Creature> targets)
        {
            CombinedIntentDisplayPatch.OnUpdateIntent(__instance, targets);
            CounterIntentAppendPatch.OnUpdateIntent(__instance, targets);
        }
    }

    [HarmonyPatch(typeof(NCreature), nameof(NCreature.UpdateIntent))]
    private static class UpdateIntent
    {
        [HarmonyPostfix]
        private static void Postfix(NCreature __instance, IEnumerable<Creature> targets)
        {
            EnemyCardIntentRuntimePatch.OnUpdateIntent(__instance, targets);
            TargetedIntentIndicatorPatch.OnUpdateIntent(__instance);
            ChordEgoIntentDimPatch.OnUpdateIntent(__instance);
            SolemnMourningSealIntentPatch.OnUpdateIntent(__instance);
        }
    }

    /// <summary>两个悬停提示都可能接管原版悬停；前一个接管后后一个不再执行，与原来两个跳过型前缀相同。</summary>
    [HarmonyPatch(typeof(NIntent), "OnHovered")]
    private static class Hovered
    {
        [HarmonyPrefix]
        private static bool Prefix(AbstractIntent ____intent, IEnumerable<Creature> ____targets, Creature ____owner) =>
            EnemyCardIntentHoverPatch.OnIntentHovered(____intent, ____targets, ____owner)
            && BadgedIntentHoverTipDisplayPatch.OnIntentHovered(____intent, ____targets, ____owner);
    }

    [HarmonyPatch(typeof(NIntent), "UpdateVisuals")]
    private static class UpdateVisuals
    {
        [HarmonyPostfix]
        private static void Postfix(NIntent __instance, AbstractIntent ____intent, IEnumerable<Creature> ____targets, Creature ____owner)
        {
            BadgedIntentVisualPatch.OnUpdateVisuals(__instance, ____intent, ____targets, ____owner);
            CombinedIntentAnimationRefreshPatch.OnUpdateVisuals(__instance, ____intent, ____targets, ____owner);
            FoxIntentValuePatch.OnUpdateVisuals(__instance, ____intent);
        }
    }

    [HarmonyPatch(typeof(NIntent), "_Process")]
    private static class Process
    {
        [HarmonyPostfix]
        private static void Postfix(NIntent __instance, int? ____animationFrame)
        {
            CombinedIntentVisualPatch.OnIntentProcess(__instance, ____animationFrame);
            CounterIntentVisualPatch.OnIntentProcess(__instance, ____animationFrame);
        }
    }
}

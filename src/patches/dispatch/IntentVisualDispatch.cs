using HarmonyLib;
using LibraryOfRuina.intents.rendering;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.patches.dispatch;

/// <summary>
/// 意图显示的补丁入口，只负责把参数转交 <see cref="IntentRenderPipeline"/>。
/// 每个嵌套类对应一个入口（<see cref="IntentRenderStage"/>），保留原来那段补丁的目标、种类和优先级；
/// 装饰器的先后与短路在流水线的顺序表里，不在这里。
/// </summary>
internal static class IntentVisualDispatch
{
    [HarmonyPatch(typeof(NCreature), nameof(NCreature.UpdateIntent))]
    private static class UpdateIntentFirst
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void Postfix(NCreature __instance, IEnumerable<Creature> targets)
        {
            var context = new IntentRenderContext { CreatureNode = __instance, Targets = targets };
            IntentRenderPipeline.Run(IntentRenderStage.CreatureLayout, ref context);
        }
    }

    [HarmonyPatch(typeof(NCreature), nameof(NCreature.UpdateIntent))]
    private static class UpdateIntent
    {
        [HarmonyPostfix]
        private static void Postfix(NCreature __instance, IEnumerable<Creature> targets)
        {
            var context = new IntentRenderContext { CreatureNode = __instance, Targets = targets };
            IntentRenderPipeline.Run(IntentRenderStage.CreatureDecorate, ref context);
        }
    }

    /// <summary>带徽记或详细意图时显示多条提示并接管原版悬停。原来还有一个敌方卡牌悬停处理，恒返回 true，已删除。</summary>
    [HarmonyPatch(typeof(NIntent), "OnHovered")]
    [LibraryPatch(Reason = "AbstractIntent.GetHoverTip 非虚无 Hook；带本模组徽记或详细意图时显示多条提示并接管原版悬停。")]
    private static class Hovered
    {
        [HarmonyPrefix]
        private static bool Prefix(AbstractIntent ____intent, IEnumerable<Creature> ____targets, Creature ____owner)
        {
            var context = new IntentRenderContext
            {
                Intent = ____intent,
                Targets = ____targets,
                Owner = ____owner
            };
            return IntentRenderPipeline.Run(IntentRenderStage.IntentHovered, ref context);
        }
    }

    [HarmonyPatch(typeof(NIntent), "OnHovered")]
    private static class HoveredAfter
    {
        [HarmonyPostfix]
        private static void Postfix(NIntent __instance, AbstractIntent ____intent, IEnumerable<Creature> ____targets, Creature ____owner)
        {
            var context = new IntentRenderContext
            {
                IntentNode = __instance,
                Intent = ____intent,
                Targets = ____targets,
                Owner = ____owner
            };
            IntentRenderPipeline.Run(IntentRenderStage.IntentHoveredAfter, ref context);
        }
    }

    [HarmonyPatch(typeof(NIntent), "OnUnhovered")]
    private static class Unhovered
    {
        [HarmonyPostfix]
        private static void Postfix(Creature ____owner)
        {
            var context = new IntentRenderContext { Owner = ____owner };
            IntentRenderPipeline.Run(IntentRenderStage.IntentUnhovered, ref context);
        }
    }

    [HarmonyPatch(typeof(NIntent), "UpdateVisuals")]
    private static class UpdateVisuals
    {
        [HarmonyPostfix]
        private static void Postfix(NIntent __instance, AbstractIntent ____intent, IEnumerable<Creature> ____targets, Creature ____owner)
        {
            var context = new IntentRenderContext
            {
                IntentNode = __instance,
                Intent = ____intent,
                Targets = ____targets,
                Owner = ____owner
            };
            IntentRenderPipeline.Run(IntentRenderStage.IntentVisuals, ref context);
        }
    }

    [HarmonyPatch(typeof(NIntent), "_Process")]
    private static class Process
    {
        [HarmonyPostfix]
        private static void Postfix(NIntent __instance, int? ____animationFrame)
        {
            var context = new IntentRenderContext { IntentNode = __instance, AnimationFrame = ____animationFrame };
            IntentRenderPipeline.Run(IntentRenderStage.IntentFrame, ref context);
        }
    }

    [HarmonyPatch(typeof(AbstractIntent), nameof(AbstractIntent.GetHoverTip))]
    private static class IntentHoverTip
    {
        [HarmonyPostfix]
        private static void Postfix(
            AbstractIntent __instance,
            IEnumerable<Creature> targets,
            Creature owner,
            ref HoverTip __result)
        {
            var context = new IntentRenderContext
            {
                Intent = __instance,
                Targets = targets,
                Owner = owner,
                HoverTip = __result
            };
            IntentRenderPipeline.Run(IntentRenderStage.HoverTip, ref context);
            __result = context.HoverTip;
        }
    }

    [HarmonyPatch(typeof(NCreature), nameof(NCreature.ShowHoverTips))]
    private static class CreatureHoverTips
    {
        [HarmonyPrefix]
        private static void Prefix(ref IEnumerable<IHoverTip> hoverTips)
        {
            var context = new IntentRenderContext { HoverTips = hoverTips };
            IntentRenderPipeline.Run(IntentRenderStage.CreatureHoverTips, ref context);
            hoverTips = context.HoverTips!;
        }
    }
}

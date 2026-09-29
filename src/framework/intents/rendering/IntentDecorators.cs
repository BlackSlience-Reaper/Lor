using LibraryOfRuina.patches;
using LibraryOfRuina.patches.TechnologyFloorLiberation;
using LibraryOfRuina.ui;
using MegaCrit.Sts2.Core.HoverTips;

namespace LibraryOfRuina.framework.intents.rendering;

// 各装饰器只做入口到处理函数的转发；绘制代码仍在原来的类里（类名、命名空间、文件位置都没动，原因见重构指导阶段 5d）。
// 处理函数自己的前置检查、try/catch 与日志保持原样，返回值只报告它做了什么。

/// <summary>徽记（IntentBadge）与详细意图（IDetailedIntentVisuals）：两者共用同一个视觉哈希和叠加节点的清理，不能拆开。</summary>
internal sealed class BadgeDetailIntentDecorator : IIntentDecorator
{
    public string Name => "BadgeDetail";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) => stage switch
    {
        IntentRenderStage.IntentVisuals => BadgedIntentVisualPatch.OnUpdateVisuals(
            context.IntentNode!, context.Intent!, context.Targets!, context.Owner!),
        IntentRenderStage.IntentHovered => BadgedIntentHoverTipDisplayPatch.OnIntentHovered(
            context.Intent!, context.Targets!, context.Owner!)
            ? IntentDecoratorOutcome.Skipped
            : IntentDecoratorOutcome.Handled,
        IntentRenderStage.HoverTip => BadgedIntentHoverTipPatch.ApplyBadgeTip(
            context.Intent!, context.Targets!, context.Owner!, ref context.HoverTip),
        _ => IntentDecoratorOutcome.Skipped
    };
}

/// <summary>Combined 复合意图：简化成对的原版意图、换动画帧、换提示图标。</summary>
internal sealed class CombinedIntentDecorator : IIntentDecorator
{
    public string Name => "Combined";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) => stage switch
    {
        IntentRenderStage.CreatureLayout => CombinedIntentDisplayPatch.OnUpdateIntent(
            context.CreatureNode!, context.Targets!),
        IntentRenderStage.IntentVisuals => CombinedIntentVisualPatch.RefreshAnimation(
            context.IntentNode!, context.Intent!, context.Targets!, context.Owner!),
        IntentRenderStage.IntentFrame => CombinedIntentVisualPatch.OnIntentProcess(
            context.IntentNode!, context.AnimationFrame),
        IntentRenderStage.HoverTip => BadgedIntentHoverTipPatch.ApplyCombinedHoverIcon(
            context.Intent!, ref context.HoverTip),
        _ => IntentDecoratorOutcome.Skipped
    };
}

/// <summary>反击意图：按观察者重排反击队列、换动画帧、补“反击”关键词提示。</summary>
internal sealed class CounterIntentDecorator : IIntentDecorator
{
    public string Name => "Counter";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context)
    {
        switch (stage)
        {
            case IntentRenderStage.CreatureLayout:
                return CounterIntentAppendPatch.OnUpdateIntent(context.CreatureNode!, context.Targets!);
            case IntentRenderStage.IntentVisuals:
                return CounterIntentVisualPatch.RefreshAnimation(
                    context.IntentNode!, context.Intent!, context.Targets!, context.Owner!);
            case IntentRenderStage.IntentFrame:
                return CounterIntentVisualPatch.OnIntentProcess(context.IntentNode!, context.AnimationFrame);
            case IntentRenderStage.CreatureHoverTips:
                IEnumerable<IHoverTip> hoverTips = context.HoverTips!;
                IntentDecoratorOutcome outcome = CounterIntentHoverTipPatch.AppendCounterKeyword(ref hoverTips);
                context.HoverTips = hoverTips;
                return outcome;
            default:
                return IntentDecoratorOutcome.Skipped;
        }
    }
}

/// <summary>敌方卡牌意图：在意图节点上方挂卡牌预览，没有卡牌意图时清掉残留。</summary>
internal sealed class EnemyCardIntentDecorator : IIntentDecorator
{
    public string Name => "EnemyCard";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) =>
        stage == IntentRenderStage.CreatureDecorate
            ? EnemyCardIntentRuntimePatch.OnUpdateIntent(context.CreatureNode!, context.Targets!)
            : IntentDecoratorOutcome.Skipped;
}

/// <summary>指定目标指示线：刷新意图时移除，悬停时画，移开时移除。</summary>
internal sealed class TargetIndicatorIntentDecorator : IIntentDecorator
{
    public string Name => "TargetIndicator";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) => stage switch
    {
        IntentRenderStage.CreatureDecorate => TargetedIntentIndicatorPatch.OnUpdateIntent(context.CreatureNode!),
        IntentRenderStage.IntentHoveredAfter => TargetedIntentHoverPatch.OnIntentHovered(
            context.IntentNode!, context.Intent!, context.Targets!, context.Owner!),
        IntentRenderStage.IntentUnhovered => TargetedIntentUnhoverPatch.OnIntentUnhovered(context.Owner!),
        _ => IntentDecoratorOutcome.Skipped
    };
}

/// <summary>狐狸的防御、强化、能量、虚弱意图把数值写进原版数字标签。</summary>
internal sealed class FoxIntentValueDecorator : IIntentDecorator
{
    public string Name => "FoxValue";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) =>
        stage == IntentRenderStage.IntentVisuals
            ? FoxIntentValuePatch.OnUpdateVisuals(context.IntentNode!, context.Intent!)
            : IntentDecoratorOutcome.Skipped;
}

/// <summary>技术层和弦 E.G.O.：格挡不足时后两段攻击意图变暗。</summary>
internal sealed class ChordEgoDimIntentDecorator : IIntentDecorator
{
    public string Name => "ChordEgoDim";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) =>
        stage == IntentRenderStage.IntentVisuals
            ? ChordEgoIntentDimPatch.OnUpdateVisuals(context.IntentNode!, context.Intent!, context.Owner!)
            : IntentDecoratorOutcome.Skipped;
}

/// <summary>技术层肃穆哀悼的封印：前 N 个意图变暗。</summary>
internal sealed class SolemnMourningSealIntentDecorator : IIntentDecorator
{
    public string Name => "SolemnMourningSeal";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) =>
        stage == IntentRenderStage.IntentVisuals
            ? SolemnMourningSealIntentPatch.OnUpdateVisuals(context.IntentNode!, context.Owner!)
            : IntentDecoratorOutcome.Skipped;
}

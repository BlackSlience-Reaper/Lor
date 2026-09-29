using System;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.framework.intents.rendering;

/// <summary>
/// 意图显示的入口。每个入口对应 <c>patches/dispatch/IntentVisualDispatch</c> 里的一个补丁段，
/// 补丁的目标、种类和优先级决定了它相对原版与其他模组补丁的位置，所以不同入口不能合并成一个。
/// </summary>
internal enum IntentRenderStage
{
    /// <summary><c>NCreature.UpdateIntent</c> 后缀，优先级 First：重排意图节点（复合显示、反击队列）。必须早于下面的装饰。</summary>
    CreatureLayout,

    /// <summary><c>NCreature.UpdateIntent</c> 后缀，默认优先级：在排好的节点上附加或清理（敌方卡牌、指示线）。</summary>
    CreatureDecorate,

    /// <summary><c>NIntent.UpdateVisuals</c> 后缀，排在 RitsuLib 角标后缀之后。</summary>
    IntentVisuals,

    /// <summary><c>NIntent._Process</c> 后缀，每帧执行，换动画帧。</summary>
    IntentFrame,

    /// <summary><c>NIntent.OnHovered</c> 跳过型前缀：第一个接手的装饰器显示自己的提示并跳过原版悬停。</summary>
    IntentHovered,

    /// <summary><c>NIntent.OnHovered</c> 后缀。</summary>
    IntentHoveredAfter,

    /// <summary><c>NIntent.OnUnhovered</c> 后缀。</summary>
    IntentUnhovered,

    /// <summary><c>AbstractIntent.GetHoverTip</c> 后缀：改写返回的提示，第一个接手的装饰器之后不再调用其余的。</summary>
    HoverTip,

    /// <summary><c>NCreature.ShowHoverTips</c> 前缀（不跳过原方法）：改写要显示的提示列表。</summary>
    CreatureHoverTips
}

/// <summary>一个装饰器在一次调用里做了什么。只用于短路判断和诊断转储，不改变装饰器自己的行为。</summary>
internal enum IntentDecoratorOutcome
{
    /// <summary>条件不满足，没有动节点。</summary>
    Skipped,

    /// <summary>条件满足，但结果与上次相同，没有重建（例如徽记的视觉哈希未变、动画帧未变）。</summary>
    Unchanged,

    /// <summary>做了修改。</summary>
    Applied,

    /// <summary>接手了这个入口：跳过型入口据此跳过原方法，改写返回值的入口据此不再调用后面的装饰器。</summary>
    Handled,

    /// <summary>抛出的异常已被装饰器自己捕获并记录。</summary>
    Failed
}

/// <summary>
/// 一次调用的参数。按值传递、按引用修改，<see cref="IntentRenderStage.IntentFrame"/> 每帧每个意图节点调用一次，不能分配。
/// 各入口只填自己用得到的字段。
/// </summary>
internal struct IntentRenderContext
{
    public NCreature? CreatureNode;
    public NIntent? IntentNode;
    public AbstractIntent? Intent;
    public IEnumerable<Creature>? Targets;
    public Creature? Owner;
    public int? AnimationFrame;

    /// <summary><see cref="IntentRenderStage.HoverTip"/>：原版生成的提示，装饰器可改写。</summary>
    public HoverTip HoverTip;

    /// <summary><see cref="IntentRenderStage.CreatureHoverTips"/>：要显示的提示，装饰器可改写。</summary>
    public IEnumerable<IHoverTip>? HoverTips;
}

/// <summary>
/// 意图显示的装饰器。一个装饰器可以参与多个入口；参与哪些、以什么顺序，由 <see cref="IntentRenderPipeline"/> 的顺序表决定。
/// 收到自己不处理的入口时返回 <see cref="IntentDecoratorOutcome.Skipped"/>。
/// </summary>
internal interface IIntentDecorator
{
    string Name { get; }

    IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context);
}

internal readonly record struct IntentRenderTraceEntry(
    IntentRenderStage Stage,
    string Decorator,
    IntentDecoratorOutcome Outcome);

/// <summary>
/// 按入口依次调用装饰器。顺序表还原了原来各补丁之间的隐式约定：
/// <list type="bullet">
/// <item>复合显示先于反击队列重排节点；两者的适用对象互斥（反击队列的怪物不做复合简化）。</item>
/// <item>徽记与详细意图先重建叠加节点，复合与反击意图随后记下动画；徽记遇到复合意图时只换友方攻击的着色方式。</item>
/// <item>悬停时徽记与详细意图接手原版提示；原版悬停之后再画指示线。</item>
/// <item>复合意图换掉提示图标后，徽记不再改写提示。</item>
/// <item>每个意图刷新时，封印先重置并应用当前封印颜色，和弦再追加未解锁段的变暗。</item>
/// </list>
/// 装饰器抛出的异常照常向外传播，与原来同一个补丁方法里依次调用时相同：后面的装饰器不再执行。
/// </summary>
internal static class IntentRenderPipeline
{
    private static readonly IIntentDecorator[][] Order = BuildOrder();

    private static IIntentDecorator[][] BuildOrder()
    {
        var badgeDetail = new BadgeDetailIntentDecorator();
        var combined = new CombinedIntentDecorator();
        var counter = new CounterIntentDecorator();
        var enemyCard = new EnemyCardIntentDecorator();
        var targetIndicator = new TargetIndicatorIntentDecorator();
        var fox = new FoxIntentValueDecorator();
        var chordEgoDim = new ChordEgoDimIntentDecorator();
        var solemnMourningSeal = new SolemnMourningSealIntentDecorator();

        var order = new IIntentDecorator[Enum.GetValues<IntentRenderStage>().Length][];
        order[(int)IntentRenderStage.CreatureLayout] = [combined, counter];
        order[(int)IntentRenderStage.CreatureDecorate] = [enemyCard, targetIndicator];
        // 原版战斗状态变化只刷新 NIntent.UpdateVisuals；逐节点调色同时覆盖格挡和封印变化。
        order[(int)IntentRenderStage.IntentVisuals] = [badgeDetail, combined, counter, fox, solemnMourningSeal, chordEgoDim];
        order[(int)IntentRenderStage.IntentFrame] = [combined, counter];
        order[(int)IntentRenderStage.IntentHovered] = [badgeDetail];
        order[(int)IntentRenderStage.IntentHoveredAfter] = [targetIndicator];
        order[(int)IntentRenderStage.IntentUnhovered] = [targetIndicator];
        order[(int)IntentRenderStage.HoverTip] = [combined, badgeDetail];
        order[(int)IntentRenderStage.CreatureHoverTips] = [counter];
        return order;
    }

    /// <summary>
    /// 依次调用该入口的装饰器。遇到 <see cref="IntentDecoratorOutcome.Handled"/> 即停止，返回 false；
    /// 跳过型前缀把返回值原样交给 Harmony（false 表示跳过原方法）。
    /// </summary>
    internal static bool Run(
        IntentRenderStage stage,
        ref IntentRenderContext context,
        List<IntentRenderTraceEntry>? trace = null)
    {
        IIntentDecorator[] decorators = Order[(int)stage];
        for (int i = 0; i < decorators.Length; i++)
        {
            IntentDecoratorOutcome outcome = decorators[i].Render(stage, ref context);
            trace?.Add(new IntentRenderTraceEntry(stage, decorators[i].Name, outcome));
            if (outcome == IntentDecoratorOutcome.Handled)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>该入口依次调用的装饰器名，供诊断与验证。</summary>
    internal static IReadOnlyList<string> DecoratorNames(IntentRenderStage stage)
    {
        IIntentDecorator[] decorators = Order[(int)stage];
        var names = new string[decorators.Length];
        for (int i = 0; i < decorators.Length; i++)
        {
            names[i] = decorators[i].Name;
        }

        return names;
    }
}

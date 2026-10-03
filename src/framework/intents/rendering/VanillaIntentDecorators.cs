using System;
using System.Linq;
using Godot;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.framework.intents.rendering;

// 原版风格画法（IntentDisplayStyle.Vanilla）专用的装饰器，只出现在 IntentRenderPipeline 的原版风格顺序表里。
// 节点上挂的是 VanillaIntentMapper 生成的显示意图；凡是要按“源意图”判断的（悬停描述、指示线、封印、和弦）都经
// VanillaIntentProxies 查回招式里的源意图。

/// <summary>CreatureLayout：把招式意图映射成原版意图并重画节点（含反击队列的同步与按观察者过滤）。</summary>
internal sealed class VanillaLayoutIntentDecorator : IIntentDecorator
{
    public string Name => "VanillaLayout";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) =>
        stage == IntentRenderStage.CreatureLayout
            ? VanillaIntentDisplayPatch.OnUpdateIntent(context.CreatureNode!, context.Targets!)
            : IntentDecoratorOutcome.Skipped;
}

/// <summary>CreatureDecorate：敌方卡牌按出牌顺序映射成原版意图，卡牌照默认画法挂在意图上方；没有敌方卡牌时还原容器布局。</summary>
internal sealed class VanillaEnemyCardIntentDecorator : IIntentDecorator
{
    public string Name => "VanillaEnemyCard";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) =>
        stage == IntentRenderStage.CreatureDecorate
            ? VanillaIntentDisplayPatch.OnDecorate(context.CreatureNode!, context.Targets!)
            : IntentDecoratorOutcome.Skipped;
}

/// <summary>
/// IntentVisuals：清掉默认画法留在复用节点上的效果行、角标与目标标记，再做两种画法共用的着色（友方攻击变绿）。
/// 默认画法里这一步写在徽记装饰器开头，原版风格画法不跑徽记装饰器，所以单独拆出来。
/// </summary>
internal sealed class VanillaTintIntentDecorator : IIntentDecorator
{
    public string Name => "VanillaTint";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context)
    {
        if (stage != IntentRenderStage.IntentVisuals || !context.IntentNode!.HasNode("%IntentHolder"))
        {
            return IntentDecoratorOutcome.Skipped;
        }

        Control holder = context.IntentNode.GetNode<Control>("%IntentHolder");
        BadgedIntentVisualPatch.RemoveOverlays(holder);
        BadgedIntentVisualPatch.ResetIntentTint(context.IntentNode, holder, context.Intent!, context.Owner!);
        return IntentDecoratorOutcome.Applied;
    }
}

/// <summary>IntentVisuals：肃穆哀悼的封印按源意图下标变暗，同一个源意图拆出的节点一起变暗。</summary>
internal sealed class VanillaSolemnMourningSealIntentDecorator : IIntentDecorator
{
    public string Name => "VanillaSolemnMourningSeal";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context)
    {
        if (stage != IntentRenderStage.IntentVisuals)
        {
            return IntentDecoratorOutcome.Skipped;
        }

        AbstractIntent intent = context.Intent!;
        Creature owner = context.Owner!;
        return SolemnMourningSealIntentPatch.OnUpdateVisuals(
            context.IntentNode!,
            owner,
            node => SourceIndexOf(node, intent, owner));
    }

    // 代理记着源下标；原样显示的意图在招式里按引用找；运行时出牌计划展开的意图不在招式里，退回节点下标（与默认画法相同）。
    private static int SourceIndexOf(NIntent node, AbstractIntent intent, Creature owner)
    {
        if (VanillaIntentProxies.TryGetSource(intent, out VanillaIntentProxySource? source) && source.SourceIndex >= 0)
        {
            return source.SourceIndex;
        }

        IReadOnlyList<AbstractIntent>? moveIntents = owner.Monster?.NextMove?.Intents;
        int index = moveIntents == null ? -1 : VanillaIntentMapper.IndexOfReference(moveIntents, intent);
        return index >= 0 ? index : node.GetIndex();
    }
}

/// <summary>IntentVisuals：和弦 E.G.O. 的第 B、C 段按源意图引用识别（代理与源意图不是同一个对象）。</summary>
internal sealed class VanillaChordEgoDimIntentDecorator : IIntentDecorator
{
    public string Name => "VanillaChordEgoDim";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context) =>
        stage == IntentRenderStage.IntentVisuals
            ? ChordEgoIntentDimPatch.OnUpdateVisuals(
                context.IntentNode!,
                VanillaIntentProxies.SourceOf(context.Intent!),
                context.Owner!)
            : IntentDecoratorOutcome.Skipped;
}

/// <summary>IntentHoveredAfter：指示线跟源意图走（指定目标、会打到怪物或友方的群体攻击）；从附带效果拆出的图标不画线。</summary>
internal sealed class VanillaTargetIndicatorIntentDecorator : IIntentDecorator
{
    public string Name => "VanillaTargetIndicator";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context)
    {
        if (stage != IntentRenderStage.IntentHoveredAfter)
        {
            return IntentDecoratorOutcome.Skipped;
        }

        AbstractIntent intent = context.Intent!;
        AbstractIntent lineIntent = VanillaIntentProxies.TryGetSource(intent, out VanillaIntentProxySource? source) && source.IsPrimary
            ? source.Source
            : intent;
        return TargetedIntentHoverPatch.OnIntentHovered(context.IntentNode!, lineIntent, context.Targets!, context.Owner!);
    }
}

/// <summary>
/// 显示意图的悬停提示。原版一个意图一条提示（<c>NIntent.OnHovered</c>）：
/// <list type="bullet">
/// <item>HoverTip：标题与图标保留显示意图的原版类型（基础提示由原版 <c>GetHoverTip</c> 按显示意图生成），
/// 描述换成源意图的描述——模组的 descriptionKey 写清了整个招式，具体内容放在描述里正是原版的做法。</item>
/// <item>IntentHovered：源意图带能力、卡牌、召唤等附加提示时，主提示之后附上这些提示，接管原版悬停；没有附加提示时交给原版。
/// 敌方卡牌本身不进提示：卡牌照默认画法挂在意图上方，悬停卡牌即放大。</item>
/// </list>
/// 不是显示代理的意图一律跳过，交给后面的默认装饰器（与默认画法相同）。
/// </summary>
internal sealed class VanillaHoverIntentDecorator : IIntentDecorator
{
    public string Name => "VanillaHover";

    public IntentDecoratorOutcome Render(IntentRenderStage stage, ref IntentRenderContext context)
    {
        if (context.Intent is not { } intent
            || !VanillaIntentProxies.TryGetSource(intent, out VanillaIntentProxySource? source))
        {
            return IntentDecoratorOutcome.Skipped;
        }

        try
        {
            switch (stage)
            {
                case IntentRenderStage.HoverTip:
                    context.HoverTip = WithSourceDescription(context.HoverTip, source, context.Targets!, context.Owner!);
                    return IntentDecoratorOutcome.Handled;
                case IntentRenderStage.IntentHovered:
                    return ShowWithExtraTips(intent, source.Source, context.Targets!, context.Owner!);
                default:
                    return IntentDecoratorOutcome.Skipped;
            }
        }
        catch (Exception exception)
        {
            LorLog.PatchFailure("VanillaIntentHover." + stage, exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    private static HoverTip WithSourceDescription(
        HoverTip baseTip,
        VanillaIntentProxySource source,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        // 源意图的 GetHoverTip 同样经过本入口；源意图不是代理，本装饰器跳过，只取它的描述文本。
        string description = source.Source.GetHoverTip(targets, owner).Description;
        return new HoverTip(new LocString("intents", source.TitlePrefix + ".title"), description, baseTip.Icon)
        {
            // Id 保留显示意图的原版标题键：反击关键词提示按 “Title=intents.COUNTER_” 识别反击意图。
            Id = baseTip.Id,
            IsSmart = baseTip.IsSmart,
            IsDebuff = baseTip.IsDebuff,
            IsInstanced = baseTip.IsInstanced,
            ShouldOverrideTextOverflow = baseTip.ShouldOverrideTextOverflow
        };
    }

    private static IntentDecoratorOutcome ShowWithExtraTips(
        AbstractIntent intent,
        AbstractIntent source,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        List<IHoverTip> extraTips = CollectExtraTips(source, targets, owner);
        if (extraTips.Count == 0 || !intent.HasIntentTip)
        {
            return IntentDecoratorOutcome.Skipped;
        }

        List<IHoverTip> hoverTips = [intent.GetHoverTip(targets, owner), .. extraTips];
        CombatQueries.CreatureNodeOf(owner)?.ShowHoverTips(IHoverTip.RemoveDupes(hoverTips).ToList());
        return IntentDecoratorOutcome.Handled;
    }

    private static List<IHoverTip> CollectExtraTips(AbstractIntent source, IEnumerable<Creature> targets, Creature owner)
    {
        var tips = new List<IHoverTip>();
        tips.AddRange(BadgedIntentHoverTipFactory.GetExtraHoverTips(IntentEffectCollection.Get(source)));
        tips.AddRange(DetailedIntentHoverTipFactory.GetExtraHoverTips(source, targets, owner));
        return tips;
    }
}

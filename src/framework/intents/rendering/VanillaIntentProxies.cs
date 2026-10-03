using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents.rendering;

/// <summary>
/// 原版风格画法里一个显示意图的来源。
/// </summary>
/// <param name="Source">招式（或敌方卡牌计划）里的源意图，模型层对象，只读不改。</param>
/// <param name="SourceIndex">源意图在 <c>NextMove.Intents</c> 里的下标；不在招式里（运行时出牌计划展开的意图）时为 -1。</param>
/// <param name="IsPrimary">是否是源意图本身的那一个图标（攻击、格挡等），而不是从附带效果、徽记拆出来的图标。
/// 指示线只跟主图标走，拆出来的增益、减益图标不画线。</param>
/// <param name="TitlePrefix">显示意图的原版本地化前缀（<c>intents</c> 表的 <c>{前缀}.title</c>），悬停提示标题用它。</param>
internal sealed record VanillaIntentProxySource(
    AbstractIntent Source,
    int SourceIndex,
    bool IsPrimary,
    string TitlePrefix);

/// <summary>
/// 原版风格画法生成的显示意图登记表：显示意图只挂在本地 NIntent 节点上，不进 <c>MoveState.Intents</c>。
/// 悬停提示（<c>AbstractIntent.GetHoverTip</c> 后缀拿不到节点）、指示线、封印与和弦变暗都按这里查回源意图。
/// 显示意图有本模组的代理类，也有直接 new 出来的原版或反击意图类实例，所以用弱表登记而不是接口。
/// </summary>
internal static class VanillaIntentProxies
{
    private static readonly ConditionalWeakTable<AbstractIntent, VanillaIntentProxySource> Sources = new();

    internal static T Register<T>(T display, AbstractIntent source, int sourceIndex, bool isPrimary, string titlePrefix)
        where T : AbstractIntent
    {
        Sources.AddOrUpdate(display, new VanillaIntentProxySource(source, sourceIndex, isPrimary, titlePrefix));
        return display;
    }

    internal static bool TryGetSource(AbstractIntent intent, [NotNullWhen(true)] out VanillaIntentProxySource? source) =>
        Sources.TryGetValue(intent, out source);

    /// <summary>显示意图对应的源意图；不是代理时就是它自己（原样显示的原版意图与反击意图）。</summary>
    internal static AbstractIntent SourceOf(AbstractIntent intent) =>
        Sources.TryGetValue(intent, out VanillaIntentProxySource? source) ? source.Source : intent;
}

/// <summary>
/// 原版风格的攻击图标。原版 <c>NIntent.UpdateVisuals</c> 只在 <c>intent is AttackIntent</c> 时显示伤害标签，
/// 所以继承 <see cref="AttackIntent"/>；伤害、段数和标签全部委托给源意图，定向伤害、按本地玩家预览、分段伤害都照旧。
/// 动画与图标走原版 <see cref="AttackIntent"/> 的规则：按总伤害取 <c>attack_1..5</c> 与 intent_atlas 的攻击图标。
/// </summary>
internal sealed class VanillaAttackIntentProxy : AttackIntent
{
    private readonly Func<int> _repeats;
    private readonly Func<IEnumerable<Creature>, Creature, int> _totalDamage;
    private readonly Func<IEnumerable<Creature>, Creature, LocString> _label;

    private VanillaAttackIntentProxy(
        Func<decimal>? damageCalc,
        Func<int> repeats,
        Func<IEnumerable<Creature>, Creature, int> totalDamage,
        Func<IEnumerable<Creature>, Creature, LocString> label)
    {
        // 原版 GetSingleDamage 直接调用 DamageCalc，不能留空。
        DamageCalc = damageCalc ?? (static () => 0m);
        _repeats = repeats;
        _totalDamage = totalDamage;
        _label = label;
    }

    public override int Repeats => _repeats();

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) => _totalDamage(targets, owner);

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner) => _label(targets, owner);

    internal static VanillaAttackIntentProxy For(AbstractIntent source) => source switch
    {
        AttackIntent attack => new VanillaAttackIntentProxy(
            attack.DamageCalc,
            () => attack.Repeats,
            attack.GetTotalDamage,
            attack.GetIntentLabel),
        // 继承 AbstractIntent 的敌方卡牌攻击：原版不显示它的标签，换成攻击代理后数字才出来。
        EnemyCardIntent { HasDamage: true } card => new VanillaAttackIntentProxy(
            card.DisplayDamageCalc,
            () => card.Repeats,
            (targets, owner) => card.GetDisplaySingleDamage(targets, owner) * card.Repeats,
            card.GetIntentLabel),
        // 意图类型是攻击却没有伤害数值：只显示图标。
        _ => new VanillaAttackIntentProxy(
            null,
            static () => 0,
            static (_, _) => 0,
            static (_, _) => new LocString("intents", "FORMAT_EMPTY"))
    };
}

/// <summary>
/// 原版风格的塞状态牌图标。原版只在 <c>intent is StatusIntent</c> 时显示张数标签，所以继承 <see cref="StatusIntent"/>；
/// 标签委托给源意图（动态张数、分堆张数之和都由源意图决定），徽记拆出来的状态牌按徽记数值实时计算。
/// </summary>
internal sealed class VanillaStatusIntentProxy : StatusIntent
{
    private readonly Func<IEnumerable<Creature>, Creature, LocString> _label;

    private VanillaStatusIntentProxy(int initialCount, Func<IEnumerable<Creature>, Creature, LocString> label)
        : base(initialCount)
    {
        _label = label;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner) => _label(targets, owner);

    internal static VanillaStatusIntentProxy ForSource(AbstractIntent source) =>
        new(source is StatusIntent status ? status.CardCount : 0, source.GetIntentLabel);

    internal static VanillaStatusIntentProxy ForCount(Func<int> count) =>
        new(Math.Max(0, count()), (_, _) => CountLabel(count()));

    private static LocString CountLabel(int count)
    {
        LocString label = new("intents", "FORMAT_STATUS_CARD_COUNT");
        label.Add("CardCount", Math.Max(0, count));
        return label;
    }
}

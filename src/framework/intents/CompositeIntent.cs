using System;
using System.Linq;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents;

/// <summary>复合意图的主意图。反击类只在敌人被攻击时结算，图标与普通攻击、格挡是不同的组。</summary>
public enum CompositeIntentPrimary
{
    Attack,
    CounterAttack,
    Defend,
    CounterDefend,
    Magic
}

/// <summary>主意图附带的效果类别，对应复合图标上叠加的那一半。</summary>
public enum CompositeIntentSecondary
{
    Buff,
    Debuff,
    CardDebuff,
    Defend
}

/// <summary>意图指向谁。指定目标的意图另有指示线与目标头像，群体攻击不显示单体目标。</summary>
public enum CompositeIntentTargeting
{
    Default,
    SingleTarget,
    GroupAttack
}

/// <summary>
/// 复合意图的统一描述：一个主意图、若干附带效果、一种指向方式，以及复合图标组（<see cref="CombinedIntentAnimData"/> 的 kind）。
/// <para>
/// 现有的 Combined* 意图类各自是原版 <see cref="AttackIntent"/>、<see cref="DefendIntent"/> 或 <see cref="AbstractIntent"/> 的子类，
/// 原版和其他模组按这些基类判断攻击、格挡，所以不能换成同一个基类；它们改为实现 <see cref="ICompositeIntent"/>，
/// 把“这是哪一种组合”集中成这份数据。显示、结算与存档仍按原来的类型和接口进行，这份描述只读、不参与判定。
/// </para>
/// </summary>
public sealed class CompositeIntent
{
    public CompositeIntent(
        CompositeIntentPrimary primary,
        IReadOnlyList<CompositeIntentSecondary> secondaries,
        CompositeIntentTargeting targeting,
        string visualKind,
        IReadOnlyList<IntentBadge> badges)
    {
        Primary = primary;
        Secondaries = secondaries ?? throw new ArgumentNullException(nameof(secondaries));
        Targeting = targeting;
        VisualKind = visualKind ?? throw new ArgumentNullException(nameof(visualKind));
        Badges = badges ?? throw new ArgumentNullException(nameof(badges));
    }

    public CompositeIntentPrimary Primary { get; }

    public IReadOnlyList<CompositeIntentSecondary> Secondaries { get; }

    public CompositeIntentTargeting Targeting { get; }

    public string VisualKind { get; }

    /// <summary>构造意图时传入的徽记；群体攻击标记也在其中。</summary>
    public IReadOnlyList<IntentBadge> Badges { get; }

    /// <summary>
    /// 按复合图标组推导主意图与附带效果。
    /// 攻击 + 卡牌负面与攻击 + 负面共用 attack_debuff 图标组，只能靠意图类型区分，所以额外传入 <paramref name="intentType"/>。
    /// </summary>
    public static CompositeIntent FromVisualKind(
        string visualKind,
        IntentType intentType,
        CompositeIntentTargeting targeting,
        IReadOnlyList<IntentBadge> badges)
    {
        (CompositeIntentPrimary primary, CompositeIntentSecondary? secondary) = visualKind switch
        {
            CombinedIntentAnimData.AttackDebuff => (CompositeIntentPrimary.Attack,
                intentType == IntentType.CardDebuff ? CompositeIntentSecondary.CardDebuff : CompositeIntentSecondary.Debuff),
            CombinedIntentAnimData.AttackBuff => (CompositeIntentPrimary.Attack, CompositeIntentSecondary.Buff),
            CombinedIntentAnimData.AttackDefend => (CompositeIntentPrimary.Attack, CompositeIntentSecondary.Defend),
            CombinedIntentAnimData.CounterAttackDebuff => (CompositeIntentPrimary.CounterAttack, CompositeIntentSecondary.Debuff),
            CombinedIntentAnimData.CounterAttackBuff => (CompositeIntentPrimary.CounterAttack, CompositeIntentSecondary.Buff),
            CombinedIntentAnimData.CounterAttackDefend => (CompositeIntentPrimary.CounterAttack, CompositeIntentSecondary.Defend),
            CombinedIntentAnimData.DefendBuff => (CompositeIntentPrimary.Defend, CompositeIntentSecondary.Buff),
            CombinedIntentAnimData.DefendDebuff => (CompositeIntentPrimary.Defend, CompositeIntentSecondary.Debuff),
            CombinedIntentAnimData.CounterDefendBuff => (CompositeIntentPrimary.CounterDefend, CompositeIntentSecondary.Buff),
            CombinedIntentAnimData.CounterDefendDebuff => (CompositeIntentPrimary.CounterDefend, CompositeIntentSecondary.Debuff),
            CombinedIntentAnimData.Magic or CombinedIntentAnimData.MagicLarge => (CompositeIntentPrimary.Magic, (CompositeIntentSecondary?)null),
            _ => throw new ArgumentOutOfRangeException(nameof(visualKind), visualKind, "Unknown combined intent kind.")
        };

        return new CompositeIntent(
            primary,
            secondary.HasValue ? [secondary.Value] : Array.Empty<CompositeIntentSecondary>(),
            targeting,
            visualKind,
            badges);
    }

    /// <summary>指向方式：指定目标优先，其次看徽记里有没有群体攻击标记。</summary>
    public static CompositeIntentTargeting ResolveTargeting(AbstractIntent intent, IReadOnlyList<IntentBadge> badges)
    {
        if (intent is ITargetedIntentIndicator)
        {
            return CompositeIntentTargeting.SingleTarget;
        }

        return IntentEffectCollection.HasGroupAttack(badges)
            ? CompositeIntentTargeting.GroupAttack
            : CompositeIntentTargeting.Default;
    }

    public override string ToString() =>
        Primary
        + "+" + (Secondaries.Count == 0 ? "-" : string.Join("+", Secondaries))
        + "|" + Targeting
        + "|" + VisualKind
        + "|badges=" + Badges.Count;
}

/// <summary>带 <see cref="CompositeIntent"/> 描述的复合意图。</summary>
public interface ICompositeIntent
{
    CompositeIntent Composite { get; }
}

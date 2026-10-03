using System;
using System.Linq;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents.rendering;

/// <summary>一个显示意图与它的源意图。</summary>
internal readonly record struct VanillaIntentPart(AbstractIntent Display, AbstractIntent Source);

/// <summary>
/// 把本模组的意图映射成原版意图（原版风格画法，设置见 <see cref="IntentDisplayStyle.Vanilla"/>）。只生成显示用的新对象并登记来源，
/// 不改源意图、不改 <c>MoveState.Intents</c>：执行、反击结算、受伤预览、意图图都读模型层对象。
/// <list type="bullet">
/// <item>原版程序集里的意图类原样显示（强力减益随之保留）；单个反击意图（<see cref="ICounterIntent"/>）原样显示，保留金色反击图标。</item>
/// <item>复合意图（<see cref="ICompositeIntent"/>，或只实现了复合图标的意图按图标组推断）拆成主意图加附带效果，并排显示。</item>
/// <item>徽记按类别取原版意图并去重：能力减益 Debuff、能力增益 Buff、状态牌 Status（张数）、卡牌减益 CardDebuff、治疗 Heal、召唤 Summon；
/// 自定义徽记（群体攻击、晕眩角标、封印等）不出图标，说明留在悬停的附加提示里。</item>
/// <item>详细意图（<see cref="IDetailedIntentVisuals"/>）只保留原版主图标，状态牌保留张数标签。</item>
/// <item>其余意图按类型继承关系，再按 <see cref="AbstractIntent.IntentType"/> 取原版意图。</item>
/// </list>
/// 复合反击的两半都用金色反击意图类（选项 A），由反击装饰器照旧换成金色帧。
/// </summary>
internal static class VanillaIntentMapper
{
    // 复合图标组（CombinedIntentAnimData 的 kind）。只实现 ICombinedIntentHoverIcon、没有 ICompositeIntent 的意图
    // （群体攻击的 CombinedEffect、审判鸟、狼来了等）按悬停图标路径反查图标组，再推出主意图与附带效果。
    private static readonly string[] CombinedKinds =
    [
        CombinedIntentAnimData.AttackDebuff,
        CombinedIntentAnimData.AttackBuff,
        CombinedIntentAnimData.AttackDefend,
        CombinedIntentAnimData.CounterAttackDebuff,
        CombinedIntentAnimData.CounterAttackBuff,
        CombinedIntentAnimData.CounterAttackDefend,
        CombinedIntentAnimData.DefendBuff,
        CombinedIntentAnimData.DefendDebuff,
        CombinedIntentAnimData.CounterDefendBuff,
        CombinedIntentAnimData.CounterDefendDebuff,
        CombinedIntentAnimData.Magic,
        CombinedIntentAnimData.MagicLarge
    ];

    private enum Kind
    {
        Attack,
        Defend,
        Buff,
        Debuff,
        DebuffStrong,
        CardDebuff,
        Status,
        Heal,
        Summon,
        Stun,
        Sleep,
        Escape,
        Unknown,
        Hidden
    }

    /// <summary>
    /// 映射一组源意图。<paramref name="moveIntents"/> 是怪物当前招式的意图，只用来记录源下标。
    /// HiddenIntent 布局占位按默认画法同一规则去掉。
    /// </summary>
    internal static IReadOnlyList<VanillaIntentPart> Map(
        IReadOnlyList<AbstractIntent> sourceIntents,
        IReadOnlyList<AbstractIntent> moveIntents)
    {
        IReadOnlyList<AbstractIntent> layout = CombinedIntentDisplayPatch.RemoveHiddenLayoutPlaceholders(sourceIntents);
        var parts = new List<VanillaIntentPart>(layout.Count + 2);
        foreach (AbstractIntent source in layout)
        {
            MapOne(source, IndexOfReference(moveIntents, source), parts);
        }

        return parts;
    }

    /// <summary>映射结果与源列表逐项同一对象（全是原样显示的意图），节点不必重建。</summary>
    internal static bool IsIdentity(IReadOnlyList<VanillaIntentPart> parts, IReadOnlyList<AbstractIntent> sourceIntents)
    {
        if (parts.Count != sourceIntents.Count)
        {
            return false;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            if (!ReferenceEquals(parts[i].Display, sourceIntents[i]))
            {
                return false;
            }
        }

        return true;
    }

    internal static int IndexOfReference(IReadOnlyList<AbstractIntent> intents, AbstractIntent intent)
    {
        for (int i = 0; i < intents.Count; i++)
        {
            if (ReferenceEquals(intents[i], intent))
            {
                return i;
            }
        }

        return -1;
    }

    private static void MapOne(AbstractIntent source, int sourceIndex, List<VanillaIntentPart> parts)
    {
        if (source.GetType().Assembly == typeof(AbstractIntent).Assembly)
        {
            parts.Add(new VanillaIntentPart(source, source));
            return;
        }

        CompositeIntent? composite = (source as ICompositeIntent)?.Composite ?? InferComposite(source);
        if (composite == null && source is ICounterIntent)
        {
            // 单个反击意图本身就是原版图标的金色重绘，原样显示；反击装饰器照旧换金色帧。
            // 不是反击的闪避（DodgeIntent，蓝色图标）不在此列，按 DefendIntent 映射成原版格挡。
            parts.Add(new VanillaIntentPart(source, source));
            return;
        }

        bool counter = composite?.Primary is CompositeIntentPrimary.CounterAttack or CompositeIntentPrimary.CounterDefend;
        Kind main = composite != null ? MainKind(composite.Primary) : MainKind(source);
        var kinds = new List<Kind> { main };

        IReadOnlyList<IntentBadge> badges = source is IDetailedIntentVisuals
            ? Array.Empty<IntentBadge>()
            : IntentEffectCollection.Get(source).Where(static badge => badge.IsVisible).ToArray();
        List<(Kind Kind, IntentBadge Badge)> badgeKinds = badges
            .Select(static badge => (Kind: BadgeKind(badge), Badge: badge))
            .Where(static entry => entry.Kind.HasValue)
            .Select(static entry => (Kind: entry.Kind!.Value, entry.Badge))
            .ToList();

        if (composite != null)
        {
            foreach (CompositeIntentSecondary secondary in composite.Secondaries)
            {
                Kind kind = SecondaryKind(secondary);
                if (!IsCoveredByBadges(kind, badgeKinds))
                {
                    AddDistinct(kinds, kind);
                }
            }
        }

        foreach ((Kind kind, _) in badgeKinds)
        {
            AddDistinct(kinds, kind);
        }

        for (int i = 0; i < kinds.Count; i++)
        {
            bool isPrimary = i == 0;
            AbstractIntent display = CreateDisplay(kinds[i], isPrimary, counter, source, badgeKinds, out string titlePrefix);
            VanillaIntentProxies.Register(display, source, sourceIndex, isPrimary, titlePrefix);
            parts.Add(new VanillaIntentPart(display, source));
        }
    }

    private static CompositeIntent? InferComposite(AbstractIntent source)
    {
        if (source is not ICombinedIntentHoverIcon hoverIcon || string.IsNullOrEmpty(hoverIcon.HoverIconPath))
        {
            return null;
        }

        foreach (string kind in CombinedKinds)
        {
            if (string.Equals(CombinedIntentAnimData.GetHoverIconPath(kind), hoverIcon.HoverIconPath, StringComparison.Ordinal))
            {
                return CompositeIntent.FromVisualKind(
                    kind,
                    source.IntentType,
                    CompositeIntentTargeting.Default,
                    Array.Empty<IntentBadge>());
            }
        }

        return null;
    }

    private static Kind MainKind(CompositeIntentPrimary primary) => primary switch
    {
        CompositeIntentPrimary.Attack or CompositeIntentPrimary.CounterAttack => Kind.Attack,
        CompositeIntentPrimary.Defend or CompositeIntentPrimary.CounterDefend => Kind.Defend,
        _ => Kind.Unknown
    };

    private static Kind MainKind(AbstractIntent source)
    {
        if (source is AttackIntent)
        {
            return Kind.Attack;
        }

        if (source is StatusIntent)
        {
            return Kind.Status;
        }

        return source.IntentType switch
        {
            IntentType.Attack or IntentType.DeathBlow => Kind.Attack,
            IntentType.Defend => Kind.Defend,
            IntentType.Buff => Kind.Buff,
            IntentType.Debuff => Kind.Debuff,
            IntentType.DebuffStrong => Kind.DebuffStrong,
            IntentType.CardDebuff => Kind.CardDebuff,
            IntentType.StatusCard => Kind.Status,
            IntentType.Heal => Kind.Heal,
            IntentType.Summon => Kind.Summon,
            IntentType.Stun => Kind.Stun,
            IntentType.Sleep => Kind.Sleep,
            IntentType.Escape => Kind.Escape,
            IntentType.Hidden => Kind.Hidden,
            _ => Kind.Unknown
        };
    }

    private static Kind SecondaryKind(CompositeIntentSecondary secondary) => secondary switch
    {
        CompositeIntentSecondary.Buff => Kind.Buff,
        CompositeIntentSecondary.Debuff => Kind.Debuff,
        CompositeIntentSecondary.CardDebuff => Kind.CardDebuff,
        _ => Kind.Defend
    };

    /// <summary>徽记对应的原版意图类别；自定义徽记、没有增减益方向的能力徽记不出图标。</summary>
    private static Kind? BadgeKind(IntentBadge badge)
    {
        if (badge.HasPower)
        {
            return badge.Power.GetTypeForAmount(badge.Amount) switch
            {
                PowerType.Buff => Kind.Buff,
                PowerType.Debuff => Kind.Debuff,
                _ => null
            };
        }

        return badge.Kind switch
        {
            IntentBadgeKind.StatusCard => Kind.Status,
            IntentBadgeKind.Heal => Kind.Heal,
            IntentBadgeKind.Summon => Kind.Summon,
            // IntentBadge.CardDebuff<T> 是带卡牌的自定义徽记。
            IntentBadgeKind.Custom when badge.HasCard => Kind.CardDebuff,
            _ => null
        };
    }

    /// <summary>
    /// 复合意图的附带效果是对徽记的概括（默认画法把状态牌归为减益、治疗与召唤归为增益），
    /// 徽记已经给出更具体的原版类别时不再重复出概括的那一个。
    /// </summary>
    private static bool IsCoveredByBadges(Kind secondary, List<(Kind Kind, IntentBadge Badge)> badgeKinds) => secondary switch
    {
        Kind.Debuff => badgeKinds.Any(static entry => entry.Kind is Kind.Status or Kind.CardDebuff)
                       && badgeKinds.All(static entry => entry.Kind != Kind.Debuff),
        Kind.CardDebuff => badgeKinds.Any(static entry => entry.Kind == Kind.Status),
        Kind.Buff => badgeKinds.Any(static entry => entry.Kind is Kind.Heal or Kind.Summon)
                     && badgeKinds.All(static entry => entry.Kind != Kind.Buff),
        _ => false
    };

    private static void AddDistinct(List<Kind> kinds, Kind kind)
    {
        Kind category = Category(kind);
        if (kinds.All(existing => Category(existing) != category))
        {
            kinds.Add(kind);
        }
    }

    private static Kind Category(Kind kind) => kind == Kind.DebuffStrong ? Kind.Debuff : kind;

    private static AbstractIntent CreateDisplay(
        Kind kind,
        bool isPrimary,
        bool counter,
        AbstractIntent source,
        List<(Kind Kind, IntentBadge Badge)> badgeKinds,
        out string titlePrefix)
    {
        bool strong = isPrimary && source.IntentType == IntentType.DebuffStrong;
        switch (kind)
        {
            case Kind.Attack when counter && source is AttackIntent attack:
                titlePrefix = "COUNTER_ATTACK";
                return new CounterAttackIntent(attack.DamageCalc ?? (static () => 0m), () => attack.Repeats);
            case Kind.Attack:
                titlePrefix = "ATTACK";
                return VanillaAttackIntentProxy.For(source);
            case Kind.Status:
                if (isPrimary)
                {
                    titlePrefix = "STATUS";
                    return VanillaStatusIntentProxy.ForSource(source);
                }

                IntentBadge[] statusBadges = badgeKinds
                    .Where(static entry => entry.Kind == Kind.Status)
                    .Select(static entry => entry.Badge)
                    .ToArray();
                int StatusCount() => statusBadges.Sum(static badge => Math.Max(0, badge.Amount));
                if (counter)
                {
                    titlePrefix = "COUNTER_STATUS";
                    return new CounterStatusIntent(StatusCount());
                }

                titlePrefix = "STATUS";
                return VanillaStatusIntentProxy.ForCount(StatusCount);
            case Kind.Defend:
                titlePrefix = counter ? "COUNTER_DEFEND" : "DEFEND";
                return counter ? new CounterDefendIntent() : new DefendIntent();
            case Kind.Buff:
                titlePrefix = counter ? "COUNTER_BUFF" : "BUFF";
                return counter ? new CounterBuffIntent() : new BuffIntent();
            case Kind.Debuff or Kind.DebuffStrong:
                strong |= kind == Kind.DebuffStrong;
                titlePrefix = counter ? "COUNTER_DEBUFF" : "DEBUFF";
                return counter ? new CounterDebuffIntent(strong) : new DebuffIntent(strong);
            case Kind.CardDebuff:
                titlePrefix = counter ? "COUNTER_CARD_DEBUFF" : "CARD_DEBUFF";
                return counter ? new CounterCardDebuffIntent() : new CardDebuffIntent();
            case Kind.Summon:
                titlePrefix = counter ? "COUNTER_SUMMON" : "SUMMON";
                return counter ? new CounterSummonIntent() : new SummonIntent();
            case Kind.Heal:
                titlePrefix = "HEAL";
                return new HealIntent();
            case Kind.Stun:
                titlePrefix = "STUN";
                return new StunIntent();
            case Kind.Sleep:
                titlePrefix = "SLEEP";
                return new SleepIntent();
            case Kind.Escape:
                titlePrefix = "ESCAPE";
                return new EscapeIntent();
            case Kind.Hidden:
                titlePrefix = "HIDDEN";
                return new HiddenIntent();
            default:
                titlePrefix = "UNKNOWN";
                return new UnknownIntent();
        }
    }
}

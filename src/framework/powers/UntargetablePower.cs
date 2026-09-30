using MegaCrit.Sts2.Core.Entities.Powers;

namespace LibraryOfRuina.framework.powers;

/// <summary>
/// 通用的无法选中状态。施加和移除时机由所属玩法控制。
/// </summary>
public class UntargetablePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "UNTARGETABLE_POWER";

    public override string PackedIconPath => "res://images/powers/spider_bud_untargetable_power.png";

    public override string ResolvedBigIconPath => PackedIconPath;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldAllowHitting(Creature creature)
    {
        return ShouldAllowHitting(Owner, creature);
    }

    public override bool ShouldAllowTargeting(Creature target)
    {
        return ShouldAllowTargeting(Owner, target);
    }

    internal static bool ShouldAllowHitting(Creature owner, Creature creature)
    {
        return creature != owner;
    }

    internal static bool ShouldAllowTargeting(Creature owner, Creature target)
    {
        return target != owner;
    }
}

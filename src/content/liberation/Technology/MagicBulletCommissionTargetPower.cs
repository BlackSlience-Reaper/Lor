using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed class MagicBulletCommissionTargetPower :
    LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "MAGIC_BULLET_COMMISSION_TARGET_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerInstanceType InstanceType =>
        PowerInstanceType.InstancedPerApplier;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "DamagePercent",
            MagicBulletShooterPageRelic.CommissionDamagePercent),
        new StringVar("ApplierName")
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _ = cardSource;
        if (applier?.Player != null)
        {
            ((StringVar)DynamicVars["ApplierName"]).StringValue =
                applier.Player.Character.Title.GetFormattedText();
        }

        return Task.CompletedTask;
    }

#if STS2_0_111_0
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        // Library 伤害链同时调用普通与带类型接口；委托仅在普通接口乘算一次。
        return IsPlayerAttackOnOwner(target, amount, props, dealer, cardSource)
            ? 1m + MagicBulletShooterPageRelic.CommissionDamagePercent / 100m
            : 1m;
    }

    private bool IsPlayerAttackOnOwner(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return target == Owner
            && amount > 0m
            && dealer?.IsPlayer == true
            && dealer.Side != Owner.Side
            && cardSource?.Type == CardType.Attack
            && ValuePropCompat.IsCardOrMonsterMove(props);
    }
}

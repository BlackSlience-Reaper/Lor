using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

public sealed class LittleRedPreyPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "LITTLE_RED_PREY_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(LittleRedMercenaryPageRelic.PreyDamageBonus, ValueProp.Unpowered),
        new StringVar("ApplierName")
    ];

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Applier?.IsPlayer == true && Applier.Player != null)
        {
            ((StringVar)DynamicVars["ApplierName"]).StringValue = Applier.Player.Character.Title.GetFormattedText();
        }
        else if (Applier?.Monster != null)
        {
            ((StringVar)DynamicVars["ApplierName"]).StringValue = Applier.Monster.Title.GetFormattedText();
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostAfterOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || amount < 1m
            || dealer == null
            || Applier == null)
        {
            return amount;
        }

        if (dealer != Applier
            && (Applier.Player == null || dealer.PetOwner != Applier.Player))
        {
            return amount;
        }

        return amount + DynamicVars["Damage"].BaseValue;
    }

    public override Task AfterModifyingHpLostAfterOsty()
    {
        Flash();
        return Task.CompletedTask;
    }

    public static async Task ApplyMark(
        Creature applier,
        Creature target,
        CardModel? cardSource)
    {
        if (target.Side == applier.Side)
        {
            return;
        }

        var mutable = (LittleRedPreyPower)ModelDb.Power<LittleRedPreyPower>().ToMutable();
        mutable.Applier = applier;
        mutable.Target = target;
        await PowerCmdCompat.Apply(mutable, target, 1m, applier, cardSource);
    }

}

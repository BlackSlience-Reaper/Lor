using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.powers;

public sealed class LibraryOfRuinaPoisonFangPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "POISON_FANG_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || !ValuePropCompat.IsPoweredAttack(props)) return;
        if (result.UnblockedDamage <= 0) return;

        Flash();
        await PowerCmdCompat.Apply<PoisonPower>(target, Amount, Owner, null);
    }
}




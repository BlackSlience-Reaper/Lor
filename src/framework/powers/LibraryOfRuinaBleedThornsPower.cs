using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.powers;

public sealed class LibraryOfRuinaBleedThornsPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BLEED_THORNS_POWER";

    public override PowerType Type => PowerType.Buff;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.FromPower<LibraryBleedingPower>() };

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || dealer == null
            || dealer == Owner
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        Flash();
        decimal bleedAmount = Amount <= 0 ? 1m : Amount;
        await PowerCmdCompat.Apply<LibraryBleedingPower>(dealer, bleedAmount, Owner, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side)
        {
            await PowerCmd.Remove(this);
        }
    }
}



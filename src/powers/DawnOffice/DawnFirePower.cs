using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.DawnOffice;

public sealed class LibraryOfRuinaDawnFirePower : LibraryOfRuinaPowerModel
{
    private const int MaxHpLossPerTrigger = 2;

    protected override string LegacyPowerId => "DAWN_FIRE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MaxHpLoss", MaxHpLossPerTrigger)
    ];

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner ||
            !ValuePropCompat.IsPoweredAttack(props) ||
            result.UnblockedDamage <= 0 ||
            !target.HasPower<LibraryBurnPower>())
        {
            return;
        }

        Flash();
        await CreatureCmd.LoseMaxHp(choiceContext, target, MaxHpLossPerTrigger, isFromCard: false);
    }
}

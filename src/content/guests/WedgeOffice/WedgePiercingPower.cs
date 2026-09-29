using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.guests.WedgeOffice;

public sealed class WedgePiercingPower : LibraryOfRuinaPowerModel
{
    private const decimal PiercingDamage = 1m;

    protected override string LegacyPowerId => "WEDGE_PIERCING_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Damage", PiercingDamage)
    ];

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner || !target.IsPlayer || target.IsDead || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        Flash();
        await CreatureCmdCompat.Damage(
            choiceContext,
            target,
            PiercingDamage,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.SkipHurtAnim,
            dealer: Owner,
            cardSource: null);
    }
}

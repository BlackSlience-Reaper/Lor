using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.MusiciansOfBremen;

public sealed class LibraryOfRuinaImprovDrummingPower : LibraryOfRuinaPowerModel
{
    private const int RapidWearAmount = 2;
    private const int RapidWearTurns = 1;

    protected override string LegacyPowerId => "IMPROV_DRUMMING_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<LibraryVulnerablePower>("Vulnerable", RapidWearAmount),
        new DynamicVar("Turns", RapidWearTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryVulnerablePower>()
    ];

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner || !ValuePropCompat.IsPoweredAttack(props) || result.UnblockedDamage <= 0)
        {
            return;
        }

        Flash();
        await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
            new ThrowingPlayerChoiceContext(),
            target,
            RapidWearAmount,
            RapidWearTurns - 1,
            IsPermanent: false,
            Owner,
            null);
    }
}

using LibraryOfRuina.encounters.QueenOfHatred;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.QueenOfHatred;

public sealed class LibraryOfRuinaQueenInTheNameOfHatredPower : LibraryOfRuinaPowerModel
{
    private const int MarkedTargetCount = 1;
    private const int DamageReductionPercent = 90;
    private const decimal UnmarkedPlayerDamageMultiplier = 0.1m;

    protected override string LegacyPowerId => "QUEEN_IN_THE_NAME_OF_HATRED_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Targets", MarkedTargetCount),
        new DynamicVar("Weak", QueenOfHatredMarkHelper.WeakAmount),
        new DynamicVar("RapidWear", QueenOfHatredMarkHelper.PermanentRapidWearAmount),
        new DynamicVar("DamageReduction", DamageReductionPercent)
    ];

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner
            || dealer is not { IsPlayer: true }
            || dealer.GetPower<LibraryOfRuinaMarkPower>() != null)
        {
            return 1m;
        }

        return UnmarkedPlayerDamageMultiplier;
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaMarkPower>()
    ];
}

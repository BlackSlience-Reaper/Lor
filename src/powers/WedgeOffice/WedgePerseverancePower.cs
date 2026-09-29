using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.WedgeOffice;

public sealed class WedgePerseverancePower : LibraryOfRuinaPowerModel, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    private const int DefaultGuardAmount = 25;
    private const int EnduranceTurns = 1;

    private sealed class Data
    {
        public bool TriggeredThisCombat;
    }

    protected override string LegacyPowerId => "WEDGE_PERSEVERANCE_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", EnduranceTurns)
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m || target.IsDead)
        {
            return amount;
        }

        Data data = GetInternalData<Data>();
        if (data.TriggeredThisCombat || amount < target.CurrentHp)
        {
            return amount;
        }

        return 0m;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        Data data = GetInternalData<Data>();
        if (data.TriggeredThisCombat)
        {
            return;
        }

        data.TriggeredThisCombat = true;
        Flash();

        decimal guardAmount = Amount > 0 ? Amount : DefaultGuardAmount;
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            guardAmount,
            EnduranceTurns - 1,
            IsPermanent: false,
            applier: Owner,
            cardSource: null);
    }
}

using System.Threading.Tasks;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.relics.StandaloneRelics;

public sealed class FiveHundredYenBillRelic : YanamiRelicModel
{
    protected override string IconBaseName => "five_hundred_yen_bill_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar(400)
    ];

    public override async Task AfterObtained()
    {
        Flash();
        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner);
    }
}



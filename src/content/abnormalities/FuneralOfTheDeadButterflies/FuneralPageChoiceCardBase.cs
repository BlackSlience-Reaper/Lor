using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;

public abstract class FuneralPageChoiceCardBase : PageChoiceCard<FuneralOfTheDeadButterfliesPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..HoverTipFactory.FromEnchantment<ChainEnchantment>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.Static(StaticHoverTip.Stun)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(FuneralOfTheDeadButterfliesPageRelic.RestEnchantMaxSelect),
        new PowerVar<StrengthPower>(FuneralOfTheDeadButterfliesPageRelic.CoffinStrength),
        new PowerVar<DexterityPower>(FuneralOfTheDeadButterfliesPageRelic.CoffinDexterity),
        new DynamicVar("StunTurns", FuneralOfTheDeadButterfliesPageRelic.MourningStunTurns)
    ];
}

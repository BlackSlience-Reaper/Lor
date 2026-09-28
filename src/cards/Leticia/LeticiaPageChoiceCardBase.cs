using LibraryOfRuina.enchantments.Leticia;
using LibraryOfRuina.relics.Leticia;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.Leticia;

public abstract class LeticiaPageChoiceCardBase : PageChoiceCard<LeticiaPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..HoverTipFactory.FromEnchantment<LeticiaPartnerMarkEnchantment>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<FrailPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChoiceCount", LeticiaPageRelic.SurpriseGiftChoiceCount),
        new DynamicVar("PickCount", LeticiaPageRelic.SurpriseGiftPickCount),
        new CardsVar(LeticiaPageRelic.BuddyEnchantMaxSelect),
        new PowerVar<StrengthPower>(LeticiaPageRelic.PrankStatAmount),
        new PowerVar<DexterityPower>(LeticiaPageRelic.PrankStatAmount),
        new DynamicVar("TurnInterval", LeticiaPageRelic.PrankTurnInterval),
        new DynamicVar("Turns", LeticiaPageRelic.PrankDebuffDuration),
        new DynamicVar("CostReduction", LeticiaPartnerMarkEnchantment.PlayCostReduction),
        new DynamicVar("CostIncrease", LeticiaPartnerMarkEnchantment.HandEndCostIncrease)
    ];
}

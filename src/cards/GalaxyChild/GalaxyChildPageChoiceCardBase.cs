using LibraryOfRuina.enchantments.GalaxyChild;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.relics.GalaxyChild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.cards.GalaxyChild;

public abstract class GalaxyChildPageChoiceCardBase : PageChoiceCard<GalaxyChildPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..HoverTipFactory.FromEnchantment<PebbleMarkEnchantment>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(GalaxyChildPageRelic.PebbleEnchantMaxSelect),
        new HealVar(GalaxyChildPageRelic.PebbleHeal),
        new DynamicVar("ProofTurns", GalaxyChildPageRelic.ProofTurns),
        new DynamicVar("ProofHeal", GalaxyChildPageRelic.ProofHeal),
        new DynamicVar("RemoveCards", GalaxyChildPageRelic.TearsRemoveMaxSelect),
        new DynamicVar("PenaltyCombats", GalaxyChildPageRelic.TearsPenaltyCombats),
        new DynamicVar("DrawReduction", GalaxyChildPageRelic.TearsDrawReduction)
    ];
}

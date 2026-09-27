using LibraryOfRuina.enchantments.Leticia;
using LibraryOfRuina.relics.Leticia;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.Leticia;

public abstract class LeticiaPageChoiceCardBase : CardModel
{
    public const string SurpriseGiftChoiceId = "LETICIA_PAGE_SURPRISE_GIFT_CHOICE_CARD";
    public const string BuddyChoiceId = "LETICIA_PAGE_BUDDY_CHOICE_CARD";
    public const string MischiefChoiceId = "LETICIA_PAGE_MISCHIEF_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected LeticiaPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsLeticiaPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is SurpriseGiftChoiceId or BuddyChoiceId or MischiefChoiceId;
    }
}

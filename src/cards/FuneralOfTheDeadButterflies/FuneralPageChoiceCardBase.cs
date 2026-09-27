using LibraryOfRuina.enchantments.FuneralOfTheDeadButterflies;
using LibraryOfRuina.relics.FuneralOfTheDeadButterflies;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.FuneralOfTheDeadButterflies;

public abstract class FuneralPageChoiceCardBase : CardModel
{
    public const string RestChoiceId = "FUNERAL_REST_CHOICE_CARD";
    public const string CoffinChoiceId = "FUNERAL_COFFIN_CHOICE_CARD";
    public const string MourningChoiceId = "FUNERAL_MOURNING_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected FuneralPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsFuneralPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is RestChoiceId or CoffinChoiceId or MourningChoiceId;
    }
}

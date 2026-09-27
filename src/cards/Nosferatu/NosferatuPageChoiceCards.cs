using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.Nosferatu;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.Nosferatu;

public abstract class NosferatuPageChoiceCardBase : CardModel
{
    public const string HydrophobiaChoiceId = "NOSFERATU_HYDROPHOBIA_CHOICE_CARD";
    public const string VampirismChoiceId = "NOSFERATU_VAMPIRISM_CHOICE_CARD";
    public const string WineChoiceId = "NOSFERATU_WINE_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HydrophobiaBleed", NosferatuPageRelic.HydrophobiaBleed),
        new DynamicVar("DamageBonus", NosferatuPageRelic.VampirismDamageBonus),
        new HealVar("VampirismHeal", NosferatuPageRelic.VampirismHeal),
        new HealVar("WineHeal", NosferatuPageRelic.WineHeal)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected NosferatuPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsNosferatuPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is HydrophobiaChoiceId or VampirismChoiceId or WineChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class NosferatuHydrophobiaChoiceCard : NosferatuPageChoiceCardBase
{
    protected override string PortraitFileName => "nosferatu_hydrophobia_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NosferatuVampirismChoiceCard : NosferatuPageChoiceCardBase
{
    protected override string PortraitFileName => "nosferatu_vampirism_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NosferatuWineChoiceCard : NosferatuPageChoiceCardBase
{
    protected override string PortraitFileName => "nosferatu_wine_choice_card.png";
}

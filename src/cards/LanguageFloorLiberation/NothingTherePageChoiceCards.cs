using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.LanguageFloorLiberation;

public abstract class NothingTherePageChoiceCardBase : CardModel
{
    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "DamageMultiplier",
            NothingTherePageRelic.GoodbyeDamageMultiplier)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            $"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected NothingTherePageChoiceCardBase()
        : base(
            -1,
            CardType.Skill,
            CardRarity.Ancient,
            TargetType.None,
            shouldShowInCardLibrary: false)
    {
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class NothingThereGoodbyeChoiceCard :
    NothingTherePageChoiceCardBase
{
    protected override string PortraitFileName =>
        "nothing_there_goodbye_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NothingThereHelloChoiceCard :
    NothingTherePageChoiceCardBase
{
    protected override string PortraitFileName =>
        "nothing_there_hello_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NothingThereShellChoiceCard :
    NothingTherePageChoiceCardBase
{
    protected override string PortraitFileName =>
        "nothing_there_shell_choice_card.png";
}

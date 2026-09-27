using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.HeartOfAspiration;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HeartOfAspiration;

public abstract class HeartOfAspirationPageChoiceCardBase : CardModel
{
    public const string PulseChoiceId = "HEART_OF_ASPIRATION_PULSE_CHOICE_CARD";
    public const string AspirationChoiceId = "HEART_OF_ASPIRATION_ASPIRATION_CHOICE_CARD";
    public const string ViolentPulseChoiceId = "HEART_OF_ASPIRATION_VIOLENT_PULSE_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>(),
        HoverTipFactory.FromPower<LibraryProtectionPower>(),
        HoverTipFactory.FromPower<LibraryQuicknessPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", HeartOfAspirationPageRelic.PulseStrongStacks),
        new DynamicVar("HpLoss", HeartOfAspirationPageRelic.PulseHpLoss),
        new DynamicVar("MaxHpPercent", HeartOfAspirationPageRelic.AspirationMaxHpPercent),
        new DynamicVar("RightClickBuffs", HeartOfAspirationPageRelic.ViolentPulseBuffStacks),
        new DynamicVar("DeathTurns", HeartOfAspirationPageRelic.ViolentPulseDeathTurns)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected HeartOfAspirationPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsHeartOfAspirationPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is PulseChoiceId or AspirationChoiceId or ViolentPulseChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class HeartOfAspirationPulseChoiceCard : HeartOfAspirationPageChoiceCardBase
{
    protected override string PortraitFileName => "heart_of_aspiration_pulse_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class HeartOfAspirationAspirationChoiceCard : HeartOfAspirationPageChoiceCardBase
{
    protected override string PortraitFileName => "heart_of_aspiration_aspiration_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class HeartOfAspirationViolentPulseChoiceCard : HeartOfAspirationPageChoiceCardBase
{
    protected override string PortraitFileName => "heart_of_aspiration_violent_pulse_choice_card.png";
}

using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.BurrowingHeaven;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.BurrowingHeaven;

public abstract class BurrowingHeavenPageChoiceCardBase : CardModel
{
    public const string WitheringBloodWingsChoiceId = "BURROWING_HEAVEN_WITHERING_BLOOD_WINGS_CHOICE_CARD";
    public const string OthersGazeChoiceId = "BURROWING_HEAVEN_OTHERS_GAZE_CHOICE_CARD";
    public const string AttentionAndFocusChoiceId = "BURROWING_HEAVEN_ATTENTION_AND_FOCUS_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<DrawCardsNextTurnPower>(),
        HoverTipFactory.FromPower<EnergyNextTurnPower>(),
        HoverTipFactory.ForEnergy(this)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ReflectPercent", BurrowingHeavenPageRelic.WitheringBloodWingsReflectPercent),
        new DynamicVar("HandCards", BurrowingHeavenPageRelic.OthersGazeHandCards),
        new CardsVar("Draw", BurrowingHeavenPageRelic.OthersGazeDraw),
        new EnergyVar("EnergyRemaining", BurrowingHeavenPageRelic.OthersGazeEnergyRemaining),
        new EnergyVar("EnergyNextTurn", BurrowingHeavenPageRelic.OthersGazeEnergyNextTurn),
        new DynamicVar("DamageTakenPercent", BurrowingHeavenPageRelic.AttentionAndFocusDamageTakenPercent),
        new DynamicVar("DamageDealtPercent", BurrowingHeavenPageRelic.AttentionAndFocusDamageDealtPercent)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected BurrowingHeavenPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsBurrowingHeavenPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is WitheringBloodWingsChoiceId or OthersGazeChoiceId or AttentionAndFocusChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class BurrowingHeavenWitheringBloodWingsChoiceCard : BurrowingHeavenPageChoiceCardBase
{
    protected override string PortraitFileName => "burrowing_heaven_withering_blood_wings_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BurrowingHeavenOthersGazeChoiceCard : BurrowingHeavenPageChoiceCardBase
{
    protected override string PortraitFileName => "burrowing_heaven_others_gaze_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BurrowingHeavenAttentionAndFocusChoiceCard : BurrowingHeavenPageChoiceCardBase
{
    protected override string PortraitFileName => "burrowing_heaven_attention_and_focus_choice_card.png";
}

using LibraryOfRuina.helpers;
using LibraryOfRuina.powers.JudgementBird;
using LibraryOfRuina.relics.JudgementBird;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.JudgementBird;

public abstract class JudgementBirdPageChoiceCardBase : CardModel
{
    public const string WeightOfSinChoiceId =
        "JUDGEMENT_BIRD_WEIGHT_OF_SIN_CHOICE_CARD";
    public const string JudgementChoiceId =
        "JUDGEMENT_BIRD_JUDGEMENT_CHOICE_CARD";
    public const string TiltedScaleChoiceId =
        "JUDGEMENT_BIRD_TILTED_SCALE_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            $"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "SelfDamage",
            JudgementBirdPageRelic.WeightOfSinSelfDamage),
        new DynamicVar(
            "Strong",
            JudgementBirdPageRelic.WeightOfSinStrong),
        new DynamicVar(
            "Turns",
            JudgementBirdPageRelic.WeightOfSinStrongTurns),
        new DynamicVar(
            "SinMultiplier",
            JudgementBirdPageRelic.JudgementSinMultiplier),
        new DynamicVar(
            "SinLossPercent",
            JudgementBirdPageRelic.JudgementSinLossPercent),
        new DynamicVar(
            "Heal",
            JudgementBirdPageRelic.TiltedScaleHeal),
        new DynamicVar(
            "MaxTriggers",
            JudgementBirdPageRelic.TiltedScaleMaxTriggers)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<JudgementBirdSinPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected JudgementBirdPageChoiceCardBase()
        : base(
            -1,
            CardType.Skill,
            CardRarity.Ancient,
            TargetType.None,
            shouldShowInCardLibrary: false)
    {
    }

    public static bool IsJudgementBirdPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is WeightOfSinChoiceId
            or JudgementChoiceId
            or TiltedScaleChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class JudgementBirdWeightOfSinChoiceCard :
    JudgementBirdPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "judgement_bird_weight_of_sin_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class JudgementBirdJudgementChoiceCard :
    JudgementBirdPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "judgement_bird_judgement_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class JudgementBirdTiltedScaleChoiceCard :
    JudgementBirdPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "judgement_bird_tilted_scale_choice_card.png";
}

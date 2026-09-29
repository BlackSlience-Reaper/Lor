using LibraryOfRuina.helpers;
using LibraryOfRuina.powers.JudgementBird;
using LibraryOfRuina.relics.JudgementBird;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.JudgementBird;

public abstract class JudgementBirdPageChoiceCardBase : PageChoiceCard<JudgementBirdPageMode>
{
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
}

[CardPool(typeof(TokenCardPool))]
public sealed class JudgementBirdWeightOfSinChoiceCard :
    JudgementBirdPageChoiceCardBase
{
    public override JudgementBirdPageMode PageMode => JudgementBirdPageMode.WeightOfSin;

    protected override string PortraitFileName =>
        "judgement_bird_weight_of_sin_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class JudgementBirdJudgementChoiceCard :
    JudgementBirdPageChoiceCardBase
{
    public override JudgementBirdPageMode PageMode => JudgementBirdPageMode.Judgement;

    protected override string PortraitFileName =>
        "judgement_bird_judgement_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class JudgementBirdTiltedScaleChoiceCard :
    JudgementBirdPageChoiceCardBase
{
    public override JudgementBirdPageMode PageMode => JudgementBirdPageMode.TiltedScale;

    protected override string PortraitFileName =>
        "judgement_bird_tilted_scale_choice_card.png";
}

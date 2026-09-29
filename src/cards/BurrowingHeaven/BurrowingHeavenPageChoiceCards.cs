using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.BurrowingHeaven;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.BurrowingHeaven;

public abstract class BurrowingHeavenPageChoiceCardBase : PageChoiceCard<BurrowingHeavenPageMode>
{
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
}

[CardPool(typeof(TokenCardPool))]
public sealed class BurrowingHeavenWitheringBloodWingsChoiceCard : BurrowingHeavenPageChoiceCardBase
{
    public override BurrowingHeavenPageMode PageMode => BurrowingHeavenPageMode.WitheringBloodWings;

    protected override string PortraitFileName => "burrowing_heaven_withering_blood_wings_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BurrowingHeavenOthersGazeChoiceCard : BurrowingHeavenPageChoiceCardBase
{
    public override BurrowingHeavenPageMode PageMode => BurrowingHeavenPageMode.OthersGaze;

    protected override string PortraitFileName => "burrowing_heaven_others_gaze_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BurrowingHeavenAttentionAndFocusChoiceCard : BurrowingHeavenPageChoiceCardBase
{
    public override BurrowingHeavenPageMode PageMode => BurrowingHeavenPageMode.AttentionAndFocus;

    protected override string PortraitFileName => "burrowing_heaven_attention_and_focus_choice_card.png";
}

using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.HeartOfAspiration;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HeartOfAspiration;

public abstract class HeartOfAspirationPageChoiceCardBase : PageChoiceCard<HeartOfAspirationPageMode>
{
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
}

[CardPool(typeof(TokenCardPool))]
public sealed class HeartOfAspirationPulseChoiceCard : HeartOfAspirationPageChoiceCardBase
{
    public override HeartOfAspirationPageMode PageMode => HeartOfAspirationPageMode.Pulse;

    protected override string PortraitFileName => "heart_of_aspiration_pulse_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class HeartOfAspirationAspirationChoiceCard : HeartOfAspirationPageChoiceCardBase
{
    public override HeartOfAspirationPageMode PageMode => HeartOfAspirationPageMode.Aspiration;

    protected override string PortraitFileName => "heart_of_aspiration_aspiration_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class HeartOfAspirationViolentPulseChoiceCard : HeartOfAspirationPageChoiceCardBase
{
    public override HeartOfAspirationPageMode PageMode => HeartOfAspirationPageMode.ViolentPulse;

    protected override string PortraitFileName => "heart_of_aspiration_violent_pulse_choice_card.png";
}

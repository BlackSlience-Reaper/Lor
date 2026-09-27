using LibraryLib.Powers;
using LibraryOfRuina.powers.QueenOfHatred;
using LibraryOfRuina.relics.NaturalFloorLiberation;
using LibraryOfRuina.relics.QueenOfHatred;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.QueenOfHatred;

public abstract class QueenOfHatredPageChoiceCardBase : CardModel
{
    public const string PhilanthropyChoiceId = "QUEEN_OF_HATRED_PHILANTHROPY_CHOICE_CARD";
    public const string JusticeChoiceId = "QUEEN_OF_HATRED_JUSTICE_CHOICE_CARD";
    public const string HatredChoiceId = "QUEEN_OF_HATRED_HATRED_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 1;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        IsUpgraded
            ? HoverTipFactory.FromPower<LibraryOfRuina.powers.NaturalFloorLiberation.NihilBadGuyPower>()
            : HoverTipFactory.FromPower<LibraryOfRuinaQueenBadGuyPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Enhanced", 0),
        new HealVar(QueenOfHatredPageRelic.PhilanthropyHealAmount),
        new DynamicVar("PhilanthropyTriggers", QueenOfHatredPageRelic.PhilanthropyTriggersPerTurn),
        new DynamicVar("Targets", QueenOfHatredPageRelic.JusticeTargetCount),
        new DynamicVar("DamageIncrease", QueenOfHatredPageRelic.JusticeDamageIncreasePercent),
        new DynamicVar("HpLoss", QueenOfHatredPageRelic.HatredHpLossBonus),
        new PowerVar<LibraryStrongPower>("Strong", QueenOfHatredPageRelic.HatredStrongGain),
        new DynamicVar("StrongTurns", QueenOfHatredPageRelic.HatredStrongTurns),
        new DynamicVar("HatredTriggers", QueenOfHatredPageRelic.HatredTriggersPerTurn)
    ];

    protected override void OnUpgrade()
    {
        DynamicVars["Enhanced"].UpgradeValueBy(
            1);
        DynamicVars["Heal"].UpgradeValueBy(
            QueenOfHatredEnhancedPageRelic.PhilanthropyHeal - DynamicVars["Heal"].BaseValue);
        DynamicVars["Targets"].UpgradeValueBy(
            QueenOfHatredEnhancedPageRelic.JusticeTargets - DynamicVars["Targets"].BaseValue);
        DynamicVars["DamageIncrease"].UpgradeValueBy(
            QueenOfHatredEnhancedPageRelic.JusticeDamagePercent - DynamicVars["DamageIncrease"].BaseValue);
        DynamicVars["Strong"].UpgradeValueBy(
            QueenOfHatredEnhancedPageRelic.HatredStrong - DynamicVars["Strong"].BaseValue);
        DynamicVars["StrongTurns"].UpgradeValueBy(
            QueenOfHatredEnhancedPageRelic.HatredTurns - DynamicVars["StrongTurns"].BaseValue);
    }

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected QueenOfHatredPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsQueenOfHatredPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is PhilanthropyChoiceId or JusticeChoiceId or HatredChoiceId;
    }
}

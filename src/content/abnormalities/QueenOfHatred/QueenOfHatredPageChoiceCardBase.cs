using LibraryLib.Powers;
using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

public abstract class QueenOfHatredPageChoiceCardBase : PageChoiceCard<QueenOfHatredPageMode>
{
    public override int MaxUpgradeLevel => 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        IsUpgraded
            ? HoverTipFactory.FromPower<LibraryOfRuina.content.liberation.Natural.NihilBadGuyPower>()
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
}

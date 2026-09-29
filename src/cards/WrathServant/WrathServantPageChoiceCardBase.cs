using LibraryOfRuina.powers.WrathServant;
using LibraryOfRuina.relics.NaturalFloorLiberation;
using LibraryOfRuina.relics.WrathServant;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.cards.WrathServant;

public abstract class WrathServantPageChoiceCardBase : PageChoiceCard<WrathServantPageMode>
{
    public override int MaxUpgradeLevel => 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        IsUpgraded
            ? HoverTipFactory.FromPower<LibraryOfRuina.powers.NaturalFloorLiberation.NihilFriendPower>()
            : HoverTipFactory.FromPower<WrathServantFriendPower>(),
        HoverTipFactory.FromPower<WrathServantCorrosionPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Enhanced", 0),
        new DynamicVar("FriendCards", WrathServantEnhancedPageRelic.FriendCards),
        new EnergyVar(WrathServantPageRelic.WrathEnergy),
        new CardsVar(WrathServantPageRelic.WrathCards),
        new PowerVar<LibraryStrongPower>("Strong", WrathServantPageRelic.WrathStrong),
        new DynamicVar("Turns", WrathServantPageRelic.WrathTurns),
        new DynamicVar("SelfTargetTurns", WrathServantPageRelic.WrathSelfTargetTurns),
        new EnergyVar("FriendEnergy", WrathServantPageRelic.FriendEnergy),
        new DynamicVar("Corrosion", WrathServantPageRelic.VenomCorrosion)
    ];

    protected override void OnUpgrade()
    {
        DynamicVars["Enhanced"].UpgradeValueBy(
            1);
        DynamicVars["Energy"].UpgradeValueBy(
            WrathServantEnhancedPageRelic.WrathEnergy - DynamicVars["Energy"].BaseValue);
        DynamicVars["Cards"].UpgradeValueBy(
            WrathServantEnhancedPageRelic.WrathCards - DynamicVars["Cards"].BaseValue);
        DynamicVars["Strong"].UpgradeValueBy(
            WrathServantEnhancedPageRelic.WrathStrong - DynamicVars["Strong"].BaseValue);
        DynamicVars["SelfTargetTurns"].UpgradeValueBy(
            WrathServantEnhancedPageRelic.WrathSelfTargetTurns - DynamicVars["SelfTargetTurns"].BaseValue);
        DynamicVars["FriendEnergy"].UpgradeValueBy(
            WrathServantEnhancedPageRelic.FriendEnergy - DynamicVars["FriendEnergy"].BaseValue);
        DynamicVars["Corrosion"].UpgradeValueBy(
            WrathServantEnhancedPageRelic.VenomCorrosion - DynamicVars["Corrosion"].BaseValue);
    }
}

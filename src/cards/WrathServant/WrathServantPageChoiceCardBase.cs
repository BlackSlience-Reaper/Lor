using LibraryOfRuina.powers.WrathServant;
using LibraryOfRuina.relics.NaturalFloorLiberation;
using LibraryOfRuina.relics.WrathServant;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.WrathServant;

public abstract class WrathServantPageChoiceCardBase : CardModel
{
    public const string WrathChoiceId = "WRATH_SERVANT_WRATH_CHOICE_CARD";
    public const string FriendChoiceId = "WRATH_SERVANT_FRIEND_CHOICE_CARD";
    public const string VenomChoiceId = "WRATH_SERVANT_VENOM_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 1;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected WrathServantPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsWrathServantPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is WrathChoiceId or FriendChoiceId or VenomChoiceId;
    }
}

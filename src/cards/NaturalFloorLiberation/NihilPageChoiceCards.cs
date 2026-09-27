using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.NaturalFloorLiberation;

public abstract class NihilPageChoiceCardBase() : CardModel(
    -1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
{
    protected abstract string PortraitName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        $"res://images/packed/card_portraits/colorless/{PortraitName}.png";

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Weak", NihilPageRelic.NihilityWeak),
        new DynamicVar("Disarm", NihilPageRelic.NihilityDisarm),
        new DynamicVar("Vulnerable", NihilPageRelic.NihilityVulnerable),
        new DynamicVar("BreakVulnerable", NihilPageRelic.NihilityBreakVulnerable),
        new DynamicVar("Turns", NihilPageRelic.NihilityTurns),
        new DynamicVar("TurnLimit", NihilPageRelic.NihilityTurnLimit),
        new DynamicVar("CombatLimit", NihilPageRelic.NihilityCombatLimit)
    ];
}

[CardPool(typeof(TokenCardPool))]
public sealed class NihilMagicalGirlsChoiceCard : NihilPageChoiceCardBase
{
    protected override string PortraitName => "nihil_magical_girls";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NihilEmptinessChoiceCard : NihilPageChoiceCardBase
{
    protected override string PortraitName => "nihil_emptiness";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<AllReturnsToVoidCard>()
    ];
}

[CardPool(typeof(TokenCardPool))]
public sealed class NihilNihilityChoiceCard : NihilPageChoiceCardBase
{
    protected override string PortraitName => "nihil_nihility";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        bool hasRequiredPages = IsMutable && NihilPageRelic.HasAllMagicalGirlPages(Owner);
        description.Add("HasRequiredPages", hasRequiredPages ? 1 : 0);
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryWeakPower>(),
        HoverTipFactory.FromPower<LibraryDisarmPower>(),
        HoverTipFactory.FromPower<LibraryVulnerablePower>(),
        HoverTipFactory.FromPower<LibraryBreakVulnerablePower>()
    ];
}

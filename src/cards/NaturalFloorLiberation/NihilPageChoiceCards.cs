using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.NaturalFloorLiberation;

public abstract class NihilPageChoiceCardBase : PageChoiceCard<NihilPageMode>
{
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
    public override NihilPageMode PageMode => NihilPageMode.MagicalGirls;

    protected override string PortraitFileName => "nihil_magical_girls.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class NihilEmptinessChoiceCard : NihilPageChoiceCardBase
{
    public override NihilPageMode PageMode => NihilPageMode.Emptiness;

    protected override string PortraitFileName => "nihil_emptiness.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<AllReturnsToVoidCard>()
    ];
}

[CardPool(typeof(TokenCardPool))]
public sealed class NihilNihilityChoiceCard : NihilPageChoiceCardBase
{
    public override NihilPageMode PageMode => NihilPageMode.Nihility;

    protected override string PortraitFileName => "nihil_nihility.png";

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

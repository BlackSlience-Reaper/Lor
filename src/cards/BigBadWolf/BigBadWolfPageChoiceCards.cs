using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.BigBadWolf;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.BigBadWolf;

public abstract class BigBadWolfPageChoiceCardBase : PageChoiceCard<BigBadWolfPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<DoubleDamagePower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryVulnerablePower>(),
        HoverTipFactory.FromPower<LibraryBreakVulnerablePower>(),
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TurnInterval", BigBadWolfPageRelic.PredatoryInstinctTurnInterval),
        new DynamicVar("DamageMultiplier", BigBadWolfPageRelic.PredatoryInstinctDamageMultiplier),
        new HealVar("Heal", BigBadWolfPageRelic.PredatoryInstinctHeal),
        new DynamicVar("WolfRoleStrong", BigBadWolfPageRelic.WolfRoleStrong),
        new DynamicVar("Vulnerable", BigBadWolfPageRelic.WolfRoleVulnerable),
        new DynamicVar("BreakVulnerable", BigBadWolfPageRelic.WolfRoleBreakVulnerable),
        new DynamicVar("MaxTriggers", BigBadWolfPageRelic.WolfRoleMaxTriggers),
        new DynamicVar("HpThresholdPercent", BigBadWolfPageRelic.CruelClawsHpThresholdPercent),
        new DynamicVar("Turns", BigBadWolfPageRelic.CruelClawsTurns),
        new DynamicVar("Strong", BigBadWolfPageRelic.CruelClawsStrong),
        new DynamicVar("Bleed", BigBadWolfPageRelic.CruelClawsBleed)
    ];
}

[CardPool(typeof(TokenCardPool))]
public sealed class BigBadWolfPredatoryInstinctChoiceCard : BigBadWolfPageChoiceCardBase
{
    public override BigBadWolfPageMode PageMode => BigBadWolfPageMode.PredatoryInstinct;

    protected override string PortraitFileName => "big_bad_wolf_predatory_instinct_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BigBadWolfWolfRoleChoiceCard : BigBadWolfPageChoiceCardBase
{
    public override BigBadWolfPageMode PageMode => BigBadWolfPageMode.WolfRole;

    protected override string PortraitFileName => "big_bad_wolf_wolf_role_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BigBadWolfCruelClawsChoiceCard : BigBadWolfPageChoiceCardBase
{
    public override BigBadWolfPageMode PageMode => BigBadWolfPageMode.CruelClaws;

    protected override string PortraitFileName => "big_bad_wolf_cruel_claws_choice_card.png";
}

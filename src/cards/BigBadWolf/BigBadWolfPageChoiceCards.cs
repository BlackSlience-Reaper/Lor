using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.BigBadWolf;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.BigBadWolf;

public abstract class BigBadWolfPageChoiceCardBase : CardModel
{
    public const string PredatoryInstinctChoiceId = "BIG_BAD_WOLF_PREDATORY_INSTINCT_CHOICE_CARD";
    public const string WolfRoleChoiceId = "BIG_BAD_WOLF_WOLF_ROLE_CHOICE_CARD";
    public const string CruelClawsChoiceId = "BIG_BAD_WOLF_CRUEL_CLAWS_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected BigBadWolfPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsBigBadWolfPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is PredatoryInstinctChoiceId or WolfRoleChoiceId or CruelClawsChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class BigBadWolfPredatoryInstinctChoiceCard : BigBadWolfPageChoiceCardBase
{
    protected override string PortraitFileName => "big_bad_wolf_predatory_instinct_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BigBadWolfWolfRoleChoiceCard : BigBadWolfPageChoiceCardBase
{
    protected override string PortraitFileName => "big_bad_wolf_wolf_role_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BigBadWolfCruelClawsChoiceCard : BigBadWolfPageChoiceCardBase
{
    protected override string PortraitFileName => "big_bad_wolf_cruel_claws_choice_card.png";
}

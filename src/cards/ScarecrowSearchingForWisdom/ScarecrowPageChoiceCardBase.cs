using LibraryOfRuina.powers.ScarecrowSearchingForWisdom;
using LibraryOfRuina.relics.ScarecrowSearchingForWisdom;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.ScarecrowSearchingForWisdom;

public abstract class ScarecrowPageChoiceCardBase : CardModel
{
    public const string RakeChoiceId = "SCARECROW_RAKE_CHOICE_CARD";
    public const string HarvestChoiceId = "SCARECROW_HARVEST_CHOICE_CARD";
    public const string TornWisdomChoiceId = "SCARECROW_TORN_WISDOM_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ScarecrowWisdomPower>(),
        ..HoverTipFactory.FromCardWithCardHoverTips<ScarecrowWisdomStatusCard>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Cards", ScarecrowPageRelic.RakeCardsToCopy),
        //new DynamicVar("CostIncrease", ScarecrowPageRelic.RakeCopiedCardCostIncrease),
        new DamageVar(ScarecrowPageRelic.HarvestDamageBonus, ValueProp.Unpowered),
        new DynamicVar("ChaoThresholdPercent", ScarecrowPageRelic.HarvestChaoThresholdPercent),
        new DynamicVar("PlayedCards", ScarecrowPageRelic.TornWisdomCardsPerTrigger),
        new EnergyVar(ScarecrowPageRelic.TornWisdomEnergyGain)
    ];

    protected ScarecrowPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsScarecrowPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is RakeChoiceId or HarvestChoiceId or TornWisdomChoiceId;
    }
}

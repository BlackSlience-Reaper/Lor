using LibraryOfRuina.relics.SpinyBus;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.SpinyBus;

public abstract class SpinyBusPageChoiceCardBase : CardModel
{
    public const string ThornsChoiceId = "SPINY_BUS_THORNS_CHOICE_CARD";
    public const string PleasureChoiceId = "SPINY_BUS_PLEASURE_CHOICE_CARD";
    public const string LaughingPowderChoiceId = "SPINY_BUS_LAUGHING_POWDER_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryDisarmPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Damage", SpinyBusPageRelic.ThornsDamageBonus),
        new DynamicVar("Flaw", SpinyBusPageRelic.PleasureFlawStacks),
        new DynamicVar("Strong", SpinyBusPageRelic.PleasureStrongStacks),
        new DynamicVar("Turns", SpinyBusPageRelic.PleasureTurns),
        new HealVar(SpinyBusPageRelic.LaughingPowderHeal)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected SpinyBusPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsSpinyBusPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is ThornsChoiceId or PleasureChoiceId or LaughingPowderChoiceId;
    }
}

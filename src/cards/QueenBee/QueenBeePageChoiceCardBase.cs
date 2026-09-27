using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.powers.QueenBee;
using LibraryOfRuina.relics.QueenBee;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.QueenBee;

public abstract class QueenBeePageChoiceCardBase : CardModel
{
    public const string SporeChoiceId = "QUEEN_BEE_SPORE_CHOICE_CARD";
    public const string WorkerBeeChoiceId = "QUEEN_BEE_WORKER_BEE_CHOICE_CARD";
    public const string LoyaltyChoiceId = "QUEEN_BEE_LOYALTY_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HistoryFloorWaspSporePower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryBleedingPower>(),
        HoverTipFactory.FromPower<LibraryBurnPower>(),
        HoverTipFactory.FromPower<QueenBeeThreatPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)QueenBeePageMode.None),
        new DynamicVar("SporeBurn", QueenBeePageRelic.SporeBurnAmount),
        new DynamicVar("SporeBleed", QueenBeePageRelic.SporeBleedAmount),
        new DynamicVar("ChaosDamage", QueenBeePageRelic.ThreatChaosDamage),
        new DynamicVar("HpLoss", QueenBeePageRelic.LoyaltyHpLossPerTrigger),
        new DynamicVar("Strength", QueenBeePageRelic.LoyaltyStrength),
        new DynamicVar("Turns", QueenBeePageRelic.LoyaltyTurns)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            $"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected QueenBeePageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsQueenBeePageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is SporeChoiceId or WorkerBeeChoiceId or LoyaltyChoiceId;
    }
}

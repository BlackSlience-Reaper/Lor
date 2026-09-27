using LibraryOfRuina.relics.ForsakenMurderer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.ForsakenMurderer;

public abstract class ForsakenMurdererPageChoiceCardBase : CardModel
{
    public const string IronEchoChoiceId = "FORSAKEN_MURDERER_IRON_ECHO_CHOICE_CARD";
    public const string BoundWrathChoiceId = "FORSAKEN_MURDERER_BOUND_WRATH_CHOICE_CARD";
    public const string ExtremeViolenceChoiceId = "FORSAKEN_MURDERER_EXTREME_VIOLENCE_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<LibraryVulnerablePower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StrengthLoss", ForsakenMurdererPageRelic.IronEchoStrengthLoss),
        new DamageVar(ForsakenMurdererPageRelic.BoundWrathDamage, ValueProp.Unpowered),
        new DynamicVar("Strength", ForsakenMurdererPageRelic.ExtremeViolenceStrength),
        new DynamicVar("RapidWear", ForsakenMurdererPageRelic.ExtremeViolenceRapidWear)
    ];

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected ForsakenMurdererPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsForsakenMurdererPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is IronEchoChoiceId or BoundWrathChoiceId or ExtremeViolenceChoiceId;
    }
}

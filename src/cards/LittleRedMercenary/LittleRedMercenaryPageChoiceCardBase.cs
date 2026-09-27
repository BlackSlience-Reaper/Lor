using LibraryOfRuina.powers.LittleRedMercenary;
using LibraryOfRuina.relics.LittleRedMercenary;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.LittleRedMercenary;

public abstract class LittleRedMercenaryPageChoiceCardBase : CardModel
{
    public const string ScarChoiceId = "LITTLE_RED_SCAR_CHOICE_CARD";
    public const string RevengeChoiceId = "LITTLE_RED_REVENGE_CHOICE_CARD";
    public const string PreyChoiceId = "LITTLE_RED_PREY_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<LittleRedPreyPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ScarHpHigh", LittleRedMercenaryPageRelic.ScarHpPercentHigh),
        new DynamicVar("ScarHpMid", LittleRedMercenaryPageRelic.ScarHpPercentMid),
        new DynamicVar("ScarHpLow", LittleRedMercenaryPageRelic.ScarHpPercentLow),
        new DynamicVar("ScarStrengthHigh", LittleRedMercenaryPageRelic.ScarStrengthBelowHigh),
        new DynamicVar("ScarStrengthMid", LittleRedMercenaryPageRelic.ScarStrengthBelowMid),
        new DynamicVar("ScarStrengthLow", LittleRedMercenaryPageRelic.ScarStrengthBelowLow),
        new DynamicVar("HpLoss", LittleRedMercenaryPageRelic.RevengeHpLossPerTrigger),
        new DynamicVar("Strength", LittleRedMercenaryPageRelic.RevengeStrengthPerTrigger),
        new DynamicVar("MaxTriggers", LittleRedMercenaryPageRelic.RevengeMaxTriggersPerCombat),
        new DamageVar(LittleRedMercenaryPageRelic.PreyDamageBonus, ValueProp.Move)
    ];

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected LittleRedMercenaryPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsLittleRedMercenaryPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is ScarChoiceId or RevengeChoiceId or PreyChoiceId;
    }
}

using LibraryOfRuina.relics.RedShoes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.RedShoes;

public abstract class RedShoesPageChoiceCardBase : CardModel
{
    public const string GlitterChoiceId = "RED_SHOES_GLITTER_CHOICE_CARD";
    public const string BloodThirstChoiceId = "RED_SHOES_BLOOD_THIRST_CHOICE_CARD";
    public const string AxeChoiceId = "RED_SHOES_AXE_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<LibraryStrongPower>("GlitterStrength", RedShoesPageRelic.GlitterStrength),
        new HpLossVar(RedShoesPageRelic.GlitterEndTurnHpLoss),
        new DynamicVar("ActiveRound", RedShoesPageRelic.BloodThirstActiveRound),
        new DynamicVar("DamageMultiplier", RedShoesPageRelic.BloodThirstDamageMultiplier),
        new PowerVar<LibraryStrongPower>("AxeStrength", RedShoesPageRelic.AxeStrength),
        new DynamicVar("AxeHpLoss", RedShoesPageRelic.AxeFailedBlockBreakHpLoss)
    ];

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected RedShoesPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsRedShoesPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is GlitterChoiceId or BloodThirstChoiceId or AxeChoiceId;
    }
}

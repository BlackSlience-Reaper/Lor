using LibraryOfRuina.relics.TodaysShyLook;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.TodaysShyLook;

public abstract class TodaysShyLookPageChoiceCardBase : CardModel
{
    public const string TodaysExpressionChoiceId = "TODAYS_SHY_LOOK_TODAYS_EXPRESSION_CHOICE_CARD";
    public const string ShynessChoiceId = "TODAYS_SHY_LOOK_SHYNESS_CHOICE_CARD";
    public const string SocialDistanceChoiceId = "TODAYS_SHY_LOOK_SOCIAL_DISTANCE_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StrengthMin", TodaysShyLookPageRelic.TodaysExpressionStrengthMin),
        new DynamicVar("StrengthMax", TodaysShyLookPageRelic.TodaysExpressionStrengthMax),
        new DynamicVar("DexterityMin", TodaysShyLookPageRelic.TodaysExpressionDexterityMin),
        new DynamicVar("DexterityMax", TodaysShyLookPageRelic.TodaysExpressionDexterityMax),
        new DynamicVar("DexterityFloor", TodaysShyLookPageRelic.TodaysExpressionDexterityFloor),
        new BlockVar(TodaysShyLookPageRelic.ShynessBlock, ValueProp.Unpowered),
        new DynamicVar("BlockPerSkill", TodaysShyLookPageRelic.SocialDistanceBlockPerSkill),
        new DynamicVar("MaxBlock", TodaysShyLookPageRelic.SocialDistanceMaxBlock)
    ];

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected TodaysShyLookPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsTodaysShyLookPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is TodaysExpressionChoiceId or ShynessChoiceId or SocialDistanceChoiceId;
    }
}

using LibraryOfRuina.relics.TodaysShyLook;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.TodaysShyLook;

public abstract class TodaysShyLookPageChoiceCardBase : PageChoiceCard<TodaysShyLookPageMode>
{
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
}

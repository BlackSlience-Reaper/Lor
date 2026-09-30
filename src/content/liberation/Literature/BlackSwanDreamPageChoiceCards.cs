using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.liberation.Literature;

public abstract class BlackSwanDreamPageChoiceCardBase : PageChoiceCard<BlackSwanDreamPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<SlipperyPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "DebuffMultiplier",
            BlackSwanDreamPageRelic.FilthDebuffMultiplier),
        new DynamicVar(
            "TurnInterval",
            BlackSwanDreamPageRelic.BrokenUmbrellaTurnInterval),
        new DynamicVar(
            "DamageReductionPercent",
            BlackSwanDreamPageRelic.BrokenUmbrellaDamageReductionPercent),
        new DynamicVar(
            "BlockMultiplier",
            BlackSwanDreamPageRelic.BrokenUmbrellaBlockMultiplier),
        new DynamicVar(
            "DearFamilyTurnInterval",
            BlackSwanDreamPageRelic.DearFamilyTurnInterval),
        new PowerVar<SlipperyPower>(
            "Slippery",
            BlackSwanDreamPageRelic.DearFamilySlippery)
    ];
}

[CardPool(typeof(TokenCardPool))]
public sealed class BlackSwanFilthChoiceCard :
    BlackSwanDreamPageChoiceCardBase
{
    public override BlackSwanDreamPageMode PageMode => BlackSwanDreamPageMode.Filth;

    protected override string PortraitFileName =>
        "black_swan_filth_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BlackSwanBrokenUmbrellaChoiceCard :
    BlackSwanDreamPageChoiceCardBase
{
    public override BlackSwanDreamPageMode PageMode => BlackSwanDreamPageMode.BrokenUmbrella;

    protected override string PortraitFileName =>
        "black_swan_broken_umbrella_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BlackSwanDearFamilyChoiceCard :
    BlackSwanDreamPageChoiceCardBase
{
    public override BlackSwanDreamPageMode PageMode => BlackSwanDreamPageMode.DearFamily;

    protected override string PortraitFileName =>
        "black_swan_dear_family_choice_card.png";
}

using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.abnormalities.RedShoes;

public abstract class RedShoesPageChoiceCardBase : PageChoiceCard<RedShoesPageMode>
{
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
}

using LibraryOfRuina.relics.FairyFestival;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.FairyFestival;

public abstract class FairyFestivalPageChoiceCardBase : PageChoiceCard<FairyFestivalPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryVulnerablePower>(),
        HoverTipFactory.FromPower<RegenPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(FairyFestivalPageRelic.FairyCareHeal),
        new DynamicVar("PermanentVulnerable", FairyFestivalPageRelic.FairyCarePermanentVulnerable),
        new DynamicVar("Regen", FairyFestivalPageRelic.GluttonyRegen),
        new HpLossVar(FairyFestivalPageRelic.PredationHpLoss),
        new PowerVar<StrengthPower>(FairyFestivalPageRelic.PredationStrength),
        new PowerVar<DexterityPower>(FairyFestivalPageRelic.PredationDexterity)
    ];
}

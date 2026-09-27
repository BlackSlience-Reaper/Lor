using LibraryOfRuina.relics.FairyFestival;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.FairyFestival;

public abstract class FairyFestivalPageChoiceCardBase : CardModel
{
    public const string FairyCareChoiceId = "FAIRY_CARE_CHOICE_CARD";
    public const string GluttonyChoiceId = "FAIRY_GLUTTONY_CHOICE_CARD";
    public const string PredationChoiceId = "FAIRY_PREDATION_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected FairyFestivalPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }
}

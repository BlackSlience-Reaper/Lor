using LibraryOfRuina.relics.CosmicFragment;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.CosmicFragment;

public abstract class CosmicFragmentPageChoiceCardBase : CardModel
{
    public const string OtherworldlyEchoChoiceId = "COSMIC_FRAGMENT_OTHERWORLDLY_ECHO_CHOICE_CARD";
    public const string TentacleChoiceId = "COSMIC_FRAGMENT_TENTACLE_CHOICE_CARD";
    public const string IncomprehensibleChoiceId = "COSMIC_FRAGMENT_INCOMPREHENSIBLE_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChaosLoss", CosmicFragmentPageRelic.OtherworldlyEchoChaosLoss),
        new HealVar(CosmicFragmentPageRelic.OtherworldlyEchoHeal),
        new DynamicVar("ChaosPercent", CosmicFragmentPageRelic.TentacleChaosPercent),
        new DynamicVar("ChaosStepPercent", CosmicFragmentPageRelic.IncomprehensibleChaosStepPercent),
        new DynamicVar("ResetAttacks", CosmicFragmentPageRelic.IncomprehensibleResetAttacks)
    ];

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected CosmicFragmentPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsCosmicFragmentPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is OtherworldlyEchoChoiceId or TentacleChoiceId or IncomprehensibleChoiceId;
    }
}

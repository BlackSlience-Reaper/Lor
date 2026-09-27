using LibraryOfRuina.enchantments.GalaxyChild;
using LibraryOfRuina.relics.GalaxyChild;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.GalaxyChild;

public abstract class GalaxyChildPageChoiceCardBase : CardModel
{
    public const string PebbleChoiceId = "GALAXY_CHILD_PEBBLE_CHOICE_CARD";
    public const string ProofOfFriendshipChoiceId = "GALAXY_CHILD_PROOF_OF_FRIENDSHIP_CHOICE_CARD";
    public const string TearsChoiceId = "GALAXY_CHILD_TEARS_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..HoverTipFactory.FromEnchantment<PebbleMarkEnchantment>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(GalaxyChildPageRelic.PebbleEnchantMaxSelect),
        new HealVar(GalaxyChildPageRelic.PebbleHeal),
        new DynamicVar("ProofTurns", GalaxyChildPageRelic.ProofTurns),
        new DynamicVar("ProofHeal", GalaxyChildPageRelic.ProofHeal),
        new DynamicVar("RemoveCards", GalaxyChildPageRelic.TearsRemoveMaxSelect),
        new DynamicVar("PenaltyCombats", GalaxyChildPageRelic.TearsPenaltyCombats),
        new DynamicVar("DrawReduction", GalaxyChildPageRelic.TearsDrawReduction)
    ];

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected GalaxyChildPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsGalaxyChildPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is PebbleChoiceId or ProofOfFriendshipChoiceId or TearsChoiceId;
    }
}

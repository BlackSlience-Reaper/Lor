using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.PunishingBird;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.cards.PunishingBird;

[CardPool(typeof(StatusCardPool))]
public sealed class ForestKeeperLockStatusCard : CardModel
{
    public const int ResetCost = 6;
    
    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/status/forest_keeper_lock_status_card.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    private static Creature? FindPunishingBird(Player? player)
    {
        return player?.Creature.CombatState?.Enemies.FirstOrDefault(
            static creature =>
                creature.IsAlive
                && creature.Monster is monsters.PunishingBird.PunishingBird);
    }

    private bool Flag
    {
        get
        {
            string? moveId = FindPunishingBird(Owner)?.Monster?.NextMove?.StateId;
            return moveId is
                monsters.PunishingBird.PunishingBird.PeckOneThenPunishMoveId
                or monsters.PunishingBird.PunishingBird.PeckTwoThenPunishMoveId;
        }
    }

    protected override bool ShouldGlowGoldInternal => Flag;

    protected override bool ShouldGlowRedInternal => !Flag;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar("ResetCost", ResetCost)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    public ForestKeeperLockStatusCard()
        : base(ResetCost, CardType.Status, CardRarity.Status, TargetType.AnyEnemy)
    {
    }

    internal static bool IsLockTarget(Creature? target) =>
        target is { IsAlive: true, Monster: monsters.PunishingBird.PunishingBird };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target?.Monster is monsters.PunishingBird.PunishingBird bird)
        {
            await bird.TryBreakCageChain(choiceContext, this);
        }

        EnergyCost.SetThisCombat(ResetCost);
        InvokeEnergyCostChanged();
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is ForestKeeperLockStatusCard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}

public abstract class PunishingBirdPageChoiceCardBase : CardModel
{
    public const string PunishmentChoiceId = "PUNISHING_BIRD_PUNISHMENT_CHOICE_CARD";
    public const string PunitiveBeakChoiceId = "PUNISHING_BIRD_PUNITIVE_BEAK_CHOICE_CARD";
    public const string FlutteringWingsChoiceId = "PUNISHING_BIRD_FLUTTERING_WINGS_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.ForEnergy(this)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Multiplier", PunishingBirdPageRelic.PunishmentMultiplier),
        new DynamicVar("Strength", PunishingBirdPageRelic.BeakStrength),
        new DynamicVar("Dexterity", PunishingBirdPageRelic.BeakDexterity),
        new EnergyVar("EnergyLoss", PunishingBirdPageRelic.WingsEnergyGain)
    ];

    protected PunishingBirdPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsPunishingBirdPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is PunishmentChoiceId or PunitiveBeakChoiceId or FlutteringWingsChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class PunishingBirdPunishmentChoiceCard : PunishingBirdPageChoiceCardBase
{
    protected override string PortraitFileName => "punishing_bird_punishment_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class PunishingBirdPunitiveBeakChoiceCard : PunishingBirdPageChoiceCardBase
{
    protected override string PortraitFileName => "punishing_bird_punitive_beak_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class PunishingBirdFlutteringWingsChoiceCard : PunishingBirdPageChoiceCardBase
{
    protected override string PortraitFileName => "punishing_bird_fluttering_wings_choice_card.png";
}

using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.BigBird;
using LibraryOfRuina.helpers;
using LibraryOfRuina.powers.BigBird;
using LibraryOfRuina.relics.BigBird;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.BigBird;

[CardPool(typeof(StatusCardPool))]
public sealed class SoulSnareStatusCard() : CardModel(3, CardType.Status, CardRarity.Status, TargetType.AnyEnemy)
{
    public const int Damage = 1;

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/status/soul_snare_status_card.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Damage", Damage)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    // public override bool ShouldAllowTargeting(Creature target)
    // {
    //     if (Owner?.Creature?.GetPower<BigBirdCharmedPower>() == null)
    //     {
    //         return true;
    //     }
    //     return target.Monster is BigBird;
    // }
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? boss = BigBirdEncounterHelper.FindBoss(Owner.Creature.CombatState);
        if (boss == null)
        {
            return;
        }

        await CreatureCmdCompat.Damage(
            choiceContext,
            boss,
            Damage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner.Creature,
            this,
            cardPlay);
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is SoulSnareStatusCard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}

[CardPool(typeof(StatusCardPool))]
public sealed class BirdLullabyCard() : CardModel(2, CardType.Skill, CardRarity.Status, TargetType.AnyEnemy)
{
    public const int Damage = 1;

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/status/bird_lullaby_card.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Damage", Damage),
        new EnergyVar("CostIncrease", 1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
        HoverTipFactory.FromPower<BigBirdSleepPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? target = cardPlay.Target;
        if (target == null)
        {
            return;
        }

        await CreatureCmdCompat.Damage(
            choiceContext,
            target,
            Damage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner.Creature,
            this,
            cardPlay);

        if (target.Monster is monsters.BigBird.BigBird bigBird)
        {
            if (target.Block > 0m)
            {
                await CreatureCmd.LoseBlock(choiceContext, target, target.Block, Owner.Creature);
            }

            await PowerCmdCompat.SetAmount<BigBirdSleepPower>(choiceContext, target, monsters.BigBird.BigBird.SleepTurns, Owner.Creature, this);
            await BigBirdEncounterHelper.ClearAllCharmedPlayers(choiceContext, target.CombatState);
            bigBird.ForceRefreshMoveState();
            // if (target is LibraryCreature libraryTarget && libraryTarget.HasChaoResistance)
            // {
            //     await LibraryCreatureCmd.SetCurrentChaoValue(libraryTarget, 0m);
            // }
        }

        EnergyCost.AddThisCombat(1);
        InvokeEnergyCostChanged();
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is BirdLullabyCard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}

public abstract class BigBirdPageChoiceCardBase() : CardModel(-1, CardType.Skill, CardRarity.Ancient, TargetType.None,
    shouldShowInCardLibrary: false)
{
    public const string WatchfulEyeChoiceId = "BIG_BIRD_WATCHFUL_EYE_CHOICE_CARD";
    public const string EverBurningLampChoiceId = "BIG_BIRD_EVER_BURNING_LAMP_CHOICE_CARD";
    public const string SalvationChoiceId = "BIG_BIRD_SALVATION_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar("Energy", BigBirdPageRelic.WatchfulEyeEnergyPenalty),
        new DynamicVar("WatchfulCooldown", BigBirdPageRelic.WatchfulEyeCooldown),
        new DynamicVar("LampCooldown", BigBirdPageRelic.EverBurningLampCooldown),
        new DynamicVar("Strong", BigBirdPageRelic.SalvationStrong),
        new DynamicVar("Damage", BigBirdPageRelic.SalvationDamage)
    ];

    public static bool IsBigBirdPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is WatchfulEyeChoiceId or EverBurningLampChoiceId or SalvationChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class BigBirdWatchfulEyeChoiceCard : BigBirdPageChoiceCardBase
{
    protected override string PortraitFileName => "big_bird_watchful_eye_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BigBirdEverBurningLampChoiceCard : BigBirdPageChoiceCardBase
{
    protected override string PortraitFileName => "big_bird_ever_burning_lamp_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class BigBirdSalvationChoiceCard : BigBirdPageChoiceCardBase
{
    protected override string PortraitFileName => "big_bird_salvation_choice_card.png";
}

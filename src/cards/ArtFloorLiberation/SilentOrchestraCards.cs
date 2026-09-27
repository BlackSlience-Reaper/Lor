using System;
using System.Threading.Tasks;
using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.ArtFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.cards.ArtFloorLiberation;

[CardPool(typeof(TokenCardPool))]
public sealed class EverRepeatingPerformanceCard() :
    CardModel(0, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    internal const int NextTurnEnergy = 4;
    internal const int NextTurnCards = 4;
    internal const string SharedPortraitPath =
        "res://images/packed/card_portraits/colorless/ever_repeating_performance.png";

    public override CardPoolModel VisualCardPool =>
        ModelDb.CardPool<ColorlessCardPool>();

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath => SharedPortraitPath;

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(NextTurnEnergy),
        new CardsVar(NextTurnCards)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StoredHp { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ReturnPending { get; private set; }

    public override Task BeforeCombatStart()
    {
        StoredHp = Math.Max(1, (int)Owner.Creature.CurrentHp);
        ReturnPending = false;
        return Task.CompletedTask;
    }

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        _ = choiceContext;
        _ = cardPlay;
        ReturnPending = true;
        PlayerCmd.EndTurn(Owner, canBackOut: false);
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner || !ReturnPending || StoredHp <= 0)
        {
            return;
        }

        int hp = Math.Max(
            Owner.Creature.CurrentHp,
            Math.Min(StoredHp, Owner.Creature.MaxHp));
        ReturnPending = false;

        await CreatureCmd.SetCurrentHp(Owner.Creature, hp);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(
            choiceContext,
            DynamicVars.Cards.BaseValue,
            Owner);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}

public abstract class SilentOrchestraPageChoiceCardBase : CardModel
{
    public const string EverRepeatingPerformanceChoiceId =
        "SILENT_ORCHESTRA_EVER_REPEATING_PERFORMANCE_CHOICE_CARD";
    public const string FerventAdorationChoiceId =
        "SILENT_ORCHESTRA_FERVENT_ADORATION_CHOICE_CARD";
    public const string FinaleChoiceId =
        "SILENT_ORCHESTRA_FINALE_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..HoverTipFactory.FromCardWithCardHoverTips<
            EverRepeatingPerformanceCard>(),
        HoverTipFactory.Static(StaticHoverTip.Stun)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(EverRepeatingPerformanceCard.NextTurnEnergy),
        new CardsVar(EverRepeatingPerformanceCard.NextTurnCards),
        new DynamicVar(
            "DamagePercent",
            SilentOrchestraPageRelic.FerventAdorationDamagePercent),
        new DynamicVar(
            "StunTurns",
            SilentOrchestraPageRelic.FinaleStunTurns)
    ];

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            $"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected SilentOrchestraPageChoiceCardBase()
        : base(
            -1,
            CardType.Skill,
            CardRarity.Ancient,
            TargetType.None,
            shouldShowInCardLibrary: false)
    {
    }

    public static bool IsSilentOrchestraChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is EverRepeatingPerformanceChoiceId
            or FerventAdorationChoiceId
            or FinaleChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class SilentOrchestraEverRepeatingPerformanceChoiceCard :
    SilentOrchestraPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "ever_repeating_performance.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class SilentOrchestraFerventAdorationChoiceCard :
    SilentOrchestraPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "silent_orchestra_fervent_adoration_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class SilentOrchestraFinaleChoiceCard :
    SilentOrchestraPageChoiceCardBase
{
    protected override string PortraitFileName =>
        "silent_orchestra_finale_choice_card.png";
}

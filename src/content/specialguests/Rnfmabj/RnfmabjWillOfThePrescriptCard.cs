using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.content.specialguests.Rnfmabj;

internal enum RnfmabjPrescriptTaskType
{
    None = 0,
    Sequence = 1,
    NoAttack = 2,
    NoSkill = 3,
    NoPower = 4,
    AtMostSixCards = 5,
    WinCombat = 6,
}

[CardPool(typeof(QuestCardPool))]
public sealed class RnfmabjWillOfThePrescriptCard() : CardModel(
    -1,
    CardType.Quest,
    CardRarity.Quest,
    TargetType.None)
{
    public const string PortraitAssetPath =
        "res://images/packed/card_portraits/quest/rnfmabj_will_of_the_prescript_card.png";
    internal const string CardHoverTipScenePath =
        "res://scenes/ui/card_hover_tip.tscn";

    internal const int MaxCardsPerTurn = 6;
    internal const int NextTurnEnergy = 3;
    internal const int FreeCardsNextTurn = 2;
    private const int FullHandSize = 10;
    private static readonly RnfmabjPrescriptTaskType[] RollableTaskTypes =
    [
        RnfmabjPrescriptTaskType.Sequence,
        RnfmabjPrescriptTaskType.NoAttack,
        RnfmabjPrescriptTaskType.NoSkill,
        RnfmabjPrescriptTaskType.NoPower,
        RnfmabjPrescriptTaskType.AtMostSixCards,
        RnfmabjPrescriptTaskType.WinCombat,
    ];

    [SavedProperty]
    internal RnfmabjPrescriptTaskType ActiveTask { get; private set; }

    [SavedProperty]
    internal RnfmabjPrescriptTaskType LastTriggeredTask { get; private set; }

    [SavedProperty]
    public bool TaskActive { get; private set; }

    [SavedProperty]
    public bool TaskFailed { get; private set; }

    [SavedProperty]
    public bool TaskCompleted { get; private set; }

    [SavedProperty]
    public int CardsPlayedThisTurn { get; private set; }

    [SavedProperty]
    public int SequenceProgress { get; private set; }

    [SavedProperty]
    public int[] SequenceCodes { get; private set; } = [];

    [SavedProperty]
    public bool LimitedTaskRolledEver { get; private set; }

    [SavedProperty]
    public bool PendingEnergyAndFullHand { get; private set; }

    [SavedProperty]
    public bool PendingEnergyGranted { get; private set; }

    [SavedProperty]
    public bool PendingFreeCards { get; private set; }

    [SavedProperty]
    public int FreeCardsRemaining { get; private set; }

    [SavedProperty]
    public bool PendingBonusGoldReward { get; private set; }

    [SavedProperty]
    public bool RemovalRewardAddedThisCombat { get; private set; }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        SequenceCodes = (int[])SequenceCodes.Clone();
    }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public override string PortraitPath => PortraitAssetPath;

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Unplayable,
        CardKeyword.Innate,
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<IntangiblePower>(),
    ];

    protected override IEnumerable<string> ExtraRunAssetPaths =>
    [
        CardHoverTipScenePath,
    ];

    public override Task BeforeCombatStart()
    {
        ResetTransientCombatState();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        _ = choiceContext;
        _ = combatState;
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        if (PendingFreeCards)
        {
            PendingFreeCards = false;
            FreeCardsRemaining = FreeCardsNextTurn;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        _ = choiceContext;
        _ = fromHandDraw;
        if (card != this || TaskActive)
        {
            return Task.CompletedTask;
        }

        RollNextTask(Owner.RunState.Rng.Niche);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature))
        {
            return;
        }

        if (TaskActive && !TaskCompleted)
        {
            await ResolveTaskAtTurnEnd(choiceContext);
        }

        TaskActive = false;
        FreeCardsRemaining = 0;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (FreeCardsRemaining > 0 && cardPlay.Player == Owner)
        {
            FreeCardsRemaining--;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext context,
        CardPlay cardPlay)
    {
        if (!TaskActive || TaskCompleted || cardPlay.Player != Owner)
        {
            return;
        }

        CardsPlayedThisTurn++;
        switch (ActiveTask)
        {
            case RnfmabjPrescriptTaskType.Sequence:
                await AdvanceSequence(context, cardPlay.Card.Type);
                break;
            case RnfmabjPrescriptTaskType.NoAttack:
                TaskFailed |= cardPlay.Card.Type == CardType.Attack;
                break;
            case RnfmabjPrescriptTaskType.NoSkill:
                TaskFailed |= cardPlay.Card.Type == CardType.Skill;
                break;
            case RnfmabjPrescriptTaskType.NoPower:
                TaskFailed |= cardPlay.Card.Type == CardType.Power;
                break;
            case RnfmabjPrescriptTaskType.AtMostSixCards:
                TaskFailed |= CardsPlayedThisTurn > MaxCardsPerTurn;
                break;
        }
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner
            || !PendingEnergyAndFullHand
            || PendingEnergyGranted)
        {
            return;
        }

        PendingEnergyGranted = true;
        await PlayerCmd.GainEnergy(NextTurnEnergy, player);
    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player == Owner && PendingEnergyAndFullHand
            ? Math.Max(count, FullHandSize)
            : count;

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        _ = choiceContext;
        if (player == Owner && PendingEnergyAndFullHand)
        {
            PendingEnergyAndFullHand = false;
            PendingEnergyGranted = false;
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ShouldMakeCardFree(card))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override bool TryModifyStarCost(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ShouldMakeCardFree(card))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (Pile?.IsCombatPile != true || !TaskActive || TaskCompleted)
        {
            return Task.CompletedTask;
        }

        switch (ActiveTask)
        {
            case RnfmabjPrescriptTaskType.AtMostSixCards
                when !TaskFailed && CardsPlayedThisTurn <= MaxCardsPerTurn:
                TaskCompleted = true;
                MarkPendingBonusGoldReward();
                break;
            case RnfmabjPrescriptTaskType.WinCombat:
                TaskCompleted = true;
                AddCardRemovalRewardOnce(room);
                break;
            default:
                TaskFailed = !TaskCompleted;
                break;
        }

        TaskActive = false;
        return Task.CompletedTask;
    }

    public override bool TryModifyRewards(
        Player player,
        List<Reward> rewards,
        AbstractRoom? room)
    {
        if (player != Owner
            || !PendingBonusGoldReward
            || room == null
            || !room.RoomType.IsCombatRoom()
            || room.RoomType == RoomType.Boss
            && player.RunState.CurrentActIndex >= player.RunState.Acts.Count - 1)
        {
            return false;
        }

        int goldAmount = 33;
        rewards.Add(new GoldReward(goldAmount, player));
        return true;
    }

    public override Task AfterModifyingRewards()
    {
        PendingBonusGoldReward = false;
        return Task.CompletedTask;
    }

    internal string GetTaskPresentationFingerprint() => string.Join(
        '|',
        TaskActive ? "1" : "0",
        ((int)ActiveTask).ToString(CultureInfo.InvariantCulture),
        TaskFailed ? "1" : "0",
        TaskCompleted ? "1" : "0",
        CardsPlayedThisTurn.ToString(CultureInfo.InvariantCulture),
        SequenceProgress.ToString(CultureInfo.InvariantCulture),
        string.Join(',', SequenceCodes),
        FreeCardsRemaining.ToString(CultureInfo.InvariantCulture));

    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("Body", BuildDescriptionBody());
    }

    private async Task ResolveTaskAtTurnEnd(PlayerChoiceContext choiceContext)
    {
        bool succeeded = ActiveTask switch
        {
            RnfmabjPrescriptTaskType.NoAttack => !TaskFailed,
            RnfmabjPrescriptTaskType.NoSkill => !TaskFailed,
            RnfmabjPrescriptTaskType.NoPower => !TaskFailed,
            RnfmabjPrescriptTaskType.AtMostSixCards =>
                !TaskFailed && CardsPlayedThisTurn <= MaxCardsPerTurn,
            _ => false,
        };
        if (!succeeded)
        {
            TaskFailed = !TaskCompleted;
            return;
        }

        await CompleteTask(choiceContext);
    }

    private async Task AdvanceSequence(
        PlayerChoiceContext choiceContext,
        CardType playedType)
    {
        if (SequenceCodes.Length == 0)
        {
            TaskFailed = true;
            return;
        }

        int expectedCode = SequenceCodes[
            Math.Clamp(SequenceProgress, 0, SequenceCodes.Length - 1)];
        if ((int)playedType == expectedCode)
        {
            SequenceProgress++;
        }
        else
        {
            SequenceProgress = (int)playedType == SequenceCodes[0] ? 1 : 0;
        }

        if (SequenceProgress >= SequenceCodes.Length)
        {
            await CompleteTask(choiceContext);
        }
    }

    private async Task CompleteTask(PlayerChoiceContext choiceContext)
    {
        if (TaskCompleted)
        {
            return;
        }

        TaskCompleted = true;
        TaskFailed = false;
        switch (ActiveTask)
        {
            case RnfmabjPrescriptTaskType.Sequence:
                await FakeDeathDebuffHelper.ClearDebuffs(Owner.Creature);
                break;
            case RnfmabjPrescriptTaskType.NoAttack:
                await PowerCmdCompat.Apply<IntangiblePower>(
                    choiceContext,
                    Owner.Creature,
                    1m,
                    Owner.Creature,
                    this);
                break;
            case RnfmabjPrescriptTaskType.NoSkill:
                PendingEnergyAndFullHand = true;
                PendingEnergyGranted = false;
                break;
            case RnfmabjPrescriptTaskType.NoPower:
                PendingFreeCards = true;
                break;
            case RnfmabjPrescriptTaskType.AtMostSixCards:
                MarkPendingBonusGoldReward();
                break;
        }
    }

    private bool ShouldMakeCardFree(CardModel card)
    {
        if (FreeCardsRemaining <= 0 || card.Owner != Owner)
        {
            return false;
        }

        return card.Pile?.Type is PileType.Hand or PileType.Play;
    }

    private void RollNextTask(Rng rng)
    {
        ActiveTask = RollTaskType(rng);
        LastTriggeredTask = ActiveTask;
        if (DeckVersion is RnfmabjWillOfThePrescriptCard deckCard)
        {
            deckCard.LastTriggeredTask = ActiveTask;
        }

        TaskActive = true;
        TaskFailed = false;
        TaskCompleted = false;
        CardsPlayedThisTurn = 0;
        SequenceProgress = 0;
        SequenceCodes = ActiveTask == RnfmabjPrescriptTaskType.Sequence
            ? RollSequence(rng)
            : [];

        if (ActiveTask == RnfmabjPrescriptTaskType.AtMostSixCards)
        {
            MarkLimitedTaskRolled();
        }
    }

    private RnfmabjPrescriptTaskType RollTaskType(Rng rng)
    {
        int totalWeight = 0;
        foreach (RnfmabjPrescriptTaskType task in RollableTaskTypes)
        {
            if (task != LastTriggeredTask)
            {
                totalWeight += GetTaskWeight(task);
            }
        }

        int roll = rng.NextInt(totalWeight);
        foreach (RnfmabjPrescriptTaskType task in RollableTaskTypes)
        {
            if (task == LastTriggeredTask)
            {
                continue;
            }

            int weight = GetTaskWeight(task);
            if (roll < weight)
            {
                return task;
            }

            roll -= weight;
        }

        throw new InvalidOperationException("No rnfmabj Prescript task was available to roll.");
    }

    private int GetTaskWeight(RnfmabjPrescriptTaskType task) => task switch
    {
        RnfmabjPrescriptTaskType.Sequence => 20,
        RnfmabjPrescriptTaskType.NoAttack => 10,
        RnfmabjPrescriptTaskType.NoSkill => 10,
        RnfmabjPrescriptTaskType.NoPower => 10,
        RnfmabjPrescriptTaskType.AtMostSixCards when !LimitedTaskRolledEver => 25,
        RnfmabjPrescriptTaskType.WinCombat => 25,
        _ => 0,
    };

    private static int[] RollSequence(Rng rng)
    {
        int[] sequence =
        [
            (int)CardType.Attack,
            (int)CardType.Skill,
            (int)CardType.Power,
        ];
        for (int index = sequence.Length - 1; index > 0; index--)
        {
            int swapIndex = rng.NextInt(index + 1);
            (sequence[index], sequence[swapIndex]) =
                (sequence[swapIndex], sequence[index]);
        }

        return sequence;
    }

    private void MarkLimitedTaskRolled()
    {
        LimitedTaskRolledEver = true;
        if (DeckVersion is RnfmabjWillOfThePrescriptCard deckCard)
        {
            deckCard.LimitedTaskRolledEver = true;
        }
    }

    private void MarkPendingBonusGoldReward()
    {
        PendingBonusGoldReward = true;
        if (DeckVersion is RnfmabjWillOfThePrescriptCard deckCard)
        {
            deckCard.PendingBonusGoldReward = true;
        }
    }

    private void AddCardRemovalRewardOnce(CombatRoom room)
    {
        if (RemovalRewardAddedThisCombat)
        {
            return;
        }

        RemovalRewardAddedThisCombat = true;
        if (DeckVersion is RnfmabjWillOfThePrescriptCard deckCard)
        {
            deckCard.RemovalRewardAddedThisCombat = true;
        }
        room.AddExtraReward(Owner, new CardRemovalReward(Owner));
    }

    private void ResetTransientCombatState()
    {
        ActiveTask = RnfmabjPrescriptTaskType.None;
        TaskActive = false;
        TaskFailed = false;
        TaskCompleted = false;
        CardsPlayedThisTurn = 0;
        SequenceProgress = 0;
        SequenceCodes = [];
        PendingEnergyAndFullHand = false;
        PendingEnergyGranted = false;
        PendingFreeCards = false;
        FreeCardsRemaining = 0;
        PendingBonusGoldReward = false;
        RemovalRewardAddedThisCombat = false;
    }

    private string BuildDescriptionBody()
    {
        LocString body = new(
            "cards",
            Id.Entry + (TaskActive ? ".active" : ".overview"));
        AddSharedDescriptionArgs(body);
        if (!TaskActive)
        {
            return body.GetFormattedText();
        }

        string taskKey = GetTaskKey(ActiveTask);
        LocString task = new("cards", Id.Entry + ".tasks." + taskKey + ".task");
        LocString reward = new("cards", Id.Entry + ".tasks." + taskKey + ".reward");
        AddSharedDescriptionArgs(task);
        AddSharedDescriptionArgs(reward);
        body.Add("Task", task.GetFormattedText());
        body.Add("Reward", reward.GetFormattedText());
        body.Add("Status", BuildStatusText());
        return body.GetFormattedText();
    }

    private string BuildStatusText()
    {
        string suffix;
        if (TaskCompleted)
        {
            suffix = "completed";
        }
        else if (TaskFailed)
        {
            suffix = "failed";
        }
        else if (ActiveTask == RnfmabjPrescriptTaskType.AtMostSixCards)
        {
            suffix = "cards";
        }
        else
        {
            suffix = "active";
        }

        LocString status = new("cards", Id.Entry + ".status." + suffix);
        AddSharedDescriptionArgs(status);
        return status.GetFormattedText();
    }

    private void AddSharedDescriptionArgs(LocString text)
    {
        text.Add("Sequence", BuildSequenceText());
        text.Add("CardsPlayed", CardsPlayedThisTurn);
        text.Add("MaxCards", MaxCardsPerTurn);
        text.Add("Energy", NextTurnEnergy);
        text.Add("FreeCards", FreeCardsNextTurn);
        text.Add(
            "Gold",
            ModelDb.Relic<AmethystAubergine>().DynamicVars.Gold.IntValue);
    }

    private string BuildSequenceText()
    {
        string[] labels = new string[SequenceCodes.Length];
        for (int index = 0; index < SequenceCodes.Length; index++)
        {
            CardType cardType = (CardType)SequenceCodes[index];
            string label = cardType.ToLocString().GetFormattedText();
            labels[index] = index < SequenceProgress
                ? "[green]" + label + "[/green]"
                : label;
        }

        return string.Join('-', labels);
    }

    private static string GetTaskKey(RnfmabjPrescriptTaskType task) => task switch
    {
        RnfmabjPrescriptTaskType.Sequence => "SEQUENCE",
        RnfmabjPrescriptTaskType.NoAttack => "NO_ATTACK",
        RnfmabjPrescriptTaskType.NoSkill => "NO_SKILL",
        RnfmabjPrescriptTaskType.NoPower => "NO_POWER",
        RnfmabjPrescriptTaskType.AtMostSixCards => "AT_MOST_SIX",
        RnfmabjPrescriptTaskType.WinCombat => "WIN_COMBAT",
        _ => "NONE",
    };
}

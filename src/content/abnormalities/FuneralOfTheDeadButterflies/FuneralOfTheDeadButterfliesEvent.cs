using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.DeadButterfly;
using LibraryOfRuina.content.acts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;

public sealed class FuneralOfTheDeadButterfliesEvent : EventModel
{
    private const decimal MaxHpLossPercent = 0.10m;
    private const int RemovalCount = 2;
    private const int CardPickCount = 1;
    private const int CardChoiceCount = 5;

    public override bool IsShared => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MaxHpLossPercent", MaxHpLossPercent),
        new DynamicVar("MaxHpLoss", 1),
        new DynamicVar("Cards", RemovalCount),
        new DynamicVar("PickCount", CardPickCount),
        new DynamicVar("ChoiceCount", CardChoiceCount),
        new StringVar("BookRelic", ModelDb.Relic<DeadButterfliesBookRelic>().Title.GetFormattedText())
    ];

    public override bool IsAllowed(IRunState runState)
    {
        if (runState is RunState concreteRunState)
        {
            return !concreteRunState.VisitedEventIds.Contains(Id)
                && LibraryOfRuinaActModel.IsFirstFamily(runState);
        }
        return false;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        RefreshMaxHpLossVar();
        bool canFight = Owner?.RunState.Players.All(player => player.GetRelic<DeadButterfliesBookRelic>() != null) == true;
        return
        [
            new EventOption(this, LoseHpAndRemoveCards, InitialOptionKey("REMOVE"))
                .ThatDecreasesMaxHp(DynamicVars["MaxHpLoss"].BaseValue),
            new EventOption(this, ChooseUpgradedCard, InitialOptionKey("CARD")),
            new EventOption(
                this,
                canFight ? FightFuneral : null,
                InitialOptionKey(canFight ? "FIGHT" : "FIGHT_LOCKED"),
                HoverTipFactory.FromRelic<DeadButterfliesBookRelic>())
        ];
    }

    private async Task LoseHpAndRemoveCards()
    {
        if (Owner is not { } owner)
        {
            return;
        }

        int hpLoss = ResolveMaxHpLoss();
        if (hpLoss > 0)
        {
            await CreatureCmd.LoseMaxHp(
                new ThrowingPlayerChoiceContext(),
                owner.Creature,
                hpLoss,
                isFromCard: false);
        }

        var cards = (await CardSelectCmd.FromDeckForRemoval(
            prefs: new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, RemovalCount),
            player: owner)).ToList();

        await CardPileCmd.RemoveFromDeck(cards);
        SetEventFinished(L10NLookup("FUNERAL_OF_THE_DEAD_BUTTERFLIES_EVENT.pages.REMOVE.description"));
    }

    private async Task ChooseUpgradedCard()
    {
        if (Owner is not { } owner)
        {
            return;
        }

        List<CardModel> options = CardFactory.CreateForReward(
                owner,
                CardChoiceCount,
                CardCreationOptions.ForNonCombatWithDefaultOdds([owner.Character.CardPool]))
            .Select(result => result.Card)
            .ToList();

        foreach (CardModel card in options.Where(card => card.IsUpgradable))
        {
            CardCmd.Upgrade(card, CardPreviewStyle.None);
        }

        CardModel? chosen = (await CardSelectCmd.FromSimpleGrid(
                new BlockingPlayerChoiceContext(),
                options,
                owner,
                new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, CardPickCount)))
            .FirstOrDefault();

        if (chosen != null)
        {
            await CardPileCmd.Add(chosen, PileType.Deck);
        }

        SetEventFinished(L10NLookup("FUNERAL_OF_THE_DEAD_BUTTERFLIES_EVENT.pages.CARD.description"));
    }

    private Task FightFuneral()
    {
        EnterCombatWithoutExitingEvent<FuneralOfTheDeadButterfliesEncounter>([], shouldResumeAfterCombat: true);
        return Task.CompletedTask;
    }

    public override Task Resume(AbstractRoom room)
    {
        if (room is CombatRoom combatRoom && combatRoom.Encounter is FuneralOfTheDeadButterfliesEncounter)
        {
            SetEventFinished(L10NLookup("FUNERAL_OF_THE_DEAD_BUTTERFLIES_EVENT.pages.FIGHT.description"));
        }

        return Task.CompletedTask;
    }

    private int ResolveMaxHpLoss()
    {
        if (Owner?.Creature == null)
        {
            return 0;
        }

        return (int)decimal.Ceiling(Owner.Creature.MaxHp * MaxHpLossPercent);
    }

    private void RefreshMaxHpLossVar()
    {
        DynamicVars["MaxHpLoss"].BaseValue = ResolveMaxHpLoss();
    }
}

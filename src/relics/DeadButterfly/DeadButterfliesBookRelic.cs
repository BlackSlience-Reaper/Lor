using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.encounters.FuneralOfTheDeadButterflies;
using LibraryOfRuina.events.FuneralOfTheDeadButterflies;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.relics.DeadButterfly;

public sealed class DeadButterfliesBookRelic : RelicModel
{
    private const string InFuneralCombatVar = "InFuneralCombat";
    private const float FuneralEventWeightBonus = 60f;

    protected override string IconBaseName => "dead_butterflies_book_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) => false;

    public override EventModel ModifyNextEvent(EventModel currentEvent)
    {
        if (currentEvent is FuneralOfTheDeadButterfliesEvent
            || Owner.RunState is not RunState runState)
        {
            return currentEvent;
        }

        Player? firstBookOwner = runState.Players
            .FirstOrDefault(player => player.GetRelic<DeadButterfliesBookRelic>() != null);
        if (firstBookOwner != Owner)
        {
            return currentEvent;
        }

        EventModel funeralEvent = ModelDb.Event<FuneralOfTheDeadButterfliesEvent>();
        if (!funeralEvent.IsAllowed(runState)
            || runState.VisitedEventIds.Contains(funeralEvent.Id))
        {
            return currentEvent;
        }

        var eligibleEvents = runState.Act.AllEvents
            .Concat(ModelDb.AllSharedEvents)
            .Where(eventModel =>
                eventModel.IsAllowed(runState)
                && !runState.VisitedEventIds.Contains(eventModel.Id))
            .Distinct()
            .ToList();
        if (eligibleEvents.All(eventModel => eventModel.Id != funeralEvent.Id))
        {
            return currentEvent;
        }

        float replacementChance = FuneralEventWeightBonus / (eligibleEvents.Count + FuneralEventWeightBonus);
        return runState.Rng.Niche.NextFloat() < replacementChance
            ? funeralEvent
            : currentEvent;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new CardsVar(2),
        new DynamicVar(InFuneralCombatVar, 0)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    public override Task BeforeCombatStart()
    {
        RefreshDescriptionState();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        DynamicVars[InFuneralCombatVar].BaseValue = 0;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        RefreshDescriptionState();
        return Task.CompletedTask;
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner || !IsInFuneralCombat)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !IsInFuneralCombat)
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    private bool IsInFuneralCombat =>
        Owner.Creature?.CombatState?.Encounter is FuneralOfTheDeadButterfliesEncounter;

    private void RefreshDescriptionState()
    {
        DynamicVars[InFuneralCombatVar].BaseValue = IsInFuneralCombat ? 1 : 0;
        InvokeDisplayAmountChanged();
    }
}

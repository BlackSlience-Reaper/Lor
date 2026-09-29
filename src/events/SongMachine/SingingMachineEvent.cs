using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LibraryOfRuina.acts;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.events.SongMachine;

public sealed class SingingMachineEvent : EventModel
{
    private const string CardsKey = "Cards";

    public const string EventId = "SINGING_MACHINE_EVENT";
    public const string BackgroundPath = "res://images/events/singing_machine_event.png";

    public override bool IsShared => false;

    public override EventLayoutType LayoutType => EventLayoutType.Default;

    public override LocString InitialDescription =>
        L10NLookup("SINGING_MACHINE_EVENT.pages.INITIAL.description");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HpLossVar(0m),
        new HealVar(0m),
        new DynamicVar(CardsKey, 0m)
    ];

    public override bool IsAllowed(IRunState runState)
    {
        if (runState is RunState concreteRunState)
        {
            return !concreteRunState.VisitedEventIds.Contains(Id)
                && LibraryOfRuinaActModel.IsThirdFamily(runState);
        }

        return false;
    }

    public override IEnumerable<string> GetAssetPaths(IRunState runState)
    {
        HashSet<string> paths = new(base.GetAssetPaths(runState))
        {
            BackgroundPath
        };
        return paths;
    }

    public override void CalculateVars()
    {
        decimal maxHp = Owner?.Creature?.MaxHp ?? 0m;
        int hpLoss = Math.Max(1, (int)Math.Floor(maxHp * 0.20m));
        int heal = Math.Max(1, (int)Math.Floor(maxHp * 0.16m));

        DynamicVars.HpLoss.BaseValue = hpLoss;
        DynamicVars.Heal.BaseValue = heal;
        DynamicVars[CardsKey].BaseValue = 4;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, Approach, InitialOptionKey("APPROACH"))
                .ThatDoesDamage(DynamicVars.HpLoss.BaseValue),
            new EventOption(this, Observe, InitialOptionKey("OBSERVE"))
        ];
    }

    private async Task Approach()
    {
        if (Owner is not { } owner)
        {
            return;
        }

        await CreatureCmdCompat.Damage(
            new ThrowingPlayerChoiceContext(),
            owner.Creature,
            DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

        UpgradeRandomDeckCards(owner, DynamicVars[CardsKey].IntValue);

        SetEventFinished(
            L10NLookup("SINGING_MACHINE_EVENT.pages.APPROACH_FOLLOWUP.description"));
    }

    private async Task Observe()
    {
        if (Owner is not { } owner)
        {
            return;
        }

        await CreatureCmd.Heal(owner.Creature, DynamicVars.Heal.IntValue);
        SetEventFinished(
            L10NLookup("SINGING_MACHINE_EVENT.pages.OBSERVE_FOLLOWUP.description"));
    }

    private void UpgradeRandomDeckCards(Player owner, int maxUpgrades)
    {
        if (maxUpgrades <= 0)
        {
            return;
        }

        List<CardModel> candidates = PileType.Deck
            .GetPile(owner)
            .Cards
            .Where(IsUpgradeable)
            .ToList();

        int upgrades = Math.Min(maxUpgrades, candidates.Count);
        for (int i = 0; i < upgrades; i++)
        {
            int pickedIndex = Rng.NextInt(candidates.Count);
            CardModel picked = candidates[pickedIndex];
            candidates.RemoveAt(pickedIndex);
            CardCmd.Upgrade(picked, CardPreviewStyle.EventLayout);
        }
    }

    private static bool IsUpgradeable(CardModel card)
    {
        
        
        Type type = card.GetType();

        PropertyInfo? isUpgradableProp = type.GetProperty("IsUpgradable", BindingFlags.Instance | BindingFlags.Public);
        if (isUpgradableProp?.PropertyType == typeof(bool))
        {
            return (bool)isUpgradableProp.GetValue(card)!;
        }

        PropertyInfo? canUpgradeProp = type.GetProperty("CanUpgrade", BindingFlags.Instance | BindingFlags.Public);
        if (canUpgradeProp?.PropertyType == typeof(bool))
        {
            return (bool)canUpgradeProp.GetValue(card)!;
        }

        PropertyInfo? upgradeableProp = type.GetProperty("Upgradeable", BindingFlags.Instance | BindingFlags.Public);
        if (upgradeableProp?.PropertyType == typeof(bool))
        {
            return (bool)upgradeableProp.GetValue(card)!;
        }

        MethodInfo? canUpgradeMethod = type.GetMethod("CanUpgrade", BindingFlags.Instance | BindingFlags.Public, binder: null, types: Type.EmptyTypes, modifiers: null);
        if (canUpgradeMethod?.ReturnType == typeof(bool))
        {
            return (bool)canUpgradeMethod.Invoke(card, null)!;
        }

        
        return type.GetMethod("Upgrade", BindingFlags.Instance | BindingFlags.Public) != null;
    }
}

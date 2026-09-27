using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.Nosferatu;
using LibraryOfRuina.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.Nosferatu;

public enum NosferatuPageMode
{
    None = 0,
    Hydrophobia = 1,
    Vampirism = 2,
    Wine = 3
}

public sealed class NosferatuPageRelic : LibraryRelicModel
{
    internal const int HydrophobiaBleed = 3;
    internal const int VampirismDamageBonus = 5;
    internal const int VampirismHeal = 2;
    internal const int WineHeal = 12;

    protected override string IconBaseName => "nosferatu_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<NosferatuPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)NosferatuPageMode.None),
        new DynamicVar("HydrophobiaBleed", HydrophobiaBleed),
        new DynamicVar("DamageBonus", VampirismDamageBonus),
        new HealVar("VampirismHeal", VampirismHeal),
        new HealVar("WineHeal", WineHeal)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    [SavedProperty]
    public NosferatuPageMode Mode { get; private set; }

    private HashSet<uint> _wineHealedCombatIds = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _wineHealedCombatIds = [.. _wineHealedCombatIds];
    }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        UpdateModeUiState();
        if (Mode != NosferatuPageMode.None)
        {
            RefreshInventoryIcon();
            return;
        }

        IReadOnlyList<CardModel> options = CreateModeChoiceCards();
        CardModel? chosenCard = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            options,
            Owner,
            canSkip: true);

        if (chosenCard == null)
        {
            await AbnormalityPageRewardHelper.SkipObtainedPageRelic(this, nameof(AfterObtained));
            return;
        }

        SetMode(ResolveModeFromChoiceCard(chosenCard));
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        RefreshInventoryIcon();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        _wineHealedCombatIds.Clear();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        return IsVampirismDamageBonusActive(target, props, dealer, cardSource)
            ? VampirismDamageBonus
            : 0m;
    }

    public override decimal ModifyChaoDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        return IsVampirismDamageBonusActive(target, props, dealer, cardSource)
            ? VampirismDamageBonus
            : 0m;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (result.UnblockedDamage <= 0m
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !IsOwnerAttackSource(dealer, cardSource, props))
        {
            return;
        }

        switch (Mode)
        {
            case NosferatuPageMode.Hydrophobia:
                Flash([target]);
                await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                    target,
                    HydrophobiaBleed,
                    Owner.Creature,
                    cardSource);
                break;
            case NosferatuPageMode.Vampirism when HasBleeding(target):
                Flash([target]);
                await CreatureCmd.Heal(Owner.Creature, VampirismHeal);
                break;
            case NosferatuPageMode.Wine when result.WasTargetKilled && HasBleeding(target):
                await HealAllPlayersForWine(target);
                break;
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _wineHealedCombatIds.Clear();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private async Task HealAllPlayersForWine(Creature killedTarget)
    {
        uint? combatId = killedTarget.CombatId;
        if (combatId == null || !_wineHealedCombatIds.Add(combatId.Value))
        {
            return;
        }

        IReadOnlyList<Creature> players = Owner.Creature.CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .OrderBy(static player => player.CombatId ?? uint.MaxValue)
            .ToArray() ?? [];
        if (players.Count == 0)
        {
            return;
        }

        Flash(players);
        foreach (Creature player in players)
        {
            await CreatureCmd.Heal(player, WineHeal);
        }
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<NosferatuHydrophobiaChoiceCard>(Owner),
            Owner.RunState.CreateCard<NosferatuVampirismChoiceCard>(Owner),
            Owner.RunState.CreateCard<NosferatuWineChoiceCard>(Owner)
        ];
    }

    private static NosferatuPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            NosferatuHydrophobiaChoiceCard => NosferatuPageMode.Hydrophobia,
            NosferatuVampirismChoiceCard => NosferatuPageMode.Vampirism,
            NosferatuWineChoiceCard => NosferatuPageMode.Wine,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(NosferatuPageMode mode)
    {
        return mode is NosferatuPageMode.None
            or NosferatuPageMode.Hydrophobia
            or NosferatuPageMode.Vampirism
            or NosferatuPageMode.Wine;
    }

    private static bool IsConcreteMode(NosferatuPageMode mode)
    {
        return mode is NosferatuPageMode.Hydrophobia
            or NosferatuPageMode.Vampirism
            or NosferatuPageMode.Wine;
    }

    private void SetMode(NosferatuPageMode mode)
    {
        Mode = mode;
        _wineHealedCombatIds.Clear();
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void EnsureValidModeOrFallback(string context)
    {
        if (IsConcreteMode(Mode))
        {
            return;
        }

        FallbackToDefaultModeAfterLoad(context);
    }

    private void FallbackToDefaultModeAfterLoad(string context)
    {
        NosferatuPageMode oldMode = Mode;
        Mode = NosferatuPageMode.Hydrophobia;
        _wineHealedCombatIds.Clear();
        Log.Warn("[LibraryOfRuina.PageRelic] NosferatuPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Hydrophobia.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = RelicStatus.Normal;
    }

    private bool IsOwnerAttackSource(Creature? dealer, CardModel? cardSource, ValueProp props)
    {
        if (dealer == null || !ValuePropCompat.IsPoweredAttack(props))
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        return cardSource == null || cardSource.Owner == Owner;
    }

    private bool IsVampirismDamageBonusActive(
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return Mode == NosferatuPageMode.Vampirism
            && target != null
            && target.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target)
            && HasBleeding(target)
            && IsOwnerAttackSource(dealer, cardSource, props);
    }

    private static bool HasBleeding(Creature target) =>
        target.GetPower<LibraryBleedingPower>()?.Amount > 0;

    private void RefreshInventoryIcon()
    {
        NRelicInventory? inventory = NRun.Instance?.GlobalUi?.RelicInventory;
        if (inventory == null)
        {
            return;
        }

        foreach (NRelicInventoryHolder holder in inventory.RelicNodes)
        {
            if (!ReferenceEquals(holder.Relic.Model, this))
            {
                continue;
            }

            holder.Relic.Icon.Texture = Icon;
            holder.Relic.Outline.Texture = IconOutline;
            break;
        }
    }
}

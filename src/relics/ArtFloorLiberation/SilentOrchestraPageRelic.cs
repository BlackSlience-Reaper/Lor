using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Commands;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.cards.ArtFloorLiberation;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.ArtFloorLiberation;

public sealed class SilentOrchestraPageRelic : ModalPageRelic<SilentOrchestraPageMode>
{
    internal const int FerventAdorationDamagePercent = 200;
    internal const int FinaleStunTurns = 1;

    private const string ArtFloorIconPath =
        "res://images/ui/run_history/art_floor_liberation_encounter.png";
    private const string ArtFloorIconOutlinePath =
        "res://images/ui/run_history/art_floor_liberation_encounter_outline.png";

    private bool _applyingFinaleStun;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override string PackedIconPath => ArtFloorIconPath;

    protected override string PackedIconOutlinePath =>
        ArtFloorIconOutlinePath;

    protected override string BigIconPath => ArtFloorIconPath;

    public override bool IsAllowed(IRunState runState)
    {
        _ = runState;
        return false;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)SilentOrchestraPageMode.None),
        new EnergyVar(EverRepeatingPerformanceCard.NextTurnEnergy),
        new CardsVar(EverRepeatingPerformanceCard.NextTurnCards),
        new DynamicVar("DamagePercent", FerventAdorationDamagePercent),
        new DynamicVar("StunTurns", FinaleStunTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        SilentOrchestraPageMode.EverRepeatingPerformance =>
        [
            ..HoverTipFactory.FromCardWithCardHoverTips<
                EverRepeatingPerformanceCard>()
        ],
        SilentOrchestraPageMode.Finale =>
        [
            HoverTipFactory.Static(StaticHoverTip.Stun)
        ],
        _ => []
    };

    [SavedProperty]
    public SilentOrchestraPageMode Mode { get; private set; }

    protected override SilentOrchestraPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshIconOnModeChange => false;

    protected override bool RefreshUiBeforeModeChoice => true;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FinaleTrackedRound { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] FinaleStunnedThisRoundCombatIds { get; private set; } = [];

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] FinalePendingCombatIds { get; private set; } = [];

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int[] FinalePendingRounds { get; private set; } = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        FinaleStunnedThisRoundCombatIds =
            [.. FinaleStunnedThisRoundCombatIds];
        FinalePendingCombatIds = [.. FinalePendingCombatIds];
        FinalePendingRounds = [.. FinalePendingRounds];
        _applyingFinaleStun = false;
    }

    protected override async Task ApplyObtainedChoiceAsync(SilentOrchestraPageMode mode)
    {
        SetMode(mode);
        if (Mode == SilentOrchestraPageMode.EverRepeatingPerformance)
        {
            await AddEverRepeatingPerformanceCardOnPickup();
        }
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetFinaleCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        if (Mode != SilentOrchestraPageMode.FerventAdoration
            || !IsHostileEnemyAttack(command)
            || command.Attacker?.CombatState is not { } combatState)
        {
            return;
        }

        SilentOrchestraPageRelic[] holders = GetModeRelics(
            combatState,
            SilentOrchestraPageMode.FerventAdoration);
        if (holders.Length == 0 || !ReferenceEquals(this, holders[0]))
        {
            return;
        }

        int attackDamage = command.Results.Sum(static hit =>
            hit.Count == 0
                ? 0
                : hit.Max(static result => result.TotalDamage));
        int damage = (int)decimal.Floor(
            attackDamage
            * FerventAdorationDamagePercent
            * holders.Length
            / 100m);
        if (damage <= 0)
        {
            return;
        }

        Creature[] targets = combatState.Enemies
            .Where(static enemy =>
                enemy.IsAlive && !AllyTurnRegistry.IsAllyCreature(enemy))
            .OrderBy(static enemy => enemy.CombatId ?? uint.MaxValue)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        foreach (SilentOrchestraPageRelic holder in holders)
        {
            holder.Flash(targets);
        }

        await CreatureCmdCompat.Damage(
            choiceContext,
            targets,
            damage,
            ValueProp.Unpowered,
            command.Attacker,
            null);
    }

    public override Task AfterStun(Creature creature)
    {
        if (Mode != SilentOrchestraPageMode.Finale
            || _applyingFinaleStun
            || creature is not LibraryCreature
                {
                    HasChaoResistance: true,
                    IsAlive: true
                }
            || AllyTurnRegistry.IsAllyCreature(creature)
            || creature.CombatState is not { } combatState
            || creature.CombatId is not uint combatId)
        {
            return Task.CompletedTask;
        }

        int round = Math.Max(1, combatState.RoundNumber);
        if (FinaleTrackedRound != round)
        {
            FinaleTrackedRound = round;
            FinaleStunnedThisRoundCombatIds = [];
        }

        int id = checked((int)combatId);
        if (FinaleStunnedThisRoundCombatIds.Contains(id))
        {
            return Task.CompletedTask;
        }

        FinaleStunnedThisRoundCombatIds =
            [.. FinaleStunnedThisRoundCombatIds, id];
        FinalePendingCombatIds = [.. FinalePendingCombatIds, id];
        FinalePendingRounds =
        [
            .. FinalePendingRounds,
            round + (combatState.CurrentSide == CombatSide.Enemy ? 2 : 1)
        ];
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        _ = choiceContext;
        _ = participants;
        if (Mode != SilentOrchestraPageMode.Finale
            || side != CombatSide.Player)
        {
            return;
        }

        SilentOrchestraPageRelic[] holders = GetModeRelics(
            combatState,
            SilentOrchestraPageMode.Finale);
        if (holders.Length == 0 || !ReferenceEquals(this, holders[0]))
        {
            return;
        }

        int round = Math.Max(1, combatState.RoundNumber);
        int[] dueIds = holders
            .SelectMany(holder => holder.GetDueFinaleCombatIds(round))
            .Distinct()
            .ToArray();
        foreach (SilentOrchestraPageRelic holder in holders)
        {
            holder.RemoveDueFinaleEntries(round);
        }

        if (dueIds.Length == 0)
        {
            return;
        }

        LibraryCreature[] targets = combatState.Enemies
            .OfType<LibraryCreature>()
            .Where(enemy =>
                enemy.IsAlive
                && enemy.HasChaoResistance
                && !AllyTurnRegistry.IsAllyCreature(enemy)
                && enemy.CombatId is { } id
                && dueIds.Contains(checked((int)id)))
            .OrderBy(static enemy => enemy.CombatId ?? uint.MaxValue)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        foreach (var holder in holders)
        {
            holder._applyingFinaleStun = true;
            holder.Flash(targets);
        }

        try
        {
            foreach (var target in targets)
            {
                await CreatureCmd.Stun(target);
            }
        }
        finally
        {
            foreach (var holder in holders)
            {
                holder._applyingFinaleStun = false;
                holder.UpdateModeUiState();
            }
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _ = room;
        ResetFinaleCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        Owner.RunState.CreateCard<
            SilentOrchestraEverRepeatingPerformanceChoiceCard>(Owner),
        Owner.RunState.CreateCard<
            SilentOrchestraFerventAdorationChoiceCard>(Owner),
        Owner.RunState.CreateCard<SilentOrchestraFinaleChoiceCard>(Owner)
    ];

    private static bool IsHostileEnemyAttack(AttackCommand command)
    {
        Creature? attacker = command.Attacker;
        return attacker is
            {
                IsAlive: true,
                IsPlayer: false,
                Monster: not null
            }
            && !AllyTurnRegistry.IsAllyCreature(attacker)
            && ValuePropCompat.IsPoweredAttack(command.DamageProps);
    }

    private static SilentOrchestraPageRelic[] GetModeRelics(
        CombatStateLike combatState,
        SilentOrchestraPageMode mode)
    {
        return combatState.Players
            .OrderBy(static player => player.NetId)
            .Where(static player => player.IsActiveForHooks)
            .Select(static player =>
                player.GetRelic<SilentOrchestraPageRelic>())
            .Where(relic => relic?.Mode == mode)
            .Cast<SilentOrchestraPageRelic>()
            .ToArray();
    }

    private int[] GetDueFinaleCombatIds(int round)
    {
        int count = Math.Min(
            FinalePendingCombatIds.Length,
            FinalePendingRounds.Length);
        return Enumerable.Range(0, count)
            .Where(index => FinalePendingRounds[index] <= round)
            .Select(index => FinalePendingCombatIds[index])
            .ToArray();
    }

    private void RemoveDueFinaleEntries(int round)
    {
        int count = Math.Min(
            FinalePendingCombatIds.Length,
            FinalePendingRounds.Length);
        int[] keep = Enumerable.Range(0, count)
            .Where(index => FinalePendingRounds[index] > round)
            .ToArray();
        FinalePendingCombatIds = keep
            .Select(index => FinalePendingCombatIds[index])
            .ToArray();
        FinalePendingRounds = keep
            .Select(index => FinalePendingRounds[index])
            .ToArray();
        UpdateModeUiState();
    }

    private void ResetFinaleCombatState()
    {
        FinaleTrackedRound = 0;
        FinaleStunnedThisRoundCombatIds = [];
        FinalePendingCombatIds = [];
        FinalePendingRounds = [];
        _applyingFinaleStun = false;
    }

    protected override void ResetStateOnModeSet(SilentOrchestraPageMode mode) => ResetFinaleCombatState();

    protected override void ResetStateOnFallback() => ResetFinaleCombatState();

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == SilentOrchestraPageMode.Finale
            && CombatManager.Instance.IsInProgress
            && FinalePendingCombatIds.Length > 0
                ? RelicStatus.Active
                : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    [AbnormalityPagePostObtainEffect(
        (int)SilentOrchestraPageMode.EverRepeatingPerformance)]
    private async Task AddEverRepeatingPerformanceCardOnPickup()
    {
        CardModel card = Owner.RunState.CreateCard<
            EverRepeatingPerformanceCard>(Owner);
        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.Add(card, PileType.Deck));
        SaveManager.Instance.MarkCardAsSeen(card);
    }
}

public enum SilentOrchestraPageMode
{
    None = 0,
    EverRepeatingPerformance = 1,
    FerventAdoration = 2,
    Finale = 3
}

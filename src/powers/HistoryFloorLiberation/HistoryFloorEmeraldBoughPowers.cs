using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers.HistoryFloorLiberation;

public sealed class EmeraldBoughVineBarrierPower : LibraryOfRuinaPowerModel
{
    public const int RequiredKills = 4;

    protected override string LegacyPowerId => "EMERALD_BOUGH_VINE_BARRIER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Required", RequiredKills)
    ];

    
    
    
    
    
    

    
    

    public override bool ShouldAllowTargeting(Creature target)
    {
        if (target != Owner)
        {
            return true;
        }

        return Amount >= RequiredKills;
    }

    public async Task RegisterVineBarrierKill()
    {
        if (Owner.IsDead || Amount >= RequiredKills)
        {
            return;
        }

        Flash();
        int next = Math.Min(RequiredKills, Amount + 1);
        await PowerCmdCompat.ModifyAmount(this, next - Amount, Owner, null, silent: true);
        
        if (next >= RequiredKills)
        {
            Owner.GetPower<UntargetablePower>()?.RemoveInternal();
        }
    }
}

public sealed class EmeraldBoughForestApplePower : LibraryOfRuinaPowerModel
{
    private const int ReducedMaxResistance = 15;
    private const int VinelessTurnsBeforeRespawn = 2;
    private const int FullMaxResistance = HistoryFloorEmeraldBoughBoss.StaggerResistance;
    private const int ReductionPercent = (FullMaxResistance - ReducedMaxResistance) * 100 / FullMaxResistance;

    private sealed class Data
    {
        public int VinelessEnemyTurnCount;
        public int EnemyTurnsElapsed;
    }

    protected override string LegacyPowerId => "EMERALD_BOUGH_FOREST_APPLE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Reduced", ReducedMaxResistance),
        new DynamicVar("ReductionPercent", ReductionPercent),
        new DynamicVar("Turns", VinelessTurnsBeforeRespawn)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStaggerResistancePower>()
    ];

    protected override object InitInternalData() => new Data();

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.IsDead || side != CombatSide.Enemy)
        {
            return;
        }

        CombatStateLike? combatState = Owner.CombatState;
        if (combatState == null)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        data.EnemyTurnsElapsed++;
        if (data.EnemyTurnsElapsed <= 1)
        {
            return;
        }

        int vineCount = CountAliveVineBarriers(combatState);

        if (vineCount > 0)
        {
            data.VinelessEnemyTurnCount = 0;
            await EnsureMaxResistance(HistoryFloorEmeraldBoughBoss.StaggerResistance);
            return;
        }

        data.VinelessEnemyTurnCount = Math.Min(VinelessTurnsBeforeRespawn, data.VinelessEnemyTurnCount + 1);
        if (data.VinelessEnemyTurnCount >= VinelessTurnsBeforeRespawn)
        {
            data.VinelessEnemyTurnCount = 0;
            if (combatState.Encounter is HistoryFloorLiberationEncounter encounter)
            {
                await encounter.TrySpawnEmeraldBoughVineBarriers(combatState);
            }
            await EnsureMaxResistance(HistoryFloorEmeraldBoughBoss.StaggerResistance);
        }
        else
        {
            await ReduceMaxResistance(ReducedMaxResistance);
        }
    }

    public async Task RefreshVineState()
    {
        if (Owner.IsDead || Owner.CombatState is not { } combatState)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        int vineCount = CountAliveVineBarriers(combatState);
        if (vineCount > 0)
        {
            data.VinelessEnemyTurnCount = 0;
            await EnsureMaxResistance(HistoryFloorEmeraldBoughBoss.StaggerResistance);
        }
        else
        {
            await ReduceMaxResistance(ReducedMaxResistance);
        }
    }

    private async Task EnsureMaxResistance(int maxResistance)
    {
        if (Owner is LibraryCreature lc)
        {
            await LibraryCreatureCmd.SetMaxChaoValue(lc, maxResistance);
        }
    }

    private async Task ReduceMaxResistance(int maxResistance)
    {
        if (Owner is LibraryCreature lc)
        {
            await LibraryCreatureCmd.SetMaxChaoValue(lc, maxResistance);
            if (lc.CurrentChaoValue > maxResistance)
            {
                await LibraryCreatureCmd.SetCurrentChaoValue(lc, maxResistance);
            }
        }
    }

    private static int CountAliveVineBarriers(CombatStateLike combatState)
    {
        return combatState.Enemies.Count(static enemy => enemy.IsAlive && enemy.Monster is HistoryFloorVineBarrier);
    }
}

public sealed class EmeraldBoughWhereAreYouPower : LibraryOfRuinaPowerModel
{
    private sealed class Data
    {
        public bool ProcessedThisPlayerTurn;
    }

    protected override string LegacyPowerId => "EMERALD_BOUGH_WHERE_ARE_YOU_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", EmeraldBoughStranglingVinePower.MaxBindStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBindingPower>(),
        HoverTipFactory.FromPower<EmeraldBoughStranglingVinePower>()
    ];

    protected override object InitInternalData() => new Data();

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Owner.IsDead || side != CombatSide.Player)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        if (data.ProcessedThisPlayerTurn)
        {
            return;
        }

        data.ProcessedThisPlayerTurn = true;
        await TickStranglingVinesAndApplyBind(combatState);
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Enemy)
        {
            GetInternalData<Data>().ProcessedThisPlayerTurn = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.IsDead)
        {
            return;
        }

        CombatStateLike? combatState = Owner.CombatState;
        if (combatState == null)
        {
            return;
        }

        int maxAmount = combatState.Enemies
            .Where(static enemy => enemy.IsAlive && enemy.Monster is HistoryFloorVineBarrier)
            .Select(static enemy => enemy.GetPower<EmeraldBoughStranglingVinePower>()?.Amount ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        if (maxAmount >= EmeraldBoughStranglingVinePower.MaxBindStacks
            && Owner.Monster is HistoryFloorEmeraldBoughBoss boss)
        {
            await boss.QueueShatteredLife();
        }
    }

    private async Task TickStranglingVinesAndApplyBind(CombatStateLike combatState)
    {
        IReadOnlyList<EmeraldBoughStranglingVinePower> vinePowers = combatState.Enemies
            .Where(static enemy => enemy.IsAlive && enemy.Monster is HistoryFloorVineBarrier)
            .Select(static enemy => enemy.GetPower<EmeraldBoughStranglingVinePower>())
            .OfType<EmeraldBoughStranglingVinePower>()
            .ToArray();

        if (vinePowers.Count == 0)
        {
            return;
        }

        int maxAmount = 0;
        foreach (EmeraldBoughStranglingVinePower vine in vinePowers)
        {
            vine.AdvanceCount();
            maxAmount = Math.Max(maxAmount, vine.Amount);
        }

        if (maxAmount <= 0)
        {
            return;
        }

        IReadOnlyList<Creature> players = combatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();

        foreach (Creature player in players)
        {
            int existing = player.GetPower<LibraryBindingPower>()?.Amount ?? 0;
            int delta = maxAmount - existing;
            if (delta > 0)
            {
                await PowerCmdCompat.Apply<LibraryBindingPower>(player, delta, Owner, null);
            }
        }

        if (maxAmount >= EmeraldBoughStranglingVinePower.MaxBindStacks
            && Owner.Monster is HistoryFloorEmeraldBoughBoss boss)
        {
            await boss.QueueShatteredLife();
        }
    }
}

public sealed class EmeraldBoughStranglingVinePower : LibraryOfRuinaPowerModel
{
    public const int MaxBindStacks = 3;

    protected override string LegacyPowerId => "EMERALD_BOUGH_STRANGLING_VINE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Max", MaxBindStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBindingPower>()
    ];

    public void AdvanceCount()
    {
        if (Owner.IsDead)
        {
            return;
        }

        int next = Math.Min(MaxBindStacks, Amount + 1);
        if (next == Amount)
        {
            return;
        }

        Flash();
        SetAmount(next, silent: true);
    }
}

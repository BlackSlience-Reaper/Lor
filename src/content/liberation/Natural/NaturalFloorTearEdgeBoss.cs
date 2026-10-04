using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class NaturalFloorTearEdgeBoss : NaturalFloorDespairMonster, ILiberationPrimaryPhaseBoss, ILibraryAbstractModel
{
    private static readonly string[] Ids = ["GRANT_TEARDROP", "SHELTERING_UNKNOWN", "PHASE_END"];

    protected override string[] MoveIds => Ids;

    public int LiberationPhase => 3;

    public const int PlatingAmount = 4; // 未知：为其他剑施加的覆甲层数。

    public const int TeardropInterval = 3; // 赋予泪滴：发动间隔的行动次数。

    [SavedProperty]
    public int CompletedActions { get; private set; }

    [SavedProperty]
    public int DespairDueRound { get; private set; }

    [SavedProperty]
    public int BrokenHeartDueRound { get; private set; }

    [SavedProperty]
    public bool IsInDespair { get; private set; }

    [SavedProperty]
    public bool BrokenHeartActive { get; private set; }

    [SavedProperty]
    public int StabCount { get; private set; }

    [SavedProperty]
    public int LastPlayerRound { get; private set; } = -1;

    [SavedProperty]
    public bool PhaseEndPending { get; private set; }

    private LocalOggLoopPlayer.LoopHandle? _crying;

    public override int MinInitialHp =>
        HpValue(
            190, // 初始体力下限：普通。
            196); // 初始体力下限：ToughEnemies 进阶。

    public override int MaxInitialHp =>
        HpValue(
            194, // 初始体力上限：普通。
            200); // 初始体力上限：ToughEnemies 进阶。

    public override int DefaultChaoResistance => 500; // 初始混乱抗性。

    public override LibraryCreatureResistanceData.Resistance DefaultPhysicalResistanceData =>
        new(LibraryResistanceLevel.Endure);

    public override LibraryCreatureResistanceData.Resistance DefaultChaoResistanceData =>
        new(LibraryResistanceLevel.Endure);

    protected override IEnumerable<string> VisualAssets =>
        NaturalFloorTearEdgeVisuals.AssetPaths
            .Concat(NaturalFloorDespairVideoController.AssetPaths);

    internal bool IsExposed =>
        BrokenHeartActive
        && Creature is LibraryCreature { IsChaoed: true };

    internal string VisualForm
    {
        get
        {
            if (StabCount > 0)
            {
                return "stabbed" + Math.Min(StabCount, 3);
            }

            if (IsInDespair)
            {
                return "despair";
            }

            return "normal";
        }
    }

    internal NaturalFloorForgottenSword[] Swords =>
        Creature.CombatState?.Enemies
            .Select(static creature => creature.Monster)
            .OfType<NaturalFloorForgottenSword>()
            .OrderBy(static sword => sword.SwordIndex)
            .ToArray()
        ?? [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _crying = null;
    }

    protected override void RebaseRounds(int offset)
    {
        if (DespairDueRound > 0)
        {
            DespairDueRound += offset;
        }

        if (BrokenHeartDueRound > 0)
        {
            BrokenHeartDueRound += offset;
        }

        if (LastPlayerRound >= 0)
        {
            LastPlayerRound += offset;
        }
    }

    protected override int SelectMove()
    {
        if (PhaseEndPending)
        {
            return 2;
        }

        if (CompletedActions % TeardropInterval == 0)
        {
            return 0;
        }

        return 1;
    }

    protected override IEnumerable<AbstractIntent> CreateIntents(int move) => move switch
    {
        0 =>
        [
            new DetailedBuffIntent<NaturalFloorTeardropPower>(
                1,
                DetailedBuffTargetScope.RandomEnemy,
                descriptionKey: "NATURAL_TEAR_GRANT.description")
        ],
        1 =>
        [
            new UnknownIntent(),
            new DetailedBuffIntent<PlatingPower>(
                PlatingAmount,
                DetailedBuffTargetScope.OtherEnemies,
                descriptionKey: "NATURAL_TEAR_SHELTER.description")
        ],
        _ => [new HealIntent(), new BuffIntent()]
    };

    protected override async Task ApplyPassives()
    {
        await PowerCmdCompat.Ensure<HistoryFloorCorrosionPower>(Creature, 1, Creature, null, silent: true);
        //await PowerCmdCompat.Ensure<NaturalFloorTearUntargetablePower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorDespairPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorDespairProtectionPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorBrokenHeartPower>(Creature, 1, Creature, null, silent: true);
        await SyncUntargetablePower();
    }

    private async Task SyncUntargetablePower()
    {
        if (Creature.CombatState == null || Creature.IsDead)
        {
            return;
        }

        // Legacy Spider Bud powers derive from the shared power, so snapshots
        // with either ID follow the same exposure rule and keep at most one lock.
        UntargetablePower[] locks = Creature.Powers.OfType<UntargetablePower>().ToArray();
        int keepCount = IsExposed ? 0 : 1;
        foreach (UntargetablePower power in locks.Skip(keepCount))
        {
            await PowerCmd.Remove(power);
        }

        if (!IsExposed)
        {
            await PowerCmdCompat.Ensure<UntargetablePower>(Creature, 1, Creature, null, silent: true);
        }
    }

    public async Task AfterStun(Creature creature)
    {
        if (creature == Creature)
        {
            await SyncUntargetablePower();
        }
    }

    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext context,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEndLate(context, side, participants);
        // LibraryMonsterModel restores stagger during AfterSideTurnEnd.
        await SyncUntargetablePower();
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (IsInDespair)
        {
            StartCrying();
        }

        if (PhaseEndPending)
        {
            ForceMove(2);
        }
    }

    internal void QueueDespair()
    {
        if (!PhaseEndPending && !IsInDespair && DespairDueRound == 0)
        {
            DespairDueRound = (Creature.CombatState?.RoundNumber ?? 0) + 1;
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext context,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        await base.BeforeSideTurnStart(context, side, participants, combatState);
        await SyncUntargetablePower();
        if (side != CombatSide.Player || !CanAct || LastPlayerRound == combatState.RoundNumber)
        {
            return;
        }

        LastPlayerRound = combatState.RoundNumber;
        // 反刺在绝望行动中发生，下一次玩家回合开始立即结算破碎的心。
        bool resolveBrokenHeart = IsInDespair && StabCount > 0;
        if (IsInDespair)
        {
            IsInDespair = false;
            StopCrying();
            foreach (NaturalFloorForgottenSword sword in Swords)
            {
                sword.EndDespair();
            }
        }
        if (BrokenHeartActive && Creature is LibraryCreature { IsChaoed: false })
        {
            BrokenHeartActive = false;
            StabCount = 0;
        }
        if (resolveBrokenHeart || BrokenHeartDueRound > 0 && BrokenHeartDueRound <= LastPlayerRound)
        {
            BrokenHeartDueRound = 0;
            BrokenHeartActive = true;
            Sound("broken");
            // 与绝望骑士一致：先解除无法选中，避免通用交互补丁拦截混乱命令。
            foreach (UntargetablePower power in Creature.Powers.OfType<UntargetablePower>().ToArray())
            {
                await PowerCmd.Remove(power);
            }

            if (Creature is LibraryCreature library)
            {
                await LibraryCreatureCmd.SetCurrentChaoValue(library, 0m);
                if (!library.IsChaoed)
                {
                    await LibraryCreatureCmd.Stun(library, Ids[SelectMove()]);
                }

                if (!Creature.IsStunned)
                {
                    // 常规招式要求至少执行一次，破碎的心必须强制打断待执行招式。
                    SetMoveImmediate(new MoveState("STUNNED", static _ => Task.CompletedTask, new StunIntent())
                    {
                        FollowUpStateId = Ids[SelectMove()],
                        MustPerformOnceBeforeTransitioning = true
                    }, forceTransition: true);
                }

                library.HealthBar?.RefreshValues();
            }
            await SyncUntargetablePower();
        }
        else if (DespairDueRound > 0 && DespairDueRound <= LastPlayerRound)
        {
            DespairDueRound = 0;
            IsInDespair = true;
            StabCount = 0;
            Sound("despair");
            StartCrying();
            foreach (NaturalFloorForgottenSword sword in Swords)
            {
                await sword.EnterDespair();
            }
        }
        foreach (NaturalFloorForgottenSword sword in Swords)
        {
            await sword.TickFalseDeath(LastPlayerRound);
        }

        if (Creature.IsStunned && Creature is LibraryCreature { IsChaoed: false })
        {
            // 混乱结束后恢复对应行动；混乱期间保留原有混乱动作。
            ForceMove(SelectMove());
        }
    }

    protected override async Task PerformMove(int move, IReadOnlyList<Creature> targets)
    {
        if (move == 2)
        {
            if (Encounter is { } encounter)
            {
                if (encounter.CurrentPhase == 4 && encounter.TransitionPending)
                {
                    await encounter.CompletePhaseTransition();
                }
                else
                {
                    await encounter.CompleteThirdPhase();
                }
            }

            return;
        }
        if (!CanAct || Creature.IsStunned)
        {
            return;
        }

        await CreatureCmd.TriggerAnim(Creature, "Cast", AttackTime);
        if (move == 0)
        {
            NaturalFloorForgottenSword[] candidates = Swords
                .Where(static sword => sword.CanReceiveTeardrop)
                .ToArray();
            if (candidates.Length > 0 && RunRng.MonsterAi.NextItem(candidates) is { } sword)
            {
                await sword.ApplyTeardrop();
            }
        }
        else
        {
            Sound("guard");
            foreach (NaturalFloorForgottenSword sword in Swords.Where(static s => s.Creature.IsAlive && !s.IsFakeDead))
            {
                await PowerCmdCompat.Apply<PlatingPower>(sword.Creature, PlatingAmount, Creature, null);
            }
        }
        CompletedActions++;
    }

    internal async Task ReceiveSwordStab(int percent)
    {
        if (!CanAct || !IsInDespair || PhaseEndPending)
        {
            return;
        }

        StabCount = Math.Min(3, StabCount + 1);
        BrokenHeartDueRound = (Creature.CombatState?.RoundNumber ?? 0) + 1;
        try
        {
            await NaturalFloorDespairVideoController.PlayAsync(StabCount, () => Sound("stab"));
        }
        catch (Exception exception)
        {
            Log.Warn("[NaturalFloorDespair] Insertion video failed: " + exception.Message);
        }

        if (Creature.CombatState == null || Encounter is { SettlementTriggered: true })
        {
            return;
        }

        decimal hpLoss = Math.Ceiling(Creature.MaxHp * percent / 100m);
        decimal remainingHp = Math.Max(0, Creature.CurrentHp - hpLoss);
        await CreatureCmd.SetCurrentHp(Creature, remainingHp);
    }

    public override Task AfterDeath(PlayerChoiceContext context, Creature creature, bool prevented, float animLength)
    {
        if (creature == Creature && !prevented && Encounter is { SettlementTriggered: false })
        {
            PhaseEndPending = true;
            StopCrying();
            ForceMove(2);
        }
        return Task.CompletedTask;
    }

    protected override Task AfterSideTurnEndInternal(
        PlayerChoiceContext context,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (PhaseEndPending && Encounter is { CurrentPhase: 3 } encounter)
        {
            return encounter.CompleteThirdPhase();
        }

        return Task.CompletedTask;
    }

    public Task TriggerReviveAndEmpowerState()
    {
        ForceReviveAndEmpowerState();
        return Task.CompletedTask;
    }

    public void ForceReviveAndEmpowerState()
    {
        ForceMove(2);
    }

    private void StartCrying()
    {
        if (_crying == null)
        {
            _crying = LocalOggLoopPlayer.StartLoop(SfxRoot + "crying.ogg", -5f);
        }
    }

    private void StopCrying()
    {
        _crying?.Dispose();
        _crying = null;
    }

    public override void BeforeRemovedFromRoom()
    {
        StopCrying();
        base.BeforeRemovedFromRoom();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        StopCrying();
        NaturalFloorDespairVideoController.Stop();
        return base.AfterCombatEnd(room);
    }
}

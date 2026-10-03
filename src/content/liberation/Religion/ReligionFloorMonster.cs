using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using LibraryOfRuina.framework.monsters;

namespace LibraryOfRuina.content.liberation.Religion;

public abstract class ReligionFloorMonster : LorMonsterModel
{
    internal const string RouterId = "RELIGION_ROUTER";
    internal const int WaitMove = 3;
    internal const int SalvationMove = 4;
    internal const int ExplosionMove = 5;
    private static readonly string[] MoveIds = ["MOVE_1", "MOVE_2", "MOVE_3", "WAIT", "SALVATION", "EXPLOSION"];
    private Dictionary<int, MoveState> _moves = [];

    [SavedProperty]
    public int PlannedMove { get; private set; }

    [SavedProperty]
    public int PlannedDamage { get; private set; }

    internal ReligionFloorLiberationEncounter? Encounter =>
        Creature?.CombatState?.Encounter as ReligionFloorLiberationEncounter;

    internal abstract string AssetName { get; }

    public override bool HasDeathSfx => false;

    public override bool ShouldDisappearFromDoom => false;

    public override string? StunRecoveryStateId => RouterId;

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Append(ReligionFloorAssets.CreatureScene(AssetName));

    protected override bool ShouldShowMoveInBestiary(string id) =>
        id is "MOVE_1" or "MOVE_2" or "MOVE_3";

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _moves = [];
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _moves = [];
        var router = new DelegatingMonsterRouterState(RouterId, (_, _) => MoveIds[PlannedMove]);
        for (int index = 0; index < MoveIds.Length; index++)
        {
            int move = index;
            MoveState state;
            if (move >= SalvationMove)
            {
                state = new LibraryPhaseTransitionMoveState(MoveIds[index], _ => ExecuteMove(move), CreateIntents(move).ToArray());
            }
            else
            {
                state = new MoveState(MoveIds[index], _ => ExecuteMove(move), CreateIntents(move).ToArray());
            }
            state.FollowUpState = router;
            state.MustPerformOnceBeforeTransitioning = true;
            _moves.Add(index, state);
        }

        return new MonsterMoveStateMachine(_moves.Values.Cast<MonsterState>().Append(router), router);
    }

    internal void PlanMove(int move, bool rollDamage = true)
    {
        AssertMutable();
        PlannedMove = move;
        if (rollDamage)
        {
            (int min, int max, _) = AttackValues(move);
            PlannedDamage = max > 0 ? RunRng.MonsterAi.NextInt(min, max + 1) : 0;
        }

        _ = MoveStateMachine;
        SetMoveImmediate(_moves[move], forceTransition: true);
    }

    internal virtual (int Min, int Max, int Hits) AttackValues(int move) => (0, 0, 0);

    protected abstract IEnumerable<AbstractIntent> CreateIntents(int move);

    protected abstract Task PerformReligionMove(int move, IReadOnlyList<Creature> targets);

    private async Task ExecuteMove(int move)
    {
        if (move >= WaitMove || Encounter is not { IsSettling: false, Completed: false })
        {
            return;
        }

        if (this is ReligionFloorApostle apostle && !apostle.CanActThisTurn)
        {
            return;
        }

        // 宗教层对玩家的招式统一作用于全体存活玩家，按战斗状态顺序解析目标。
        Creature[] targets = Creature.CombatState!.PlayerCreatures
            .Where(static target => target.IsAlive)
            .ToArray();
        await PerformReligionMove(move, targets);
    }

    protected async Task AttackPlayers(int move, IReadOnlyList<Creature> targets, string animation)
    {
        int hits = AttackValues(move).Hits;
        for (int hit = 0; hit < hits; hit++)
        {
            if (!Creature.IsAlive || Encounter is not { IsSettling: false, Completed: false }
                || this is ReligionFloorApostle { CanActThisTurn: false })
            {
                break;
            }
            Creature[] livingTargets = targets.Where(static target => target.IsAlive).ToArray();
            if (livingTargets.Length == 0)
            {
                break;
            }

            using (TargetedMonsterAttackHelper.ForceTargets(Creature, livingTargets))
            {
                ReligionFloorPresentation.PlayAttackStart(this, animation);
                await LibraryDamageCmd.Attack(PlannedDamage)
                    .FromMonster(this)
                    .WithAttackerAnim(animation, ReligionFloorRules.FrameSeconds)
                    .AfterAttackerAnim(() => ReligionFloorPresentation.PlayAttackImpact(this))
                    .Execute(null);
            }
        }
    }

    protected AbstractIntent AttackIntent(int move) => new ReligionFloorAttackIntent(
        () => IsMutable ? PlannedDamage : AttackValues(move).Max,
        () => AttackValues(move).Hits);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (this is ReligionFloorLostParadise)
        {
            await PowerCmdCompat.Ensure<ReligionFloorImmunityPower>(Creature);
            await PowerCmdCompat.Ensure<ReligionFloorRipeTimePower>(Creature);
        }
        else
        {
            await PowerCmdCompat.Ensure<ReligionFloorImmortalityPower>(Creature);
        }
    }
}

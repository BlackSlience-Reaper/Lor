using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.content.specialguests.Rnfmabj;

/// <summary>
/// Rnfmabj 本体与双手共用的多意图计划：计划由本体在玩家回合开始时统一规划（双手的 <c>PlanRound</c> 也由本体调用），
/// 攻击招式的伤害在规划时就掷好存进 <see cref="PlannedDamageValues"/>，意图显示与执行用同一个数。
/// </summary>
public abstract class RnfmabjMonsterBase : SpecialGuestMonsterBase
{
    private const string CompositeMoveId = "RNFMABJ_COMPOSITE";
    private const string RouterMoveId = "RNFMABJ_ROUTER";
    private const string HiddenMoveId = "RNFMABJ_HIDDEN";
    private const int DamageValuesPerSlot = 3;

    private PlannedMoveController<RnfmabjMove>? _plan;

    public int LastPlannedRound { get; private set; } = -1;

    public int PlanSerial { get; protected set; }

    public int[] PlannedDamageValues { get; private set; } =
        Enumerable.Repeat(-1, StoredIntentSlots * DamageValuesPerSlot).ToArray();

    protected virtual bool CanPerformMoves => true;

    // 计划存在基类的五个槽位里；实例随怪物克隆丢弃、用到时重建（见 PlannedMoveController）。
    private PlannedMoveController<RnfmabjMove> Plan => _plan ??= new(
        this,
        StoredIntentSlots,
        slot => (RnfmabjMove)GetStoredIntent(slot),
        (slot, move) => SetStoredIntent(slot, (int)move),
        (slot, move) => CreateIntent(move, slot),
        RnfmabjMove.None);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await RnfmabjPassiveInstaller.EnsureFor(this);
        await InstallFormationPowers();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _plan = null;
        PlannedDamageValues = (int[])PlannedDamageValues.Clone();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState compositeState = Plan.CreateCompositeState(
            CompositeMoveId,
            PerformCompositeMove,
            mustPerformOnce: true);
        MoveState hiddenState = Plan.CreateHiddenState(HiddenMoveId);

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (owner, rng) =>
            {
                _ = owner;
                _ = rng;
                return ResolveFollowUpMoveId();
            });
        compositeState.FollowUpState = router;
        hiddenState.FollowUpState = router;

        return new MonsterMoveStateMachine(
            [compositeState, hiddenState, router],
            !CanPerformMoves
                ? hiddenState
                : HasStoredIntentPlan
                    ? compositeState
                    : router);
    }

    private string ResolveFollowUpMoveId()
    {
        if (!CanPerformMoves)
        {
            return HiddenMoveId;
        }

        if (HasStoredIntentPlan)
        {
            Plan.RefreshIntents();
            return CompositeMoveId;
        }

        return HiddenMoveId;
    }

    protected void InstallPlan(
        IReadOnlyList<RnfmabjMove> moves,
        Rng rng,
        int roundNumber)
    {
        int count = Math.Min(
            moves.Count,
            Math.Min(IntentCapacity, StoredIntentSlots));
        var damageValues = Enumerable.Repeat(
            -1,
            StoredIntentSlots * DamageValuesPerSlot).ToArray();

        Plan.WriteSlots(count, slot => moves[slot]);
        for (int slot = 0; slot < count; slot++)
        {
            RnfmabjMove move = moves[slot];
            if (move == RnfmabjMove.None)
            {
                continue;
            }

            RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
            if (!definition.IsAttack)
            {
                continue;
            }

            int rolled = rng.NextInt(
                definition.MinimumDamage,
                definition.MaximumDamage + 1);
            for (int hit = 0; hit < definition.Hits; hit++)
            {
                // Flurry intentionally displays and executes one synchronized
                // roll three times, matching the (5-6)x3 contract.
                damageValues[DamageIndex(slot, hit)] = rolled;
            }
        }

        PlannedDamageValues = damageValues;
        LastPlannedRound = roundNumber;
        PlanSerial++;
        HasCompletedFirstTurn = true;
        Plan.RefreshIntents();
        RevealPlan();
    }

    protected RnfmabjMove GetPlannedMove(int slot) => Plan.GetMove(slot);

    protected int GetPlannedDamage(int slot, int hit = 0)
    {
        int index = DamageIndex(slot, hit);
        return index >= 0 && index < PlannedDamageValues.Length
            ? Math.Max(0, PlannedDamageValues[index])
            : 0;
    }

    protected void RemovePlannedMoveAt(int removedSlot)
    {
        if (removedSlot < 0 || removedSlot >= StoredIntentSlots)
        {
            throw new ArgumentOutOfRangeException(
                nameof(removedSlot),
                removedSlot,
                null);
        }

        int[] shiftedDamage = (int[])PlannedDamageValues.Clone();
        for (int slot = removedSlot; slot < StoredIntentSlots - 1; slot++)
        {
            SetStoredIntent(slot, GetStoredIntent(slot + 1));
            for (int hit = 0; hit < DamageValuesPerSlot; hit++)
            {
                shiftedDamage[DamageIndex(slot, hit)] =
                    shiftedDamage[DamageIndex(slot + 1, hit)];
            }
        }

        SetStoredIntent(StoredIntentSlots - 1, (int)RnfmabjMove.None);
        for (int hit = 0; hit < DamageValuesPerSlot; hit++)
        {
            shiftedDamage[DamageIndex(StoredIntentSlots - 1, hit)] = -1;
        }

        PlannedDamageValues = shiftedDamage;
        Plan.RefreshIntents();
    }

    protected async Task RefreshPlanDisplay()
    {
        // RevealPlan switches the move state machine (SetMoveImmediate) and stays on the normal
        // exception path; only the intent node refresh is local presentation.
        Plan.RefreshIntents();
        RevealPlan();
        await PresentationGuard.RunAsync(async () =>
        {
            if (NCombatRoom.Instance?.GetCreatureNode(Creature) is { } node)
            {
                await node.RefreshIntents();
            }
        }, "Rnfmabj intent refresh");
    }

    protected void HidePlan() => Plan.Hide();

    protected void RevealPlan()
    {
        if (Plan.CompositeState != null && CanPerformMoves && HasStoredIntentPlan)
        {
            Plan.Reveal();
        }
        else
        {
            HidePlan();
        }
    }

    protected abstract AbstractIntent CreateIntent(RnfmabjMove move, int slot);

    protected abstract Task PerformPlannedMove(int slot, RnfmabjMove move);

    protected async Task<IReadOnlyList<Creature>> AttackAllPlayers(
        int slot,
        RnfmabjMove move)
    {
        RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
        if (Creature.IsDead || Creature.CombatState == null)
        {
            return [];
        }

        IReadOnlyList<Creature> attackedPlayers = Creature.CombatState.PlayerCreatures
            .Where(static target => target.IsAlive)
            .OrderBy(static target => target.CombatId)
            .ToArray();
        if (!string.IsNullOrWhiteSpace(definition.WindupSfx))
        {
            LocalOggOneShotPlayer.Play(definition.WindupSfx, -2f);
        }
        if (!Creature.IsAlive || !CanPerformMoves)
        {
            return attackedPlayers;
        }

        await CreatureCmd.TriggerAnim(Creature, definition.Animation, 0f);
        await Cmd.Wait(definition.AnimationDelaySeconds);
        if (!Creature.IsAlive || !CanPerformMoves)
        {
            return attackedPlayers;
        }

        if (!definition.IsAttack)
        {
            if (!string.IsNullOrWhiteSpace(definition.ImpactSfx))
            {
                LocalOggOneShotPlayer.Play(definition.ImpactSfx, -2f);
            }

            return attackedPlayers;
        }

        int hitIndex = 0;
        AttackCommand attack = DamageCmd.Attack(GetPlannedDamage(slot))
            .WithHitCount(definition.Hits)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx(definition.DamageType == LibraryDamageType.Blunt
                ? "vfx/vfx_attack_blunt"
                : "vfx/vfx_attack_slash")
            .SpawningHitVfxOnEachCreature()
            .BeforeDamage(() =>
            {
                string? hitSfx = hitIndex % 2 == 1
                    ? definition.AlternateImpactSfx ?? definition.ImpactSfx
                    : definition.ImpactSfx;
                if (!string.IsNullOrWhiteSpace(hitSfx))
                {
                    LocalOggOneShotPlayer.Play(hitSfx, -2f);
                }
                hitIndex++;
                return Task.CompletedTask;
            });

        var command = await attack.Execute(null);
        RecordDirectAttackDamageDealt(
            AttackCommandCompat.Results(command)
                .Where(static result => result.Receiver.IsPlayer));

        return attackedPlayers;
    }

    protected async Task ApplyMoveEffectToPlayers(
        RnfmabjMove move,
        IReadOnlyList<Creature> attackedPlayers)
    {
        RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
        foreach (Creature player in attackedPlayers
                     .Where(static player => player.IsAlive)
                     .OrderBy(static player => player.CombatId))
        {
            switch (definition.Effect)
            {
                case RnfmabjMoveEffect.ApplyParalysisAndWeak:
                {
                    LibraryOfRuinaParalysisPower? paralysis =
                        await PowerCmdCompat.Apply<LibraryOfRuinaParalysisPower>(
                            player,
                            definition.EffectAmount,
                            Creature,
                            null);
                    paralysis?.SetTurnsRemaining(definition.EffectDurationTurns);
                    await LibraryPowerCmd.Apply<LibraryWeakPower>(
                        player,
                        definition.EffectAmount,
                        definition.EffectDurationTurns,
                        Creature,
                        null);
                    break;
                }
                case RnfmabjMoveEffect.ApplyCorrosion:
                    await PowerCmdCompat.Apply<RnfmabjCorrosionPower>(
                        player,
                        definition.EffectAmount,
                        Creature,
                        null);
                    break;
                case RnfmabjMoveEffect.TransformDrawPileCardToWound:
                    await TransformRandomDrawPileCardToWound(player);
                    break;
            }
        }
    }

    private static async Task TransformRandomDrawPileCardToWound(Creature player)
    {
        if (player.Player == null)
        {
            return;
        }

        IReadOnlyList<CardModel> candidates = PileType.Draw
            .GetPile(player.Player)
            .Cards
            .Select(static (card, index) => (card, index))
            .Where(static candidate =>
                candidate.card.IsTransformable && candidate.card is not Wound)
            .OrderBy(static candidate =>
                candidate.card.Id.Entry,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.index)
            .Select(static candidate => candidate.card)
            .ToArray();
        if (candidates.Count == 0)
        {
            return;
        }

        int index = player.Player.RunState.Rng.CombatCardSelection.NextInt(candidates.Count);
        await CardCmd.TransformTo<Wound>(candidates[index]);
    }

    private async Task PerformCompositeMove(IReadOnlyList<Creature> targets)
    {
        _ = targets;
        if (!CanPerformMoves)
        {
            ClearPlanAndHide();
            return;
        }

        await Plan.PerformPlan(
            () => Creature.IsAlive && CanPerformMoves,
            PerformPlannedMove);

        ClearConsumedPlan();
        if (!CanPerformMoves)
        {
            HidePlan();
        }
    }

    private void ClearConsumedPlan()
    {
        Plan.ClearSlots();
        PlannedDamageValues = Enumerable.Repeat(
            -1,
            StoredIntentSlots * DamageValuesPerSlot).ToArray();
        Plan.RefreshIntents();
    }

    protected void ClearPlanAndHide()
    {
        ClearConsumedPlan();
        HidePlan();
    }

    private static int DamageIndex(int slot, int hit) =>
        slot * DamageValuesPerSlot + Math.Clamp(hit, 0, DamageValuesPerSlot - 1);

    private async Task InstallFormationPowers()
    {
        if (Creature.CombatState == null)
        {
            return;
        }

        if (this is Rnfmabj)
        {
            foreach (Creature player in Creature.CombatState.PlayerCreatures
                         .Where(static player => player.IsAlive)
                         .OrderBy(static player => player.CombatId))
            {
                await PowerCmdCompat.Ensure<SurroundedPower>(
                    player,
                    1,
                    Creature,
                    null,
                    silent: true);
            }
            return;
        }

        await PowerCmdCompat.Ensure<MinionPower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        if (this is RnfmabjLeftHand)
        {
            await PowerCmdCompat.Ensure<BackAttackLeftPower>(
                Creature,
                1,
                Creature,
                null,
                silent: true);
        }
        else if (this is RnfmabjRightHand)
        {
            await PowerCmdCompat.Ensure<BackAttackRightPower>(
                Creature,
                1,
                Creature,
                null,
                silent: true);
        }
    }
}

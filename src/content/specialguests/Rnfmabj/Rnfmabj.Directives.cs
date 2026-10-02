using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.content.specialguests.Rnfmabj;

// 指令（Prescript）任务：状态是 Rnfmabj.cs 里 Directive* 开头的战斗内属性，规则在 RnfmabjDirectiveTracker。
public sealed partial class Rnfmabj
{
    public override async Task AfterCardPlayed(
        PlayerChoiceContext context,
        CardPlay cardPlay)
    {
        await base.AfterCardPlayed(context, cardPlay);
        if (Creature.IsDead
            || Creature.CombatState?.CurrentSide != CombatSide.Player
            || !TryGetCurrentDirectiveSequence(out CardType[] sequence)
            || CurrentDirectiveCompleted)
        {
            return;
        }

        IReadOnlyList<ulong> requiredPlayers = ParseLivingRequiredPlayerNetIds();
        ulong playerNetId = cardPlay.PlayerCompat().NetId;
        if (!requiredPlayers.Contains(playerNetId))
        {
            return;
        }

        int progress = GetDirectiveProgress(playerNetId);
        if (progress < sequence.Length)
        {
            CardType playedType = cardPlay.Card.Type;
            progress = playedType == sequence[progress]
                ? progress + 1
                : 0;
            SetDirectiveProgress(playerNetId, progress);
        }

        // 已完成的玩家进度锁定，等其他玩家。
        if (progress < sequence.Length)
        {
            return;
        }

        await TryCompleteCurrentDirective(sequence);
    }

    /// <summary>
    /// 玩家回合里有玩家死亡时复核指令：还没完成的队友死了，已完成的玩家可能不会再出牌，
    /// 只在出牌时复核的话指令就停在“全员完成”却不取消意图。死亡与存活都是同步的战斗状态，两端在同一次死亡结算里复核。
    /// 敌方回合不复核：那时意图正在执行，取消槽位会打乱本回合的出招。
    /// </summary>
    private async Task RecheckDirectiveAfterPlayerDeath(Creature creature)
    {
        if (!creature.IsPlayer
            || Creature.IsDead
            || Creature.CombatState?.CurrentSide != CombatSide.Player
            || CurrentDirectiveCompleted
            || !TryGetCurrentDirectiveSequence(out CardType[] sequence))
        {
            return;
        }

        await TryCompleteCurrentDirective(sequence);
    }

    /// <summary>
    /// 仍存活的登记玩家都完成当前任务时取消最左边的指令意图并进入下一个任务。不改任何玩家的进度；
    /// 进入下一个任务时进度清空，所以同一个任务不会被重复推进。
    /// </summary>
    private async Task TryCompleteCurrentDirective(CardType[] sequence)
    {
        IReadOnlyList<ulong> requiredPlayers = ParseLivingRequiredPlayerNetIds();
        if (requiredPlayers.Count == 0
            || requiredPlayers.Any(netId =>
                GetDirectiveProgress(netId) < sequence.Length))
        {
            return;
        }

        bool canceled = await CancelLeftmostDirectiveIntent();
        int taskCount = GetDirectiveTaskCount();
        if (!canceled || CurrentDirectiveTaskIndex + 1 >= taskCount)
        {
            CurrentDirectiveCompleted = true;
            return;
        }

        CurrentDirectiveTaskIndex++;
        CurrentDirectiveCompleted = false;
        DirectiveProgressByPlayerNetId = string.Empty;
    }

    internal RnfmabjDirectiveSnapshot GetDirectiveSnapshot(
        ulong? requestedPlayerNetId)
    {
        if (!TryGetCurrentDirectiveSequence(out CardType[] sequence))
        {
            return RnfmabjDirectiveSnapshot.Hidden;
        }

        IReadOnlyList<ulong> requiredPlayers = ParseLivingRequiredPlayerNetIds();
        ulong? localPlayerNetId = requestedPlayerNetId;
        if (!localPlayerNetId.HasValue && requiredPlayers.Count > 0)
        {
            localPlayerNetId = requiredPlayers[0];
        }

        int progress = localPlayerNetId.HasValue
            ? GetDirectiveProgress(localPlayerNetId.Value)
            : 0;
        int completedPlayers = requiredPlayers.Count(netId =>
            GetDirectiveProgress(netId) >= sequence.Length);
        int taskCount = GetDirectiveTaskCount();
        string fingerprint = string.Join(
            '|',
            DirectivePlanSerial.ToString(CultureInfo.InvariantCulture),
            CurrentDirectiveTaskIndex.ToString(CultureInfo.InvariantCulture),
            CurrentDirectiveCompleted ? "1" : "0",
            DirectiveProgressByPlayerNetId,
            localPlayerNetId?.ToString(CultureInfo.InvariantCulture) ?? "none",
            // 要求人数随队友死亡、复活变化，而计划与进度可能都没变；不计入指纹时指令卡不会重画。
            string.Join(',', requiredPlayers));
        return new RnfmabjDirectiveSnapshot(
            IsVisible: true,
            Sequence: sequence,
            Progress: Math.Clamp(progress, 0, sequence.Length),
            TaskNumber: CurrentDirectiveTaskIndex + 1,
            TaskCount: taskCount,
            CompletedPlayers: completedPlayers,
            RequiredPlayers: requiredPlayers.Count,
            LocalPlayerCompleted: progress >= sequence.Length,
            CurrentTaskCompleted: CurrentDirectiveCompleted,
            Fingerprint: fingerprint);
    }

    /// <summary>
    /// 本体计划换了一份（<see cref="RnfmabjMonsterBase.PlanSerial"/> 变了）或存档里的指令状态不自洽时，按当前计划重建任务。
    /// 读档回到同一回合时计划序号不变，不会重掷随机数。
    /// </summary>
    private void EnsureDirectiveTasksForPlan(int round, Rng rng)
    {
        if (DirectivePlanSerial == PlanSerial && HasUsableDirectiveState())
        {
            return;
        }

        int taskCount = Enumerable.Range(0, StoredIntentSlots)
            .Count(slot => RnfmabjDirectiveTracker.IsCancelableDirectiveMove(GetPlannedMove(slot)));
        DirectivePlanSerial = PlanSerial;
        CurrentDirectiveTaskIndex = 0;
        CurrentDirectiveCompleted = false;
        DirectiveProgressByPlayerNetId = string.Empty;
        DirectiveRequiredPlayerNetIds = Creature.CombatState == null
            ? string.Empty
            : RnfmabjDirectiveTracker.FormatRequiredPlayerNetIds(
                Creature.CombatState.Players.Select(static player => player.NetId));

        if (taskCount <= 0)
        {
            DirectiveSequenceLength = 0;
            DirectiveSequenceCodes = [];
            return;
        }

        DirectiveSequenceLength = RnfmabjDirectiveTracker.ChecksPerTask;
        DirectiveSequenceCodes = RnfmabjDirectiveTracker.RollSequenceCodes(
            round,
            taskCount,
            rng);
    }

    private bool HasUsableDirectiveState() =>
        RnfmabjDirectiveTracker.HasUsableState(
            DirectiveSequenceLength,
            DirectiveSequenceCodes,
            CurrentDirectiveTaskIndex);

    private bool TryGetCurrentDirectiveSequence(out CardType[] sequence) =>
        RnfmabjDirectiveTracker.TryGetSequence(
            DirectiveSequenceLength,
            DirectiveSequenceCodes,
            CurrentDirectiveTaskIndex,
            out sequence);

    private int GetDirectiveTaskCount() =>
        RnfmabjDirectiveTracker.GetTaskCount(
            DirectiveSequenceLength,
            DirectiveSequenceCodes);

    private async Task<bool> CancelLeftmostDirectiveIntent()
    {
        int slot = Enumerable.Range(0, StoredIntentSlots)
            .FirstOrDefault(
                slot => RnfmabjDirectiveTracker.IsCancelableDirectiveMove(GetPlannedMove(slot)),
                -1);
        if (slot < 0)
        {
            return false;
        }

        int[] targets = Enumerable.Range(0, StoredIntentSlots)
            .Select(GetPlannedTarget)
            .ToArray();
        RemovePlannedMoveAt(slot);
        for (int targetSlot = slot; targetSlot < StoredIntentSlots - 1; targetSlot++)
        {
            SetPlannedTarget(targetSlot, targets[targetSlot + 1]);
        }
        SetPlannedTarget(StoredIntentSlots - 1, -1);
        await RefreshPlanDisplay();
        return true;
    }

    /// <summary>
    /// 计划生成时登记的玩家里当前仍存活的那些。登记名单写在计划生成时，包含当时已倒下的玩家，
    /// 也不随回合中的死亡更新；死亡的玩家无法再出牌，算进名单指令就永远完成不了。
    /// 存活状态是同步的战斗状态，两端在同一张牌的结算里得到相同名单；玩家被复活后重新计入。
    /// </summary>
    private IReadOnlyList<ulong> ParseLivingRequiredPlayerNetIds()
    {
        IReadOnlyList<ulong> registered = RnfmabjDirectiveTracker.ParseRequiredPlayerNetIds(
            DirectiveRequiredPlayerNetIds);
        if (PlacedCombatState() is not { } combatState)
        {
            return registered;
        }

        return registered
            .Where(netId => combatState.Players.Any(player =>
                player.NetId == netId && player.Creature.IsAlive))
            .ToArray();
    }

    /// <summary>
    /// 指令快照也会在怪物还没放进战斗时读取（读回存档状态后、生成生物之前），这时原版的 <c>Creature</c> 访问器会抛异常；
    /// 没有生物就按“不在战斗中”处理，名单不过滤。
    /// </summary>
    private CombatStateLike? PlacedCombatState()
    {
        try
        {
            return Creature.CombatState;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private int GetDirectiveProgress(ulong playerNetId)
    {
        Dictionary<ulong, int> progress = RnfmabjDirectiveTracker.ParseProgress(
            DirectiveProgressByPlayerNetId,
            DirectiveSequenceLength);
        return progress.TryGetValue(playerNetId, out int value)
            ? Math.Clamp(value, 0, Math.Max(0, DirectiveSequenceLength))
            : 0;
    }

    private void SetDirectiveProgress(ulong playerNetId, int value)
    {
        Dictionary<ulong, int> progress = RnfmabjDirectiveTracker.ParseProgress(
            DirectiveProgressByPlayerNetId,
            DirectiveSequenceLength);
        value = Math.Clamp(value, 0, Math.Max(0, DirectiveSequenceLength));
        if (value == 0)
        {
            progress.Remove(playerNetId);
        }
        else
        {
            progress[playerNetId] = value;
        }

        DirectiveProgressByPlayerNetId =
            RnfmabjDirectiveTracker.FormatProgress(progress);
    }
}

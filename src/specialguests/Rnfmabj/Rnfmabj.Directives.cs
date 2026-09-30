using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.specialguests.Rnfmabj;

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

        IReadOnlyList<ulong> requiredPlayers = ParseRequiredPlayerNetIds();
        ulong playerNetId = cardPlay.Player.NetId;
        if (!requiredPlayers.Contains(playerNetId))
        {
            return;
        }

        int progress = GetDirectiveProgress(playerNetId);
        if (progress >= sequence.Length)
        {
            // A player who has completed the current directive stays locked
            // while waiting for every other player.
            return;
        }

        CardType playedType = cardPlay.Card.Type;
        progress = playedType == sequence[progress]
            ? progress + 1
            : 0;
        SetDirectiveProgress(playerNetId, progress);
        if (progress < sequence.Length
            || requiredPlayers.Count == 0
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

        IReadOnlyList<ulong> requiredPlayers = ParseRequiredPlayerNetIds();
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
            localPlayerNetId?.ToString(CultureInfo.InvariantCulture) ?? "none");
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

    private IReadOnlyList<ulong> ParseRequiredPlayerNetIds() =>
        RnfmabjDirectiveTracker.ParseRequiredPlayerNetIds(
            DirectiveRequiredPlayerNetIds);

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

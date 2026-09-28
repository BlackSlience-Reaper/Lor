using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.patches;

internal static class DodgeDiceCombatFeedback
{
    private const string BlockHitSfx = "event:/sfx/block_hit";

    private static int _pendingBlockHitSfxSkips;

    private static readonly Dictionary<Creature, int> PendingBlockSparkSkips = new();

    private static readonly Dictionary<Creature, int> PendingBlockedTextSkips = new();

    private static readonly Dictionary<Creature, int> PendingDodgeBlockGainSkips = new();

    [ThreadStatic]
    private static Stack<CardPreviewMode>? _damagePreviewModes;

    internal static bool IsResolvingActualDamage =>
        _damagePreviewModes is { Count: > 0 } && _damagePreviewModes.Peek() == CardPreviewMode.None;

    internal static void PushDamagePreviewMode(CardPreviewMode previewMode)
    {
        _damagePreviewModes ??= new Stack<CardPreviewMode>();
        _damagePreviewModes.Push(previewMode);
    }

    internal static void PopDamagePreviewMode()
    {
        if (_damagePreviewModes is { Count: > 0 })
        {
            _damagePreviewModes.Pop();
        }
    }

    internal static void QueueDodgeAvoidedHit(Creature target)
    {
        _pendingBlockHitSfxSkips++;
        Increment(PendingBlockSparkSkips, target);
        Increment(PendingBlockedTextSkips, target);
    }

    internal static void QueueDodgeBlockGain(Creature target)
    {
        Increment(PendingDodgeBlockGainSkips, target);
    }

    internal static bool TryConsumeBlockHitSfx(string sfx)
    {
        if (!string.Equals(sfx, BlockHitSfx, StringComparison.Ordinal) || _pendingBlockHitSfxSkips <= 0)
        {
            return false;
        }

        _pendingBlockHitSfxSkips--;
        return true;
    }

    internal static bool TryConsumeBlockSpark(Creature target) =>
        TryConsume(PendingBlockSparkSkips, target);

    internal static bool TryConsumeBlockedText(Creature target) =>
        TryConsume(PendingBlockedTextSkips, target);

    internal static bool TryConsumeDodgeBlockGain(Creature target) =>
        TryConsume(PendingDodgeBlockGainSkips, target);

    internal static void Reset()
    {
        _pendingBlockHitSfxSkips = 0;
        PendingBlockSparkSkips.Clear();
        PendingBlockedTextSkips.Clear();
        PendingDodgeBlockGainSkips.Clear();
        _damagePreviewModes?.Clear();
    }

    private static void Increment(Dictionary<Creature, int> counts, Creature target)
    {
        counts[target] = counts.TryGetValue(target, out int count) ? count + 1 : 1;
    }

    private static bool TryConsume(Dictionary<Creature, int> counts, Creature target)
    {
        if (!counts.TryGetValue(target, out int count) || count <= 0)
        {
            return false;
        }

        if (count == 1)
        {
            counts.Remove(target);
        }
        else
        {
            counts[target] = count - 1;
        }

        return true;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
internal static class DodgeDiceFeedbackCombatSetupPatch
{
    [HarmonyPrefix]
    private static void Prefix() => DodgeDiceCombatFeedback.Reset();
}

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.Reset), typeof(bool))]
internal static class DodgeDiceFeedbackCombatResetPatch
{
    [HarmonyPrefix]
    private static void Prefix() => DodgeDiceCombatFeedback.Reset();
}

// 前缀没执行（排在前面的前缀抛异常）时 Finalizer 不能弹栈，否则会弹掉外层嵌套调用压的那一帧。
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
[LibraryPatch(Reason = "闪避骰能力的 ModifyDamageAdditive 拿不到 previewMode，只能由钩子入口传下去，用来区分实战与预览。现在没有内容施加闪避骰；改为能力在 BeforeDamageReceived 提交状态的方案见重构指导附录 B。")]
internal static class DodgeDiceDamagePreviewModePatch
{
    private static void Prefix(CardPreviewMode previewMode, out bool __state)
    {
        DodgeDiceCombatFeedback.PushDamagePreviewMode(previewMode);
        __state = true;
    }

    private static void Finalizer(bool __state)
    {
        if (__state)
        {
            DodgeDiceCombatFeedback.PopDamagePreviewMode();
        }
    }
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), typeof(string), typeof(float))]
[LibraryPatch(Reason = "原版在 CreatureCmd.Damage 完全格挡后硬编码播放 block_hit，没有 Hook；只消耗本模组闪避骰在同一次命中里入队的计数，其余调用原样放行。")]
internal static class DodgeDiceBlockHitSfxPatch
{
    private static bool Prefix(string sfx) =>
        !DodgeDiceCombatFeedback.TryConsumeBlockHitSfx(sfx);
}

[HarmonyPatch(typeof(NBlockSparkVfx), nameof(NBlockSparkVfx.Create))]
[LibraryPatch(Reason = "原版完全格挡火花在 CreatureCmd.Damage 内直接创建，没有 Hook；只对本模组闪避骰在同一次命中里入队的受击者跳过一次创建。")]
internal static class DodgeDiceBlockSparkVfxPatch
{
    private static bool Prefix(Creature target, ref NBlockSparkVfx? __result)
    {
        if (!DodgeDiceCombatFeedback.TryConsumeBlockSpark(target))
        {
            return true;
        }

        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(NDamageBlockedVfx), nameof(NDamageBlockedVfx.Create))]
[LibraryPatch(Reason = "原版“已格挡”飘字在 CreatureCmd.Damage 内直接创建，没有 Hook；只对本模组闪避骰入队的受击者跳过一次。")]
internal static class DodgeDiceBlockedTextVfxPatch
{
    private static bool Prefix(Creature target, ref NDamageBlockedVfx? __result)
    {
        if (!DodgeDiceCombatFeedback.TryConsumeBlockedText(target))
        {
            return true;
        }

        __result = null;
        return false;
    }
}

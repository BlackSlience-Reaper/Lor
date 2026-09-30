using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.addons.mega_text;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

// 仆从之死路径先照常生成、填充奖励再清空，会推进药水掉率和奖励 RNG；改为空界面补丁直接用 RewardsSet.EmptyForRoom
// 可以避免，见重构指导附录 B。
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyRewards))]
[LibraryPatch(Reason = "ModifyRewards 只问局级监听者，怪物、遭遇、能力都不在，没有已有模型能覆写；仆从之死要以 Last 清空（含其他模组追加的奖励），胜利时按最终列表去重补发书页。只作用于本模组 WrathServantStrong 的房间结算奖励。")]
internal static class WrathServantRewardPatch
{
    private const string FailedRewardHeaderLocKey = "WRATH_SERVANT_FAILED_REWARD_HEADER";

    private static readonly string WrathServantPageRelicTitleLocKey =
        $"{ModelDb.GetId<WrathServantPageRelic>().Entry}.title";

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (room is not CombatRoom { Encounter: WrathServantStrong encounter } combatRoom)
        {
            return;
        }

        if (encounter.EndedByServantDeath)
        {
            rewards.Clear();
            return;
        }

        if (!AbnormalityPageRewardHelper.ShouldAddPageReward<WrathServantPageRelic>(
            combatRoom,
            player,
            rewards,
            WrathServantPageRelicTitleLocKey))
        {
            return;
        }

        rewards.Add(new RelicReward(ModelDb.Relic<WrathServantPageRelic>().ToMutable(), player));
    }

    public static bool IsServantDeathRewardScreen(AbstractRoom? room)
    {
        return room is CombatRoom { Encounter: WrathServantStrong { EndedByServantDeath: true } };
    }

    public static string FailedRewardHeaderText =>
        new LocString("gameplay_ui", FailedRewardHeaderLocKey).GetFormattedText();
}

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen._Ready))]
internal static class WrathServantRewardHeaderPatch
{
    private static void Postfix(NRewardsScreen __instance)
    {
        if (!WrathServantRewardPatch.IsServantDeathRewardScreen(
                RunManager.Instance.DebugOnlyGetState()?.CurrentRoom))
        {
            return;
        }

        MegaLabel? headerLabel = __instance.GetNodeOrNull<MegaLabel>("%HeaderLabel");
        headerLabel?.SetTextAutoSize(WrathServantRewardPatch.FailedRewardHeaderText);
    }
}

[HarmonyPatch(typeof(NCombatUi), "OnCombatWon")]
[LibraryPatch(Reason = "愤怒仆从之死结局要显示空的终局奖励界面，NCombatUi.OnCombatWon 私有无 Hook；只作用于本模组愤怒仆从遭遇。改为不覆写 ShouldGiveRewards 会改变读档表现，暂缓。")]
internal static class WrathServantEmptyRewardScreenPatch
{

    private static bool Prefix(NCombatUi __instance, CombatRoom room)
    {
        if (!WrathServantRewardPatch.IsServantDeathRewardScreen(room))
        {
            return true;
        }

        // This encounter intentionally shows an empty terminal rewards screen on servant death.
        if (VanillaPrivate.CombatUiShowRewards.Invoke(__instance, [room]) is Task task)
        {
            TaskHelper.RunSafely(task);
            return false;
        }

        return true;
    }
}

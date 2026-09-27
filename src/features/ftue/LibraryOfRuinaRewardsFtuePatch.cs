using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;

namespace LibraryOfRuina.features.ftue;

/// <summary>
/// After the vanilla rewards screen finishes its SetRewards,
/// show the LoR rewards intro FTUE if this combat was against an LoR encounter.
///
/// Uses manual patching via TargetMethod to handle Public/Beta API drift
/// where the method name may differ between versions.
/// </summary>
[HarmonyPatch]
public static class LibraryOfRuinaRewardsFtuePatch
{
    private static MethodBase? _targetMethod;

    [HarmonyPrepare]
    public static bool Prepare()
    {
        _targetMethod = ResolveTargetMethod();
        if (_targetMethod != null)
        {
            return true;
        }

        Log.Warn(
            "LibraryOfRuina FTUE: Could not find "
            + "NRewardsScreen.SetRewards or _Ready; rewards FTUE disabled.");
        return false;
    }

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return _targetMethod
            ?? throw new MissingMethodException(
                nameof(NRewardsScreen),
                "SetRewards/_Ready");
    }

    private static MethodBase? ResolveTargetMethod()
    {
        var method = AccessTools.Method(typeof(NRewardsScreen), "SetRewards",
            [typeof(IEnumerable<Reward>)]);

        method ??= AccessTools.Method(typeof(NRewardsScreen), "SetRewards");

        method ??= typeof(NRewardsScreen)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "SetRewards");

        // v0.106.1+: SetRewards was inlined into _Ready, patch _Ready instead
        method ??= AccessTools.Method(typeof(NRewardsScreen), "_Ready");
        return method;
    }

    [HarmonyPostfix]
    public static void Postfix(object __instance)
    {
        if (__instance is not NRewardsScreen rewardsScreen)
            return;

        TaskHelper.RunSafely(ShowAbnormalityPageRewardFtue(rewardsScreen));
    }

    private static async Task ShowAbnormalityPageRewardFtue(NRewardsScreen rewardsScreen)
    {
        if (!FtueGuard.ShouldShow(LibraryOfRuinaFtueIds.FirstAbnormalityPageReward)
            || !GodotObject.IsInstanceValid(rewardsScreen)
            || !rewardsScreen.IsInsideTree())
        {
            return;
        }

        var tree = rewardsScreen.GetTree();
        if (tree == null)
            return;

        for (int i = 0; i < 4; i++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(rewardsScreen) || !rewardsScreen.IsInsideTree())
            {
                return;
            }
        }

        for (int i = 0; NModalContainer.Instance?.OpenModal != null && i < 600; i++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (!GodotObject.IsInstanceValid(rewardsScreen) || !rewardsScreen.IsInsideTree())
            {
                return;
            }
        }

        NModalContainer? modalContainer = NModalContainer.Instance;
        if (!GodotObject.IsInstanceValid(modalContainer)
            || !modalContainer!.IsInsideTree()
            || modalContainer.OpenModal != null
            || !rewardsScreen.IsVisibleInTree())
        {
            return;
        }

        Control? rewardButton = FindFirstAbnormalityPageRewardButton(rewardsScreen);
        if (rewardButton == null)
            return;

        if (!FtueGuard.TryConsumeShowRequest(LibraryOfRuinaFtueIds.FirstAbnormalityPageReward))
            return;

        var popup = NLibraryOfRuinaFtuePopup.CreateAnchored(
            LibraryOfRuinaFtueIds.FirstAbnormalityPageReward,
            "LOR_ABNORMALITY_PAGE_REWARD_FTUE_TITLE",
            "LOR_ABNORMALITY_PAGE_REWARD_FTUE_BODY",
            () => rewardButton,
            new Vector2(680f, -20f));

        modalContainer.Add(popup);
        Log.Info($"LibraryOfRuina: Showing FTUE {LibraryOfRuinaFtueIds.FirstAbnormalityPageReward}");
    }

    private static Control? FindFirstAbnormalityPageRewardButton(NRewardsScreen rewardsScreen)
    {
        foreach (NRewardButton rewardButton in rewardsScreen.GetChildren().OfType<NRewardButton>())
        {
            if (IsAbnormalityPageReward(rewardButton.Reward))
                return rewardButton;
        }

        return FindFirstAbnormalityPageRewardButtonRecursive(rewardsScreen);
    }

    private static Control? FindFirstAbnormalityPageRewardButtonRecursive(Node node)
    {
        if (node is NRewardButton rewardButton && IsAbnormalityPageReward(rewardButton.Reward))
            return rewardButton;

        foreach (Node child in node.GetChildren())
        {
            Control? found = FindFirstAbnormalityPageRewardButtonRecursive(child);
            if (found != null)
                return found;
        }

        return null;
    }

    private static bool IsAbnormalityPageReward(Reward? reward)
    {
        if (reward is not RelicReward relicReward || !relicReward.IsPopulated)
            return false;

        string locKey = relicReward.Description.LocEntryKey;
        if (string.IsNullOrEmpty(locKey))
            return false;

        return locKey.StartsWith("MATCH_MARK_RELIC.", StringComparison.Ordinal)
            || locKey.EndsWith("_PAGE_RELIC.title", StringComparison.Ordinal);
    }
}

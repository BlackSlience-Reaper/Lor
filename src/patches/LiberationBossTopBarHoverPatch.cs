using Godot;
using HarmonyLib;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;

[HarmonyPatch(
    typeof(NTopBarBossIcon),
    "OnFocus")]
[LibraryPatch(Reason = "原版 Boss 图标悬停文本固定为通用 BOSS 提示且方法受保护，没有扩展点；只在可见 Boss 为本模组解放战时替换提示。")]
internal static class LiberationBossTopBarHoverPatch
{
    private const string DoubleHoverLocalizationPrefix =
        "LIBERATION_DOUBLE_BOSS.topBarHover";

    private static bool Prefix(
        NTopBarBossIcon __instance)
    {
        // 悬停是本地 UI 事件，只在悬停的一端执行，这里不能改写本局的房间序列。内容关闭时，普通地图进房
        // （含 Boss 房）经过 PullNextEncounter，由 MonsterExtensionPullNextEncounterGatePatch 对称地换回原版遭遇；
        // 显式指定遭遇的进房路径与顶栏显示不经过它，关闭内容后继续旧局的一致性另行处理（重构指导阶段 3a）。
        if (!LibraryRunSettings.MonsterExtensionEnabled)
        {
            return true;
        }

        IRunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null)
        {
            return true;
        }

        EncounterModel firstBoss = runState.Act.BossEncounter;
        EncounterModel? secondBoss = runState.Act.SecondBossEncounter;
        bool onlyShowSecondBoss = IsShowingOnlySecondBoss(runState);

        if (secondBoss != null && !onlyShowSecondBoss)
        {
            if (!LiberationBossRegistry.IsLiberationEncounter(firstBoss)
                || !LiberationBossRegistry.IsLiberationEncounter(secondBoss))
            {
                return true;
            }

            return ShowDoubleLiberationHover(
                __instance,
                firstBoss,
                secondBoss);
        }

        EncounterModel? visibleBoss = onlyShowSecondBoss
            ? secondBoss
            : firstBoss;
        return visibleBoss != null
            && LiberationBossRegistry.IsLiberationEncounter(visibleBoss)
                ? ShowSingleLiberationHover(__instance, visibleBoss)
                : true;
    }

    private static bool ShowDoubleLiberationHover(
        NTopBarBossIcon instance,
        EncounterModel firstBoss,
        EncounterModel secondBoss)
    {
        var title = new LocString(
            "encounters",
            DoubleHoverLocalizationPrefix + ".title");
        title.Add("BossName1", firstBoss.Title);
        title.Add("BossName2", secondBoss.Title);

        var description = new LocString(
            "encounters",
            DoubleHoverLocalizationPrefix + ".description");
        description.Add("FloorName1", FloorName(firstBoss));
        description.Add("FloorName2", FloorName(secondBoss));

        return ShowHover(instance, new HoverTip(title, description));
    }

    private static bool ShowSingleLiberationHover(
        NTopBarBossIcon instance,
        EncounterModel encounter)
    {
        string prefix = encounter.Id.Entry + ".topBarHover";
        return ShowHover(
            instance,
            new HoverTip(
                new LocString("encounters", prefix + ".title"),
                new LocString("encounters", prefix + ".description")));
    }

    private static bool ShowHover(
        NTopBarBossIcon instance,
        HoverTip hoverTip)
    {
        if (NHoverTipSet.CreateAndShow(instance, hoverTip)
            is not { } hoverTipSet)
        {
            return true;
        }

        hoverTipSet.GlobalPosition = instance.GlobalPosition
            + new Vector2(0f, instance.Size.Y + 20f);
        return false;
    }

    private static bool IsShowingOnlySecondBoss(IRunState runState) =>
        runState.Map.SecondBossMapPoint != null
        && runState.CurrentMapPoint == runState.Map.BossMapPoint;

    private static LocString FloorName(EncounterModel encounter) =>
        new("encounters", encounter.Id.Entry + ".floorName");
}

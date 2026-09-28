using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.settings;
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
internal static class LiberationBossTopBarHoverPatch
{
    private const string DoubleHoverLocalizationPrefix =
        "LIBERATION_DOUBLE_BOSS.topBarHover";

    private static bool Prefix(
        NTopBarBossIcon __instance)
    {
        if (!LibraryOfRuinaSettings.MonsterExtensionActive)
        {
            LibraryEncounterWeighting.RestoreVanillaEncounters(
                RunManager.Instance.DebugOnlyGetState());
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

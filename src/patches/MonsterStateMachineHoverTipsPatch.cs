using System;
using HarmonyLib;
using LibraryOfRuina.features.intentgraph;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.ShowHoverTips))]
public static class MonsterStateMachineHoverTipsPatch
{
    [HarmonyPostfix]
    public static void ShowHoverTipsPostfix(NCreature __instance)
    {
        try
        {
            MonsterIntentGraphOverlayController.ShowFor(__instance);
        }
        catch (Exception e)
        {
            Log.Warn("[LibraryOfRuina.IntentGraph] ShowHoverTips patch failed: " + e);
        }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.HideHoverTips))]
public static class MonsterStateMachineHideHoverTipsPatch
{
    [HarmonyPostfix]
    public static void HideHoverTipsPostfix(NCreature __instance)
    {
        MonsterIntentGraphOverlayController.HideFor(__instance);
    }
}

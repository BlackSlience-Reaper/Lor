using HarmonyLib;
using LibraryOfRuina.features;
using MegaCrit.Sts2.Core.Localization;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.SetLanguage))]
public static class MonsterStateMachineHoverTipLayoutPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        MonsterStateMachineIntentGraphFeature.ReloadLocalization();
    }
}

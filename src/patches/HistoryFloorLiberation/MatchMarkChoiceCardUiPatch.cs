using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.addons.mega_text;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace LibraryOfRuina.patches.HistoryFloorLiberation;

[HarmonyPatch(typeof(NCard), "UpdateTypePlaque")]
public static class PageRelicChoiceCardUiPatch
{

    [HarmonyPostfix]
    public static void Postfix(NCard __instance)
    {
        Control? typePlaque = VanillaPrivate.CardTypePlaque.Get(__instance) as Control;
        MegaLabel? typeLabel = VanillaPrivate.CardTypeLabel.Get(__instance) as MegaLabel;
        if (typePlaque == null || typeLabel == null)
        {
            return;
        }

        bool shouldHide = AbnormalityPageRewardPreselection
            .IsPageRelicChoiceCard(__instance.Model);
        typePlaque.Visible = !shouldHide;
        typeLabel.Visible = !shouldHide;
    }
}

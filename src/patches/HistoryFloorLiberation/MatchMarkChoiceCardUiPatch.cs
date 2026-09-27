using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.addons.mega_text;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace LibraryOfRuina.patches.HistoryFloorLiberation;

[HarmonyPatch(typeof(NCard), "UpdateTypePlaque")]
public static class PageRelicChoiceCardUiPatch
{
    private static readonly FieldInfo? TypePlaqueField = AccessTools.Field(typeof(NCard), "_typePlaque");
    private static readonly FieldInfo? TypeLabelField = AccessTools.Field(typeof(NCard), "_typeLabel");

    [HarmonyPostfix]
    public static void Postfix(NCard __instance)
    {
        Control? typePlaque = TypePlaqueField?.GetValue(__instance) as Control;
        MegaLabel? typeLabel = TypeLabelField?.GetValue(__instance) as MegaLabel;
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

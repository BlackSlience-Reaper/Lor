using System.Linq;
using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.cards.Xiao;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NChooseACardSelectionScreen), nameof(NChooseACardSelectionScreen._Ready))]
public static class ChooseACardScreenBannerPatch
{
    private static readonly FieldInfo? CardsField =
        AccessTools.Field(typeof(NChooseACardSelectionScreen), "_cards");

    [HarmonyPostfix]
    public static void Postfix(NChooseACardSelectionScreen __instance)
    {
        if (CardsField?.GetValue(__instance) is not IReadOnlyList<CardModel> cards || cards.Count == 0)
        {
            return;
        }

        if (IsXiaoEgoChoice(cards))
        {
            NCommonBanner xiaoBanner = __instance.GetNode<NCommonBanner>("Banner");
            xiaoBanner.label.SetTextAutoSize(
                new LocString(
                    "events",
                    "XIAO_SPECIAL_GUEST_EVENT.selectionScreenPrompt")
                .GetFormattedText());
            return;
        }

        if (!AbnormalityPageRewardPreselection.IsPageRelicChoiceCard(cards[0]))
        {
            return;
        }

        NCommonBanner banner = __instance.GetNode<NCommonBanner>("Banner");
        banner.label.Text = "选择一个效果";
    }

    private static bool IsXiaoEgoChoice(IReadOnlyList<CardModel> cards) =>
        cards.Count == 3
        && cards.All(static card => card is
            XiaoPulaoBellEgoCard
            or XiaoYaziVengeanceEgoCard
            or XiaoTaotieFeastEgoCard)
        && cards.Any(static card => card is XiaoPulaoBellEgoCard)
        && cards.Any(static card => card is XiaoYaziVengeanceEgoCard)
        && cards.Any(static card => card is XiaoTaotieFeastEgoCard);

}

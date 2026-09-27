using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.ShowHoverTips))]
public static class CounterIntentHoverTipPatch
{
    private const string CounterKeywordTitleId = "Title=intents.COUNTER_KEYWORD.title";
    private const string CounterIntentTitlePrefix = "Title=intents.COUNTER_";

    [HarmonyPrefix]
    public static void Prefix(ref IEnumerable<IHoverTip> hoverTips)
    {
        List<IHoverTip> tips = hoverTips.ToList();
        if (tips.Any(IsCounterKeywordTip) || !tips.Any(IsCounterIntentTip))
        {
            hoverTips = tips;
            return;
        }

        tips.Add(CreateCounterKeywordTip());
        hoverTips = tips;
    }

    private static bool IsCounterIntentTip(IHoverTip tip) =>
        tip.Id.Contains(CounterIntentTitlePrefix) && !IsCounterKeywordTip(tip);

    private static bool IsCounterKeywordTip(IHoverTip tip) =>
        tip.Id.Contains(CounterKeywordTitleId);

    private static IHoverTip CreateCounterKeywordTip() =>
        new HoverTip(
            new LocString("intents", "COUNTER_KEYWORD.title"),
            new LocString("intents", "COUNTER_KEYWORD.description"));
}

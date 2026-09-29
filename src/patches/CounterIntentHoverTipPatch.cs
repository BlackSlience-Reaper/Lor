using System.Linq;
using LibraryOfRuina.intents.rendering;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace LibraryOfRuina.patches;

/// <summary><c>NCreature.ShowHoverTips</c> 前缀的处理函数，入口在 IntentVisualDispatch：有反击意图提示时补一条“反击”关键词。</summary>
public static class CounterIntentHoverTipPatch
{
    private const string CounterKeywordTitleId = "Title=intents.COUNTER_KEYWORD.title";
    private const string CounterIntentTitlePrefix = "Title=intents.COUNTER_";

    // 不论是否补充，都把参数换成物化后的列表，原版随后再枚举一次。
    internal static IntentDecoratorOutcome AppendCounterKeyword(ref IEnumerable<IHoverTip> hoverTips)
    {
        List<IHoverTip> tips = hoverTips.ToList();
        if (tips.Any(IsCounterKeywordTip) || !tips.Any(IsCounterIntentTip))
        {
            hoverTips = tips;
            return IntentDecoratorOutcome.Unchanged;
        }

        tips.Add(CreateCounterKeywordTip());
        hoverTips = tips;
        return IntentDecoratorOutcome.Applied;
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

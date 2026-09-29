using System.Linq;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.Helpers;

namespace LibraryOfRuina.framework.intents;

internal static class CounterIntentAnimData
{
    public const string Dodge = "dodge";
    public const string CounterDodge = "counter_dodge";

    private readonly record struct InternalData(string FrameRoot, string Prefix, int FrameCount);

    private static readonly Dictionary<string, InternalData> Data = new()
    {
        [IntentAnimData.attack1] = new("intents/counter/frames/attack", "counter_attack_1", 1),
        [IntentAnimData.attack2] = new("intents/counter/frames/attack", "counter_attack_2", 1),
        [IntentAnimData.attack3] = new("intents/counter/frames/attack", "counter_attack_3", 1),
        [IntentAnimData.attack4] = new("intents/counter/frames/attack", "counter_attack_4", 1),
        [IntentAnimData.attack5] = new("intents/counter/frames/attack", "counter_attack_5", 1),
        [IntentAnimData.buff] = new("intents/counter/frames/buff", "counter_buff", 30),
        [IntentAnimData.cardDebuff] = new("intents/counter/frames/card_debuff", "counter_card_debuff", 15),
        [IntentAnimData.debuff] = new("intents/counter/frames/debuff", "counter_debuff", 11),
        [IntentAnimData.defend] = new("intents/counter/frames/defend", "counter_defend", 45),
        [Dodge] = new("intents/dodge/frames", "dodge", 30),
        [CounterDodge] = new("intents/counter/frames/dodge", "counter_dodge", 30),
        [IntentAnimData.status] = new("intents/counter/frames/status", "counter_status_card", 19),
        [IntentAnimData.summon] = new("intents/counter/frames/summon", "counter_summon", 25)
    };

    public static IEnumerable<string> AssetPaths =>
        Data.Values.SelectMany(static data =>
            Enumerable.Range(0, data.FrameCount).Select(index => GetFramePath(data, index)));

    public static IEnumerable<string> GetAssetPaths(string animation)
    {
        if (!Data.TryGetValue(animation, out InternalData data))
        {
            return [];
        }

        return Enumerable.Range(0, data.FrameCount).Select(index => GetFramePath(data, index));
    }

    public static string GetAnimationFrame(string animation, int frame)
    {
        InternalData data = Data[animation];
        return GetFramePath(data, frame % data.FrameCount);
    }

    public static bool TryGetAnimationFrame(string animation, int frame, out string path)
    {
        if (!Data.TryGetValue(animation, out InternalData data))
        {
            path = string.Empty;
            return false;
        }

        path = GetFramePath(data, frame % data.FrameCount);
        return true;
    }

    private static string GetFramePath(InternalData data, int frame) =>
        ImageHelper.GetImagePath($"{data.FrameRoot}/{data.Prefix}_{frame:00}.png");
}

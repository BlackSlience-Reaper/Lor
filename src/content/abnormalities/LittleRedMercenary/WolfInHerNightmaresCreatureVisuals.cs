using System;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

[MonsterVisual(typeof(WolfInHerNightmares), ScenePath = WolfInHerNightmaresCreatureVisuals.ScenePath)]
internal sealed partial class WolfInHerNightmaresCreatureVisuals
    : SceneAnimatedCreatureVisuals
{
    // 整块的 Spine 身体（按标注点对齐；斩击、突刺图照场景按 0.48 倍、其余 0.6 倍），加载失败时退回场景动画
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "wolf_in_her_nightmares",
        "wolf_nightmare",
        "slash",
        new Dictionary<string, string>
        {
            ["Slash"] = "slash",
            ["Thrust"] = "thrust",
            ["Howl"] = "howl",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal const string ScenePath =
        LittleRedMercenaryAssets.WolfInHerNightmaresScene;

    protected override string ResolveCurrentAnimationLibrary() =>
        WolfInHerNightmaresAnimationContract.Library;

    internal static string ResolveAnimationName(string triggerName) =>
        $"{WolfInHerNightmaresAnimationContract.Library}/{triggerName}";
}

internal static class WolfInHerNightmaresAnimationContract
{
    internal const string Library = "wolf";
    internal const float AttackDurationSeconds = 0.52f;
    internal const float HowlDurationSeconds = 0.45f;
    internal const float HitDurationSeconds = 0.12f;

    internal static IReadOnlyList<string> Animations { get; } =
        ["Idle", "Slash", "Thrust", "Howl", "Hit"];

    internal static float DurationForTrigger(string triggerName) =>
        triggerName switch
        {
            "Slash" or "Thrust" => AttackDurationSeconds,
            "Howl" => HowlDurationSeconds,
            "Hit" => HitDurationSeconds,
            _ => throw new ArgumentOutOfRangeException(
                nameof(triggerName),
                triggerName,
                "Only non-idle Wolf in Her Nightmares actions have a duration.")
        };
}

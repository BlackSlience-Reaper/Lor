using System;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

[MonsterVisual(typeof(WolfInHerNightmares), ScenePath = WolfInHerNightmaresCreatureVisuals.ScenePath)]
internal sealed partial class WolfInHerNightmaresCreatureVisuals
    : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/wolf_in_her_nightmares.tscn";

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

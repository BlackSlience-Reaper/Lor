using System;
using LibraryOfRuina.monsters.LiteratureFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.LiteratureFloorLiberation;

[MonsterVisual(typeof(LiteratureFloorBloodlustBoss), ScenePath = LiteratureFloorBloodlustCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorBloodlustCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/literature_floor_bloodlust_boss.tscn";

    protected override string ResolveCurrentAnimationLibrary() =>
        LiteratureFloorBloodlustAnimationContract.Library;
}

internal static class LiteratureFloorBloodlustAnimationContract
{
    internal const string Library = "bloodlust";
    internal const float PersistenceDurationSeconds = 1.80f;
    internal const float ObsessionDurationSeconds = 1.30f;
    internal const float DesireBurstDurationSeconds = 2.30f;
    internal const float UnbearableDurationSeconds = 4.10f;
    internal const float UnbearableFinisherHitTimeSeconds = 3.30f;
    internal const float CastDurationSeconds = 0.90f;
    internal const float HitDurationSeconds = 0.36f;

    internal static IReadOnlyList<float>
        PersistenceHitFrameTimesSeconds { get; } = [0.50f, 1.30f];

    internal static IReadOnlyList<float>
        ObsessionHitFrameTimesSeconds { get; } = [0.64f];

    internal static IReadOnlyList<float>
        DesireBurstHitFrameTimesSeconds { get; } =
        [0.40f, 1.10f, 1.80f];

    internal static IReadOnlyList<float>
        UnbearableHitFrameTimesSeconds { get; } =
        [0.50f, 1.10f, 1.70f, 2.30f];

    internal static IReadOnlyList<string> Animations { get; } =
    [
        "Idle",
        "Persistence",
        "Obsession",
        "DesireBurst",
        "Unbearable",
        "Cast",
        "Hit"
    ];

    internal static float DurationForTrigger(string triggerName) =>
        triggerName switch
        {
            "Persistence" => PersistenceDurationSeconds,
            "Obsession" => ObsessionDurationSeconds,
            "DesireBurst" => DesireBurstDurationSeconds,
            "Unbearable" => UnbearableDurationSeconds,
            "Cast" => CastDurationSeconds,
            "Hit" => HitDurationSeconds,
            _ => throw new ArgumentOutOfRangeException(
                nameof(triggerName),
                triggerName,
                "Only non-idle Bloodlust actions have a duration.")
        };
}

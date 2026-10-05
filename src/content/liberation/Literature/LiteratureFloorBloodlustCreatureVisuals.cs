using System;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Literature;

[MonsterVisual(typeof(LiteratureFloorBloodlustBoss), ScenePath = LiteratureFloorBloodlustCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorBloodlustCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    // Spine 身体的触发与动画契约同名；多段招式的换姿势时刻照场景动画，见 tools/spine_from_layers/build_boss_configs.py
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "literature_floor_liberation",
        "bloodlust",
        "persistence",
        new Dictionary<string, string>
        {
            ["Persistence"] = "persistence",
            ["Obsession"] = "obsession",
            ["DesireBurst"] = "desire_burst",
            ["Unbearable"] = "unbearable",
            ["Cast"] = "cast",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal const string ScenePath =
        LiteratureFloorAssets.BloodlustBossScene;

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

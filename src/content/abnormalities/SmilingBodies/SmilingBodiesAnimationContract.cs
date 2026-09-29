using System;

namespace LibraryOfRuina.content.abnormalities.SmilingBodies;

internal static class SmilingBodiesAnimationContract
{
    internal const float ActionDurationSeconds = 0.45f;
    internal const float PhaseDurationSeconds = 0.55f;
    internal const float HitDurationSeconds = 0.18f;

    private static readonly string[] Phase1Animations =
        ["Idle", "Absorb", "Hit", "Phase"];

    private static readonly string[] Phase2Animations =
        ["Idle", "Absorb", "Hit", "Phase", "Scream"];

    private static readonly string[] Phase3Animations =
        ["Idle", "Absorb", "Hit", "Phase", "Sit", "Vomit"];

    internal static IReadOnlyList<SmilingBodiesPhase> Phases { get; } =
        [
            SmilingBodiesPhase.First,
            SmilingBodiesPhase.Second,
            SmilingBodiesPhase.Third
        ];

    internal static string LibraryForPhase(SmilingBodiesPhase phase) =>
        phase switch
        {
            SmilingBodiesPhase.First => "phase_1",
            SmilingBodiesPhase.Second => "phase_2",
            SmilingBodiesPhase.Third => "phase_3",
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };

    internal static IReadOnlyList<string> AnimationsForPhase(
        SmilingBodiesPhase phase) => phase switch
        {
            SmilingBodiesPhase.First => Phase1Animations,
            SmilingBodiesPhase.Second => Phase2Animations,
            SmilingBodiesPhase.Third => Phase3Animations,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };

    internal static string QualifiedAnimationName(
        SmilingBodiesPhase phase,
        string triggerName) =>
        $"{LibraryForPhase(phase)}/{triggerName}";

    internal static float DurationForTrigger(string triggerName) =>
        triggerName switch
        {
            "Absorb" or "Scream" or "Sit" or "Vomit" =>
                ActionDurationSeconds,
            "Phase" => PhaseDurationSeconds,
            "Hit" => HitDurationSeconds,
            _ => throw new ArgumentOutOfRangeException(
                nameof(triggerName),
                triggerName,
                "Only non-idle Smiling Bodies actions have settlement durations.")
        };
}

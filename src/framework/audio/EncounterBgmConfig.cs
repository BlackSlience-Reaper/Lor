using System;

namespace LibraryOfRuina.framework.audio;

internal enum TrackProgressionMode
{
    OnMonsterDeath,
    OnRoundThreshold,
    Dynamic
}

/// <summary>
/// 一场遭遇或一个接待层的战斗 BGM：曲目、推进方式、回合阈值与音量缩放。
/// 会话只读它，不改它；曲目数组可能与别的配置共用（接待层曲目表），不能原地修改。
/// </summary>
internal sealed record EncounterBgmConfig(
    string LogTag,
    string[] TrackPaths,
    TrackProgressionMode ProgressionMode,
    int[] RoundThresholds,
    float VolumeScale = 0.85f)
{
    public Func<CombatStateLike, int>? DynamicTrackResolver { get; init; }

    public static EncounterBgmConfig DeathBased(string logTag, params string[] tracks)
    {
        return new EncounterBgmConfig(logTag, tracks, TrackProgressionMode.OnMonsterDeath, Array.Empty<int>());
    }

    public static EncounterBgmConfig RoundBased(
        string logTag,
        string[] tracks,
        float volumeScale = 0.85f,
        params int[] roundThresholds)
    {
        return new EncounterBgmConfig(
            logTag,
            tracks,
            TrackProgressionMode.OnRoundThreshold,
            roundThresholds,
            volumeScale);
    }

    public static EncounterBgmConfig PhaseBased(
        string logTag,
        string[] tracks,
        float volumeScale = 0.85f)
    {
        return new EncounterBgmConfig(
            logTag,
            tracks,
            TrackProgressionMode.Dynamic,
            Array.Empty<int>(),
            volumeScale)
        {
            DynamicTrackResolver = ResolveLiberationPhaseTrack
        };
    }

    public static EncounterBgmConfig DynamicSourceBased(
        string logTag,
        string[] tracks,
        float volumeScale = 0.85f)
    {
        return new EncounterBgmConfig(
            logTag,
            tracks,
            TrackProgressionMode.Dynamic,
            Array.Empty<int>(),
            volumeScale)
        {
            DynamicTrackResolver = ResolveDynamicSourceTrack
        };
    }

    private static int ResolveLiberationPhaseTrack(CombatStateLike combatState)
    {
        return combatState.Encounter is ILiberationPhaseBgmSource source
            ? EncounterBgmController.ResolveLiberationPhaseTrackIndex(source.CurrentPhase)
            : 0;
    }

    private static int ResolveDynamicSourceTrack(CombatStateLike combatState)
    {
        int trackIndex = 0;
        foreach (Creature enemy in combatState.Enemies)
        {
            if (enemy.Monster is IEncounterDynamicBgmTrackSource source)
            {
                trackIndex = Math.Max(trackIndex, source.CurrentEncounterBgmTrackIndex);
            }
        }

        return trackIndex;
    }
}

using System;
using LibraryOfRuina.audio;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.encounters;

/// <summary>
/// 遭遇战 BGM 的对外入口。配置查找在 <see cref="BgmRegistry"/>，会话状态机在 <see cref="BgmSession"/>，
/// 播放器与淡入淡出在 <see cref="BgmCrossfader"/>。
/// <para>
/// 残响乐团幕由 <see cref="ReverberationEnsembleBgmController"/> 按整幕接管音乐，此时这里不登记、也不报告有专属音乐；
/// 异想体精英自己的 BGM（<see cref="AbnormalityEliteBgmController"/>）优先于遭遇配置。
/// </para>
/// </summary>
internal static class EncounterBgmController
{
    public static void RegisterMonster(Creature creature)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled
            || ReverberationEnsembleBgmController.IsActScope)
        {
            return;
        }

        CombatStateLike? CombatState = creature.CombatState;
        if (CombatState?.Encounter == null)
        {
            return;
        }

        if (AbnormalityEliteBgmController.HasBgmForCombat(CombatState))
        {
            return;
        }

        Type encounterType = CombatState.Encounter.GetType();
        if (!BgmRegistry.TryGetEncounterConfig(CombatState.Encounter, out EncounterBgmConfig? defaultConfig))
        {
            return;
        }

        EncounterBgmConfig resolvedConfig = BgmRegistry.ResolveConfigForEncounter(CombatState, defaultConfig, out string? matchedLayerPath);
        string monsterId = creature.Monster?.Id.Entry ?? "UNKNOWN_MONSTER";
        if (matchedLayerPath != null)
        {
            Log.Info("[" + resolvedConfig.LogTag + "] Matched background layer binding: " + matchedLayerPath);
        }

        Log.Info("[" + resolvedConfig.LogTag + "] RegisterMonster: " + monsterId);
        BgmSession.Start(resolvedConfig, encounterType, CombatState);
        BgmSession.TrackCreature(creature);
    }

    public static void UnregisterMonster(Creature creature)
    {
        BgmSession.UntrackCreature(creature);
    }

    public static bool HasBgmForEncounter(CombatStateLike? CombatState)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled
            || ReverberationEnsembleBgmController.IsActScope)
        {
            return false;
        }

        if (CombatState?.Encounter == null)
        {
            return false;
        }

        return BgmRegistry.HasEncounterConfig(CombatState.Encounter);
    }

    internal static bool IsRunning => BgmSession.IsRunning;

    public static void ForceCurrentEncounterTrack(string trackPath, string logTag, float volumeScale = 0.85f)
    {
        BgmSession.ForceCurrentEncounterTrack(trackPath, logTag, volumeScale);
    }

    public static void RefreshCurrentEncounterTrack()
    {
        BgmSession.RefreshCurrentEncounterTrack();
    }

    public static void OnRunCleaningUp(bool graceful)
    {
        BgmSession.OnRunCleaningUp(graceful);
    }

    public static void StopRuntimeSession()
    {
        BgmSession.StopRuntimeSession();
    }

    internal static int ResolveLiberationPhaseTrackIndex(int phase) =>
        Math.Clamp((Math.Max(1, phase) - 1) / 2, 0, 2);
}

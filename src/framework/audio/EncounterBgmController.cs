using System;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.framework.audio;

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
    /// <summary>
    /// 由怪物的 <c>AfterAddedToRoom</c> 调用，处在同步的生成流程里。载入与播放曲目只影响本机，
    /// 出错时记录后返回，不能打断后面的状态写入（设计哲学 §4）。
    /// </summary>
    public static void RegisterMonster(Creature creature) =>
        PresentationGuard.Run(() => RegisterMonsterCore(creature), "EncounterBgm register");

    private static void RegisterMonsterCore(Creature creature)
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

    // 以下两个入口由卡莉 E.G.O. 与各解放层的阶段推进调用，都夹在同步状态写入之间，同样只记录本机的音频错误。
    public static void ForceCurrentEncounterTrack(string trackPath, string logTag, float volumeScale = 0.85f)
    {
        PresentationGuard.Run(
            () => BgmSession.ForceCurrentEncounterTrack(trackPath, logTag, volumeScale),
            "EncounterBgm force track");
    }

    public static void RefreshCurrentEncounterTrack()
    {
        PresentationGuard.Run(BgmSession.RefreshCurrentEncounterTrack, "EncounterBgm refresh");
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

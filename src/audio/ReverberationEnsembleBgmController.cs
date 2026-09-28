using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.audio;

internal static class ReverberationEnsembleBgmController
{
    private const string LowerTrack = "res://audio/bgm/reverberation_ensemble/lower.ogg";
    private const string MiddleTrack = "res://audio/bgm/reverberation_ensemble/middle.ogg";
    private const string UpperTrack = "res://audio/bgm/reverberation_ensemble/upper.ogg";
    private const string BlueTrack = "res://audio/bgm/reverberation_ensemble/blue_reverberation.ogg";
    private const string PlayerNodeName = "ReverberationEnsembleBgmPlayer";
    private static AudioStreamPlayer? _player;
    private static NRun? _playerHost;
    private static RunState? _run;
    private static string? _track;
    private static int _wonRow = -1;
    private static bool _lost;
    private static bool _syncing;
    private static bool _initialized;
    private static bool _cleaningUp;

    internal static bool IsActScope =>
        LibraryOfRuinaSettings.RuntimeSideEffectsEnabled
        && RunManager.Instance.DebugOnlyGetState()?.Act is ReverberationEnsembleAct;

    internal static bool OwnsMusic => ResolveTrack() != null;

    internal static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        RunManager.Instance.RunStarted += OnRunStarted;
        RunManager.Instance.RoomEntered += Sync;
        CombatManager.Instance.CombatSetUp += OnCombatSetUp;
        CombatManager.Instance.CombatWon += OnCombatWon;
    }

    private static void OnRunStarted(RunState state)
    {
        Reset();
        _cleaningUp = false;
        _run = state;
        Sync();
    }

    private static void OnCombatSetUp(CombatState state)
    {
        Sync();
    }

    private static void OnCombatWon(CombatRoom room)
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (state?.Act is not ReverberationEnsembleAct
            || !ReferenceEquals(room.CombatState.RunState, state))
        {
            return;
        }

        _run = state;
        _wonRow = state.CurrentMapCoord?.row ?? -1;
        Sync();
    }

    internal static void OnCombatLost()
    {
        _lost = true;
        Stop();
    }

    private static string? ResolveTrack()
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (!IsActScope || _cleaningUp || NonInteractiveMode.IsActive || !RunManager.Instance.IsInProgress
            || state == null || (ReferenceEquals(_run, state) && _lost))
        {
            return null;
        }

        int row = state.CurrentMapCoord?.row ?? 0;
        // 地图坐标在进入战斗时已推进；只有胜利事件或读档恢复的已完成房间才提前换曲。
        bool wonCurrentRoom = (ReferenceEquals(_run, state) && _wonRow == row)
            || state.CurrentRoom is CombatRoom { IsPreFinished: true }
            || state.BaseRoom is CombatRoom { IsPreFinished: true };
        if (row > ReverberationEnsembleActMap.UpperSupplyRow && wonCurrentRoom)
        {
            return null;
        }

        if (row > ReverberationEnsembleActMap.UpperReceptionRow
            || (row == ReverberationEnsembleActMap.UpperReceptionRow && wonCurrentRoom))
        {
            return BlueTrack;
        }

        if (row > ReverberationEnsembleActMap.MiddleReceptionRow
            || (row == ReverberationEnsembleActMap.MiddleReceptionRow && wonCurrentRoom))
        {
            return UpperTrack;
        }

        if (row > ReverberationEnsembleActMap.LowerReceptionRow
            || (row == ReverberationEnsembleActMap.LowerReceptionRow && wonCurrentRoom))
        {
            return MiddleTrack;
        }

        return LowerTrack;
    }

    internal static void Sync()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            RunState? state = RunManager.Instance.DebugOnlyGetState();
            if (!ReferenceEquals(_run, state))
            {
                Reset();
                _run = state;
            }

            string? nextTrack = ResolveTrack();
            if (nextTrack == null)
            {
                Stop();
                return;
            }

            AudioStreamPlayer? player = EnsurePlayer();
            if (player == null || !player.IsInsideTree())
            {
                // 播放器入树时由 TreeEntered 单次信号补同步；此处不排队延迟调用，避免在同一帧消息队列里自我重排。
                return;
            }

            bool changed = nextTrack != _track;
            if (changed)
            {
                AudioStreamOggVorbis source = GD.Load<AudioStreamOggVorbis>(nextTrack);
                AudioStreamOggVorbis stream = (AudioStreamOggVorbis)source.Duplicate();
                stream.Loop = true;
                player.Stop();
                player.Stream = stream;
                _track = nextTrack;
            }

            RefreshVolume();
            LibraryBgmPlaybackCoordinator.StopBeforePlayback(player);
            if (changed || !player.Playing)
            {
                player.Play();
                Log.Info("[ReverberationEnsembleBGM] Playing " + nextTrack);
            }
        }
        catch (Exception exception)
        {
            Log.Error("[ReverberationEnsembleBGM] Playback failed: " + exception);
        }
        finally
        {
            _syncing = false;
        }
    }

    internal static void RefreshVolume()
    {
        if (IsPlayerAlive())
        {
            // 音乐保留原始增益，仅跟随游戏 BGM 音量，不套用音效的音量倍率。
            float volume = Mathf.Clamp(SaveManager.Instance.SettingsSave.VolumeBgm, 0f, 1f);
            _player!.VolumeDb = volume > 0f ? Mathf.LinearToDb(volume * volume) : -80f;
        }
    }

    internal static void Reset()
    {
        Stop();
        _run = null;
        _wonRow = -1;
        _lost = false;
    }

    internal static void OnRunCleaningUp()
    {
        _cleaningUp = true;
        Reset();
    }

    private static AudioStreamPlayer? EnsurePlayer()
    {
        NRun? host = NRun.Instance;
        if (IsPlayerAlive() && ReferenceEquals(_playerHost, host))
        {
            return _player;
        }

        // QuickRestart 等流程会替换 NRun：旧播放器随旧 NRun 移出场景树但在帧末才释放，需丢弃后挂到当前 NRun。
        ReleasePlayer();
        if (host == null || !GodotObject.IsInstanceValid(host) || host.IsQueuedForDeletion())
        {
            return null;
        }

        AudioStreamPlayer player = new()
        {
            Name = PlayerNodeName,
            Bus = "Master"
        };
        player.Connect(
            Node.SignalName.TreeEntered,
            Callable.From(Sync),
            (uint)GodotObject.ConnectFlags.OneShot);
        _player = player;
        _playerHost = host;
        host.AddChildSafely(player);
        return player;
    }

    private static void ReleasePlayer()
    {
        if (IsPlayerAlive())
        {
            _player!.Stop();
            _player.Stream = null;
            _player.QueueFree();
        }

        _player = null;
        _playerHost = null;
        _track = null;
    }

    private static void Stop()
    {
        if (IsPlayerAlive())
        {
            _player!.Stop();
            _player.Stream = null;
        }

        _track = null;
    }

    private static bool IsPlayerAlive() =>
        _player != null && GodotObject.IsInstanceValid(_player) && !_player.IsQueuedForDeletion();
}

[HarmonyPatch]
internal static class ReverberationEnsembleNativeMusicPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NRunMusicController), nameof(NRunMusicController.UpdateMusic));
        yield return AccessTools.Method(typeof(NRunMusicController), nameof(NRunMusicController.PlayCustomMusic));
        yield return AccessTools.Method(typeof(NRunMusicController), nameof(NRunMusicController.StopCustomMusic));
    }

    private static bool Prefix(NRunMusicController __instance)
    {
        if (!ReverberationEnsembleBgmController.OwnsMusic)
        {
            ReverberationEnsembleBgmController.Sync();
            return true;
        }

        __instance.UpdateAmbience();
        ReverberationEnsembleBgmController.Sync();
        return false;
    }
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateTrack), new Type[] { })]
internal static class ReverberationEnsembleTrackPatch
{
    private static void Postfix() => ReverberationEnsembleBgmController.Sync();
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.LoseCombat))]
internal static class ReverberationEnsembleMusicLossPatch
{
    private static void Prefix() => ReverberationEnsembleBgmController.OnCombatLost();
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.SetBgmVol))]
internal static class ReverberationEnsembleMusicVolumePatch
{
    private static void Postfix() => ReverberationEnsembleBgmController.RefreshVolume();
}

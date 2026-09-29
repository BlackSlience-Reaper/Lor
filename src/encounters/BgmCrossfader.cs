using System;
using Godot;
using LibraryOfRuina.audio;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.encounters;

/// <summary>
/// 遭遇 BGM 的两个播放器、淡入淡出 Tween 与音量换算。会话状态（当前曲目、是否在切换）归 <see cref="BgmSession"/>，
/// 这里只管节点：播放器挂在 NRun（没有时挂在 NGame）下，会话结束时释放，下次开始时重建。
/// <para>
/// 这里的异常保护只有原来就有的几处（Tween 与播放器已释放时的 <see cref="ObjectDisposedException"/>）；
/// 切换曲目时的播放调用没有保护，调用方在死亡事件与回合事件里，异常会沿这些事件抛出。
/// </para>
/// </summary>
internal static class BgmCrossfader
{
    internal const float FadeDurationSeconds = 1.25f;
    internal const float MaxVolumeDb = 4f;
    internal const float MinVolumeDb = -40f;

    private static Node? _hostNode;
    private static AudioStreamPlayer? _playerA;
    private static AudioStreamPlayer? _playerB;
    private static AudioStreamPlayer? _activePlayer;
    private static AudioStreamPlayer? _inactivePlayer;
    private static Tween? _fadeTween;

    internal static AudioStreamPlayer? ActivePlayer => _activePlayer;

    internal static AudioStreamPlayer? InactivePlayer => _inactivePlayer;

    internal static bool HasHostAndPlayers =>
        _hostNode != null && _activePlayer != null && _inactivePlayer != null;

    internal static void EnsureHostAndPlayers()
    {
        if (!IsAlive(_hostNode))
        {
            _hostNode = NRun.Instance ?? (Node?)NGame.Instance;
        }

        if (_hostNode == null)
        {
            Log.Error("[EncounterBGM] Host node is null.");
            return;
        }

        if (!IsAlive(_playerA))
        {
            _playerA = CreatePlayer("EncounterBgmPlayerA");
            _hostNode.AddChildSafely(_playerA);
        }

        if (!IsAlive(_playerB))
        {
            _playerB = CreatePlayer("EncounterBgmPlayerB");
            _hostNode.AddChildSafely(_playerB);
        }

        _activePlayer = _playerA;
        _inactivePlayer = _playerB;
    }

    private static AudioStreamPlayer CreatePlayer(string name)
    {
        return new AudioStreamPlayer
        {
            Name = name,
            Bus = "Master",
            VolumeDb = MinVolumeDb
        };
    }

    internal static AudioStream? LoadTrack(EncounterBgmConfig config, int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= config.TrackPaths.Length)
        {
            return null;
        }

        string trackPath = config.TrackPaths[trackIndex];
        AudioStream? stream = ResourceLoader.Load<AudioStream>(trackPath);
        if (stream == null)
        {
            Log.Error("[" + config.LogTag + "] ResourceLoader failed: " + trackPath);
            return null;
        }

        Log.Info("[" + config.LogTag + "] Loaded track resource: " + trackPath);
        if (stream is AudioStreamOggVorbis ogg)
        {
            AudioStreamOggVorbis looped = (AudioStreamOggVorbis)ogg.Duplicate();
            looped.Loop = true;
            return looped;
        }

        return stream;
    }

    /// <summary>
    /// 停掉两个播放器，从 <paramref name="positionSeconds"/> 起在当前播放器上直接播放（不淡入）。
    /// 调用前须已由 <see cref="EnsureHostAndPlayers"/> 建好两个播放器。
    /// </summary>
    internal static void PlayFirstTrack(AudioStream firstTrack, float maxVolumeDb, float positionSeconds)
    {
        StopAndResetPlayer(_activePlayer);
        StopAndResetPlayer(_inactivePlayer);
        _activePlayer!.Stream = firstTrack;
        _activePlayer.VolumeDb = maxVolumeDb;
        _inactivePlayer!.VolumeDb = MinVolumeDb;
        LibraryBgmPlaybackCoordinator.StopBeforePlayback(_activePlayer, _inactivePlayer);
        _activePlayer.Play(MathF.Max(0f, positionSeconds));
    }

    /// <summary>
    /// 在备用播放器上以最小音量开始播放下一首；任一播放器已失效时什么也不做并返回 false。
    /// 之后由 <see cref="RunFade"/> 建立淡入淡出。两步分开，是为了让会话在两者之间标记“正在切换”，与拆分前的顺序一致。
    /// </summary>
    internal static bool TryStartIncoming(
        AudioStream nextTrack,
        out AudioStreamPlayer fromPlayer,
        out AudioStreamPlayer toPlayer)
    {
        KillFadeTween();

        fromPlayer = _activePlayer!;
        toPlayer = _inactivePlayer!;
        if (!IsAlive(fromPlayer) || !IsAlive(toPlayer))
        {
            return false;
        }

        toPlayer.Stop();
        toPlayer.Stream = nextTrack;
        toPlayer.VolumeDb = MinVolumeDb;
        LibraryBgmPlaybackCoordinator.StopBeforePlayback(fromPlayer, toPlayer);
        toPlayer.Play();
        return true;
    }

    /// <summary>
    /// 并行淡出 <paramref name="fromPlayer"/>、淡入 <paramref name="toPlayer"/>；结束时停掉旧播放器、交换两者角色，
    /// 再调用 <paramref name="onFinished"/>。
    /// </summary>
    internal static void RunFade(
        AudioStreamPlayer fromPlayer,
        AudioStreamPlayer toPlayer,
        float targetVolumeDb,
        Action onFinished)
    {
        _fadeTween = _hostNode!.CreateTween();
        _fadeTween.SetParallel();
        _fadeTween.TweenProperty(fromPlayer, "volume_db", MinVolumeDb, FadeDurationSeconds);
        _fadeTween.TweenProperty(toPlayer, "volume_db", targetVolumeDb, FadeDurationSeconds);
        _fadeTween.Finished += () =>
        {
            fromPlayer.Stop();
            fromPlayer.VolumeDb = MinVolumeDb;
            _activePlayer = toPlayer;
            _inactivePlayer = fromPlayer;
            _fadeTween = null;
            onFinished();
        };
    }

    internal static void SetActivePlayerVolume(float volumeDb)
    {
        if (IsAlive(_activePlayer))
        {
            _activePlayer!.VolumeDb = volumeDb;
        }
    }

    /// <summary>停止并释放两个播放器；下次会话开始时由 <see cref="EnsureHostAndPlayers"/> 重建。</summary>
    internal static void StopAndDisposePlayers()
    {
        StopAndResetPlayer(_playerA);
        StopAndResetPlayer(_playerB);
        DisposePlayer(ref _playerA);
        DisposePlayer(ref _playerB);

        _activePlayer = null;
        _inactivePlayer = null;
    }

    internal static bool IsAlive(GodotObject? obj)
    {
        return obj != null
            && GodotObject.IsInstanceValid(obj)
            && (obj is not Node node
                || (!node.IsQueuedForDeletion() && node.IsInsideTree()));
    }

    internal static void KillFadeTween()
    {
        if (!IsAlive(_fadeTween))
        {
            _fadeTween = null;
            return;
        }

        try
        {
            _fadeTween!.Kill();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _fadeTween = null;
        }
    }

    private static void StopAndResetPlayer(AudioStreamPlayer? player)
    {
        if (!IsAlive(player))
        {
            return;
        }

        try
        {
            player!.Stop();
            player.VolumeDb = MinVolumeDb;
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void DisposePlayer(ref AudioStreamPlayer? player)
    {
        if (!IsAlive(player))
        {
            player = null;
            return;
        }

        try
        {
            player!.QueueFree();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            player = null;
        }
    }

    private static float ResolveScaledMaxVolumeDb(float volumeScale)
    {
        float clampedScale = Mathf.Clamp(volumeScale, 0f, 1f);
        if (clampedScale <= 0f)
        {
            return MinVolumeDb;
        }

        if (Mathf.IsEqualApprox(clampedScale, 1f))
        {
            return MaxVolumeDb;
        }

        return MaxVolumeDb + Mathf.LinearToDb(clampedScale);
    }

    /// <summary>配置的音量缩放乘以游戏 BGM 音量的平方，换算成播放器的最大音量（dB）。</summary>
    internal static float ResolveEffectiveMaxVolumeDb(EncounterBgmConfig config)
    {
        float globalBgmScale = ResolveGlobalBgmVolumeScale();
        float effectiveScale = config.VolumeScale * globalBgmScale;
        return ResolveScaledMaxVolumeDb(effectiveScale);
    }

    private static float ResolveGlobalBgmVolumeScale()
    {
        try
        {
            float rawVolume = SaveManager.Instance?.SettingsSave?.VolumeBgm ?? 1f;
            float clampedVolume = Mathf.Clamp(rawVolume, 0f, 1f);
            return Mathf.Pow(clampedVolume, 2f);
        }
        catch (Exception ex)
        {
            Log.Warn("[EncounterBGM] Failed to read global BGM volume, fallback to 1.0: " + ex.Message);
            return 1f;
        }
    }
}

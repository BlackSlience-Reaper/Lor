using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.infra.lifecycle;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.framework.audio;

internal static class MainMenuBgmController
{
    [Flags]
    private enum PauseReason
    {
        None = 0,
        Combat = 1 << 0,
        ExternalGlobalMusic = 1 << 1
    }

    private const string LogTag = "MainMenuBGM";
    private const string HostNodeName = "LibraryOfRuinaMainMenuBgmHost";
    private const string PlayerNodeName = "MainMenuBgmPlayer";
    private const string BaseMenuMusicPath = "event:/music/menu_update";
    private const string MenuProgressParameter = "menu_progress";
    private const string TrackPath = "res://audio/bgm/main_menu/custom_main_menu.ogg";

    private const float MaxVolumeDb = 0f;
    private const float MinVolumeDb = -80f;

    private static Node? _hostNode;
    private static AudioStreamPlayer? _player;
    private static bool _initialized;
    private static bool _isPlaying;
    private static bool _isPaused;
    private static float _resumePositionSeconds;
    private static PauseReason _pauseReasons = PauseReason.None;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        RunManager.Instance.RunStarted += OnRunStarted;
        RunManager.Instance.RoomEntered += OnRoomLocationChanged;
        RunManager.Instance.RoomExited += OnRoomLocationChanged;
        CombatManager.Instance.CombatSetUp += OnCombatSetUp;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    public static bool TryHandleBasePlayMusic(string music)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            Stop();
            return false;
        }

        if (!string.Equals(music, BaseMenuMusicPath, StringComparison.Ordinal))
        {
            AddPauseReason(PauseReason.ExternalGlobalMusic);
            return false;
        }

        if (!LibraryOfRuinaSettings.MainMenuBgmEnabled)
        {
            Stop();
            return false;
        }

        ClearRunScopedPauseReasons();
        Sync();
        return IsPlayingOrPaused();
    }

    public static bool ShouldSuppressMenuProgressParameter(string parameter)
    {
        return LibraryOfRuinaSettings.RuntimeSideEffectsEnabled &&
            _isPlaying &&
            LibraryOfRuinaSettings.MainMenuBgmEnabled &&
            string.Equals(parameter, MenuProgressParameter, StringComparison.Ordinal);
    }

    public static bool ShouldOwnRunMusic()
    {
        return LibraryOfRuinaSettings.RuntimeSideEffectsEnabled
            && LibraryOfRuinaSettings.MainMenuBgmEnabled
            && !NonInteractiveMode.IsActive
            && IsMainMenuActive();
    }

    public static bool TryResumeOwnedRunMusic()
    {
        if (!ShouldOwnRunMusic())
        {
            return false;
        }

        Sync();
        return IsPlayingOrPaused();
    }

    public static void Start()
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            Stop();
            return;
        }

        ClearRunScopedPauseReasons();
        Sync();
    }

    public static void Sync()
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            Stop();
            return;
        }

        if (ShouldPlay())
        {
            StartOrRefresh();
            return;
        }

        if (!LibraryOfRuinaSettings.MainMenuBgmEnabled || NonInteractiveMode.IsActive || !IsMainMenuActive())
        {
            Stop();
            return;
        }

        Pause();
    }

    public static void OnRunMusicUpdated()
    {
        Sync();
    }

    public static void OnRunMusicStoppedExternally()
    {
        if (IsCombatActiveOrStarting())
        {
            AddPauseReason(PauseReason.Combat);
        }
    }

    public static void OnGlobalMusicStoppedExternally()
    {
        if (LibraryBgmPlaybackCoordinator.IsStoppingCompetingMusic)
        {
            return;
        }

        ClearPauseReason(PauseReason.ExternalGlobalMusic);
    }

    public static void ForceStop()
    {
        Stop();
    }

    public static void RefreshVolumeFromSettings()
    {
        if (!IsAlive(_player))
        {
            return;
        }

        _player!.VolumeDb = ResolveEffectiveVolumeDb();
    }

    public static void OnSettingChanged()
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            Stop();
            return;
        }

        if (LibraryOfRuinaSettings.MainMenuBgmEnabled)
        {
            NonCombatRunBgmController.Sync();
            Sync();
            return;
        }

        Stop();
        NonCombatRunBgmController.Sync();
        if (IsMainMenuActive())
        {
            NAudioManager.Instance?.PlayMusic(BaseMenuMusicPath);
        }
    }

    private static void OnRunStarted(RunState _)
    {
        _pauseReasons = PauseReason.None;
        _resumePositionSeconds = 0f;
        Stop();
    }

    private static void OnRoomLocationChanged()
    {
        if ((_pauseReasons & PauseReason.Combat) != 0 && !IsCombatActiveOrStarting())
        {
            _pauseReasons &= ~PauseReason.Combat;
        }

        Sync();
    }

    private static void OnCombatSetUp(CombatState _)
    {
        AddPauseReason(PauseReason.Combat);
    }

    private static void OnCombatEnded(CombatRoom _)
    {
        _pauseReasons &= ~PauseReason.Combat;
        SyncDeferred();
    }

    private static bool ShouldPlay()
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            return false;
        }

        if (!LibraryOfRuinaSettings.MainMenuBgmEnabled)
        {
            return false;
        }

        if (NonInteractiveMode.IsActive)
        {
            return false;
        }

        if (!IsMainMenuActive())
        {
            return false;
        }

        if (_pauseReasons != PauseReason.None)
        {
            return false;
        }

        return true;
    }

    private static bool IsCombatActiveOrStarting()
    {
        CombatManager? combatManager = CombatManager.Instance;
        return combatManager != null &&
            (combatManager.IsInProgress || CurrentCombat.Of(combatManager) != null);
    }

    private static void AddPauseReason(PauseReason reason)
    {
        PauseReason previous = _pauseReasons;
        _pauseReasons |= reason;
        if (_pauseReasons == previous && !_isPlaying)
        {
            return;
        }

        Pause();
    }

    private static void ClearPauseReason(PauseReason reason)
    {
        PauseReason previous = _pauseReasons;
        _pauseReasons &= ~reason;
        if (_pauseReasons == previous && _isPlaying)
        {
            return;
        }

        Sync();
    }

    private static void ClearRunScopedPauseReasons()
    {
        _pauseReasons &= ~(PauseReason.Combat | PauseReason.ExternalGlobalMusic);
    }

    private static void StartOrRefresh()
    {
        if (!LibraryOfRuinaSettings.MainMenuBgmEnabled || NonInteractiveMode.IsActive)
        {
            Stop();
            return;
        }

        NonCombatRunBgmController.Sync();

        Node? host = EnsureHost();
        if (host == null)
        {
            return;
        }

        AudioStreamPlayer? player = EnsurePlayer(host);
        if (player == null)
        {
            return;
        }

        player.VolumeDb = ResolveEffectiveVolumeDb();

        if (player.Playing)
        {
            if (player.StreamPaused)
            {
                LibraryBgmPlaybackCoordinator.StopBeforePlayback(player);
            }

            player.StreamPaused = false;
            _isPlaying = true;
            _isPaused = false;
            return;
        }

        if (player.Stream == null)
        {
            AudioStream? stream = LoadLoopStream();
            if (stream == null)
            {
                return;
            }

            player.Stream = stream;
        }

        LibraryBgmPlaybackCoordinator.StopBeforePlayback(player);
        player.Play(MathF.Max(0f, _resumePositionSeconds));
        _isPlaying = true;
        _isPaused = false;
        Log.Info("[" + LogTag + "] Started loop: " + TrackPath);
    }

    private static void Pause()
    {
        if (IsAlive(_player))
        {
            AudioStreamPlayer player = _player!;
            if (player.Playing)
            {
                _resumePositionSeconds = player.GetPlaybackPosition();
                player.StreamPaused = true;
            }

            _isPaused = player.Stream != null;
        }

        _isPlaying = false;

    }

    private static void Stop()
    {
        if (IsAlive(_player))
        {
            _player!.Stop();
            _player.Stream = null;
            _player.VolumeDb = MinVolumeDb;
        }

        _isPlaying = false;
        _isPaused = false;
        _pauseReasons = PauseReason.None;
        _resumePositionSeconds = 0f;

    }

    private static bool IsMainMenuActive()
    {
        return NGame.Instance?.RootSceneContainer?.CurrentScene is NMainMenu;
    }

    private static Node? EnsureHost()
    {
        if (IsAlive(_hostNode))
        {
            return _hostNode;
        }

        Node? hostParent = NGame.Instance;
        if (hostParent == null)
        {
            return null;
        }

        Node host = hostParent.GetNodeOrNull<Node>(HostNodeName) ?? new Node { Name = HostNodeName };
        if (host.GetParent() == null)
        {
            hostParent.AddChildSafely(host);
        }

        _hostNode = host;
        return _hostNode;
    }

    private static AudioStreamPlayer? EnsurePlayer(Node host)
    {
        if (IsAlive(_player))
        {
            return _player;
        }

        AudioStreamPlayer player = host.GetNodeOrNull<AudioStreamPlayer>(PlayerNodeName) ?? new AudioStreamPlayer
        {
            Name = PlayerNodeName,
            Bus = "Master",
            VolumeDb = MinVolumeDb
        };

        if (player.GetParent() == null)
        {
            host.AddChildSafely(player);
        }

        _player = player;
        return _player;
    }

    private static AudioStream? LoadLoopStream()
    {
        AudioStream? stream = ResourceLoader.Load<AudioStream>(TrackPath);
        if (stream == null)
        {
            LorLog.ErrorOnce(
                "MainMenuBgmController.LoadLoopStream",
                "[" + LogTag + "] Unable to load audio stream: " + TrackPath);

            return null;
        }

        if (stream is AudioStreamOggVorbis ogg)
        {
            AudioStreamOggVorbis looped = (AudioStreamOggVorbis)ogg.Duplicate();
            looped.Loop = true;
            return looped;
        }

        return stream;
    }

    private static float ResolveEffectiveVolumeDb()
    {
        float rawVolume = SaveManager.Instance?.SettingsSave?.VolumeBgm ?? 1f;
        float effectiveScale = Mathf.Pow(Mathf.Clamp(rawVolume, 0f, 1f), 2f)
            * (float)LibraryOfRuinaSettings.MainMenuBgmVolume;
        if (effectiveScale <= 0f)
        {
            return MinVolumeDb;
        }

        return MaxVolumeDb + Mathf.LinearToDb(effectiveScale);
    }

    private static bool IsAlive(GodotObject? obj)
    {
        return obj != null
            && GodotObject.IsInstanceValid(obj)
            && (obj is not Node node
                || (!node.IsQueuedForDeletion() && node.IsInsideTree()));
    }

    private static bool IsPlayingOrPaused()
    {
        return _isPlaying || _isPaused;
    }

    private static void SyncDeferred()
    {
        if (NGame.Instance == null)
        {
            Sync();
            return;
        }

        Callable.From(Sync).CallDeferred();
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class MainMenuBgmReadyPatch
{
    private static void Postfix()
    {
        MainMenuBgmController.Start();
    }
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayMusic), typeof(string))]
[LibraryPatch(Reason = "原版在 NMainMenu._Ready 内联调用 PlayMusic 播放菜单曲，无 Hook 或虚方法；仅在本模组主菜单 BGM 开关开启且自有播放器接管时跳过 menu_update，其余曲目放行。")]
internal static class MainMenuBgmPlayMusicPatch
{
    private static bool Prefix(string music)
    {
        return !MainMenuBgmController.TryHandleBasePlayMusic(music);
    }
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.UpdateMusicParameter), typeof(string), typeof(string))]
[LibraryPatch(Reason = "原版 NMainMenu 切换子菜单时直接调用 UpdateMusicParameter(\"menu_progress\")，无 Hook；仅在本模组主菜单 BGM 接管播放时丢弃该参数。")]
internal static class MainMenuBgmMusicParameterPatch
{
    private static bool Prefix(string parameter)
    {
        return !MainMenuBgmController.ShouldSuppressMenuProgressParameter(parameter);
    }
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.StopMusic))]
internal static class MainMenuBgmStopMusicPatch
{
    private static void Postfix()
    {
        MainMenuBgmController.OnGlobalMusicStoppedExternally();
    }
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.SetBgmVol))]
internal static class MainMenuBgmVolumePatch
{
    private static void Postfix()
    {
        MainMenuBgmController.RefreshVolumeFromSettings();
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame.Quit))]
internal static class MainMenuBgmQuitPatch
{
    private static void Prefix()
    {
        MainMenuBgmController.ForceStop();
    }
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateMusic))]
internal static class MainMenuBgmRunMusicUpdatePatch
{
    private static void Postfix()
    {
        MainMenuBgmController.OnRunMusicUpdated();
    }
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateTrack), new Type[] { })]
internal static class MainMenuBgmRunMusicTrackPatch
{
    private static void Postfix()
    {
        MainMenuBgmController.Sync();
    }
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.StopMusic))]
internal static class MainMenuBgmRunMusicStopPatch
{
    private static void Postfix()
    {
        MainMenuBgmController.OnRunMusicStoppedExternally();
    }
}

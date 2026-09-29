using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.encounters;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.audio;

internal static class NonCombatRunBgmController
{
    private const string LogTag = "NonCombatRunBGM";
    private const string HostNodeName = "LibraryOfRuinaNonCombatRunBgmHost";
    private const string PlayerNodeName = "NonCombatRunBgmPlayer";
    private const string TrackPath = "res://audio/bgm/non_combat/non_combat_loop.ogg";

    private const float MaxVolumeDb = 4f;
    private const float MinVolumeDb = -80f;

    private static readonly StringName StopMusicCallback = new("stop_music");
    private static Node? _hostNode;
    private static AudioStreamPlayer? _player;
    private static bool _initialized;
    private static bool _isPlaying;
    private static bool _isPausedForRoom;
    private static bool _baseRunMusicStoppedByController;
    private static bool _baseRunMusicStoppedUsingPublicApi;
    private static bool _externalMusicOwnsOutput;
    private static bool _combatLossPending;
    private static bool _suppressRunMusicStopHook;
    private static bool _isRestoringRunMusic;
    private static bool _deferredSyncPending;
    private static bool _loggedLoadError;
    private static float _resumePositionSeconds;

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

        Sync();
    }

    public static void Sync(
        NRunMusicController? runMusicController = null,
        bool baseRunMusicMayHaveRestarted = false)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            StopLoop(restoreRunMusic: true);
            return;
        }

        if (_isRestoringRunMusic)
        {
            return;
        }

        try
        {
            runMusicController ??= NRunMusicController.Instance;
            if (!(RunManager.Instance?.IsInProgress ?? false))
            {
                StopLoop(restoreRunMusic: false);
                return;
            }

            if (ShouldPlay(runMusicController))
            {
                StartOrRefresh(runMusicController, baseRunMusicMayHaveRestarted);
            }
            else
            {
                PauseLoop(restoreRunMusic: ShouldRestoreRunMusic());
            }
        }
        catch (Exception ex)
        {
            Log.Error("[" + LogTag + "] Sync failed: " + ex);
        }
    }

    public static void OnMapOpened(bool _)
    {
        Sync();
    }

    public static void OnMapClosed()
    {
        Sync();
    }

    public static void OnRunMusicStoppedExternally()
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            return;
        }

        if (_suppressRunMusicStopHook)
        {
            return;
        }

        _externalMusicOwnsOutput = true;

        if (_isPlaying)
        {
            Log.Info("[" + LogTag + "] Run music stopped externally; yielding to room audio.");
        }

        PauseLoop(restoreRunMusic: false);
    }

    public static void OnRunMusicUpdated(NRunMusicController runMusicController)
    {
        if (_isRestoringRunMusic)
        {
            return;
        }

        if (!IsDedicatedCombatMusicActive())
        {
            _externalMusicOwnsOutput = false;
        }

        Sync(runMusicController, baseRunMusicMayHaveRestarted: true);
    }

    public static void OnCombatLossPending()
    {
        _combatLossPending = true;
        PauseLoop(restoreRunMusic: false);
    }

    internal static bool IsCombatLossPending => _combatLossPending;

    public static void RefreshVolumeFromSettings()
    {
        if (!IsAlive(_player) || (!_isPlaying && !_isPausedForRoom))
        {
            return;
        }

        _player!.VolumeDb = ResolveEffectiveVolumeDb();
    }

    public static void OnSettingChanged()
    {
        if (!_initialized)
        {
            return;
        }

        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            StopLoop(restoreRunMusic: true);
            return;
        }

        if (LibraryOfRuinaSettings.NonCombatRunBgmEnabled)
        {
            Sync();
            return;
        }

        StopLoop(restoreRunMusic: true);
    }

    private static void OnRunStarted(RunState _)
    {
        _isPausedForRoom = false;
        _baseRunMusicStoppedByController = false;
        _baseRunMusicStoppedUsingPublicApi = false;
        _externalMusicOwnsOutput = false;
        _combatLossPending = false;
        _resumePositionSeconds = 0f;
        Sync();
    }

    private static void OnRoomLocationChanged()
    {
        Sync();
    }

    private static void OnCombatSetUp(CombatState combatState)
    {
        _combatLossPending = false;
        bool hasDedicatedCombatMusic = EncounterBgmController.HasBgmForEncounter(combatState)
            || AbnormalityEliteBgmController.HasBgmForCombat(combatState);

        PauseLoop(restoreRunMusic: false);
        if (hasDedicatedCombatMusic)
        {
            return;
        }

        _externalMusicOwnsOutput = false;
        RestoreRunMusicSafely();
    }

    private static bool ShouldPlay(NRunMusicController? _)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            return false;
        }

        if (!LibraryOfRuinaSettings.NonCombatRunBgmEnabled)
        {
            return false;
        }

        if (MainMenuBgmController.ShouldOwnRunMusic())
        {
            return false;
        }

        if (NonInteractiveMode.IsActive)
        {
            return false;
        }

        if (!(RunManager.Instance?.IsInProgress ?? false))
        {
            return false;
        }

        if (RunManager.Instance.DebugOnlyGetState()?.Act is not LibraryOfRuinaActModel)
        {
            return false;
        }

        if (_externalMusicOwnsOutput || _combatLossPending)
        {
            return false;
        }

        CombatManager? combatManager = CombatManager.Instance;
        if (combatManager?.IsStarting == true || combatManager?.IsInProgress == true)
        {
            return false;
        }

        return !IsDedicatedCombatMusicActive();
    }

    private static bool ShouldRestoreRunMusic()
    {
        if (!_baseRunMusicStoppedByController)
        {
            return false;
        }

        if (MainMenuBgmController.ShouldOwnRunMusic())
        {
            return false;
        }

        if (_externalMusicOwnsOutput || _combatLossPending || IsDedicatedCombatMusicActive())
        {
            return false;
        }

        CombatManager? combatManager = CombatManager.Instance;
        if (combatManager?.IsStarting == true || combatManager?.IsInProgress == true)
        {
            return false;
        }

        return RunManager.Instance?.IsInProgress ?? false;
    }

    private static bool IsDedicatedCombatMusicActive()
    {
        return EncounterBgmController.IsRunning || AbnormalityEliteBgmController.IsRunning;
    }

    private static void StartOrRefresh(
        NRunMusicController? runMusicController,
        bool baseRunMusicMayHaveRestarted)
    {
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

        if (!host.IsInsideTree() || !player.IsInsideTree())
        {
            ScheduleDeferredSync();
            return;
        }

        if (runMusicController != null
            && (baseRunMusicMayHaveRestarted || !_baseRunMusicStoppedByController))
        {
            StopBaseRunMusicOnly(runMusicController);
        }

        player.VolumeDb = ResolveEffectiveVolumeDb();

        if (player.Playing)
        {
            if (player.StreamPaused || baseRunMusicMayHaveRestarted)
            {
                LibraryBgmPlaybackCoordinator.StopBeforePlayback(player);
            }

            if (player.StreamPaused)
            {
                player.StreamPaused = false;
                Log.Info("[" + LogTag + "] Resumed loop at " + _resumePositionSeconds.ToString("0.00") + "s.");
            }

            _isPlaying = true;
            _isPausedForRoom = false;
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
        _isPausedForRoom = false;
        Log.Info("[" + LogTag + "] Started loop: " + TrackPath);
    }

    private static void ScheduleDeferredSync()
    {
        if (_deferredSyncPending)
        {
            return;
        }

        _deferredSyncPending = true;
        Callable.From(() =>
        {
            _deferredSyncPending = false;
            if (!IsAlive(_hostNode)
                || !IsAlive(_player)
                || !_hostNode!.IsInsideTree()
                || !_player!.IsInsideTree())
            {
                return;
            }

            Sync();
        }).CallDeferred();
    }

    private static Node? EnsureHost()
    {
        if (IsAlive(_hostNode))
        {
            return _hostNode;
        }

        Node? hostParent = NRun.Instance ?? (Node?)NGame.Instance;
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
            if (!_loggedLoadError)
            {
                _loggedLoadError = true;
                Log.Error("[" + LogTag + "] Unable to load audio stream: " + TrackPath);
            }

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

    private static void StopBaseRunMusicOnly(NRunMusicController runMusicController)
    {
        try
        {
            if (VanillaPrivate.RunMusicControllerProxy.Get(runMusicController) is Node proxy)
            {
                proxy.Call(StopMusicCallback);
                _baseRunMusicStoppedByController = true;
                _baseRunMusicStoppedUsingPublicApi = false;
                return;
            }

            _suppressRunMusicStopHook = true;
            runMusicController.StopMusic();
            _baseRunMusicStoppedByController = true;
            _baseRunMusicStoppedUsingPublicApi = true;
        }
        catch (Exception ex)
        {
            Log.Warn("[" + LogTag + "] Failed to stop base run music: " + ex.Message);
        }
        finally
        {
            _suppressRunMusicStopHook = false;
        }
    }

    private static void PauseLoop(bool restoreRunMusic)
    {
        if (IsAlive(_player))
        {
            AudioStreamPlayer player = _player!;
            if (player.Playing)
            {
                _resumePositionSeconds = player.GetPlaybackPosition();
                player.StreamPaused = true;
            }

            _isPausedForRoom = player.Stream != null;
        }

        _isPlaying = false;

        if (restoreRunMusic)
        {
            RestoreRunMusicSafely();
        }
    }

    private static void StopLoop(bool restoreRunMusic)
    {
        if (IsAlive(_player))
        {
            _player!.Stop();
            _player.Stream = null;
            _player.VolumeDb = MinVolumeDb;
        }

        _isPlaying = false;
        _isPausedForRoom = false;
        _resumePositionSeconds = 0f;

        if (restoreRunMusic)
        {
            RestoreRunMusicSafely();
        }
        else
        {
            _baseRunMusicStoppedByController = false;
            _baseRunMusicStoppedUsingPublicApi = false;
        }
    }

    private static void RestoreRunMusicSafely()
    {
        if (!_baseRunMusicStoppedByController)
        {
            return;
        }

        if (!(RunManager.Instance?.IsInProgress ?? false))
        {
            return;
        }

        if (RunManager.Instance?.DebugOnlyGetState()?.CurrentRoom == null)
        {
            return;
        }

        if (MainMenuBgmController.ShouldOwnRunMusic()
            || _externalMusicOwnsOutput
            || _combatLossPending
            || IsDedicatedCombatMusicActive())
        {
            return;
        }

        NRunMusicController? runMusicController = NRunMusicController.Instance;
        if (runMusicController == null)
        {
            return;
        }

        try
        {
            _isRestoringRunMusic = true;
            if (_baseRunMusicStoppedUsingPublicApi)
            {
                runMusicController.UpdateMusic();
            }
            else
            {
                runMusicController.StopCustomMusic();
            }

            runMusicController.UpdateTrack();
            _baseRunMusicStoppedByController = false;
            _baseRunMusicStoppedUsingPublicApi = false;
        }
        catch (Exception ex)
        {
            Log.Error("[" + LogTag + "] Failed to restore run music: " + ex);
        }
        finally
        {
            _isRestoringRunMusic = false;
        }
    }

    private static float ResolveEffectiveVolumeDb()
    {
        float rawVolume = SaveManager.Instance?.SettingsSave?.VolumeBgm ?? 1f;
        float effectiveScale = Mathf.Clamp(rawVolume, 0f, 1f);
        effectiveScale = Mathf.Pow(effectiveScale, 2f)
            * (float)LibraryOfRuinaSettings.NonCombatRunBgmVolume;
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
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateMusic))]
internal static class NonCombatRunBgmUpdateMusicPatch
{
    private static void Postfix(NRunMusicController __instance)
    {
        NonCombatRunBgmController.OnRunMusicUpdated(__instance);
    }
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateTrack), new Type[] { })]
internal static class NonCombatRunBgmUpdateTrackPatch
{
    private static void Postfix(NRunMusicController __instance)
    {
        NonCombatRunBgmController.OnRunMusicUpdated(__instance);
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.LoseCombat))]
internal static class NonCombatRunBgmCombatLossPatch
{
    private static void Postfix()
    {
        NonCombatRunBgmController.OnCombatLossPending();
    }
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.StopMusic))]
internal static class NonCombatRunBgmStopMusicPatch
{
    private static void Postfix()
    {
        NonCombatRunBgmController.OnRunMusicStoppedExternally();
    }
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.SetBgmVol))]
internal static class NonCombatRunBgmVolumePatch
{
    private static void Postfix()
    {
        NonCombatRunBgmController.RefreshVolumeFromSettings();
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open), typeof(bool))]
internal static class NonCombatRunBgmMapOpenPatch
{
    private static void Postfix(bool isOpenedFromTopBar)
    {
        NonCombatRunBgmController.OnMapOpened(isOpenedFromTopBar);
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close), typeof(bool))]
internal static class NonCombatRunBgmMapClosePatch
{
    private static void Postfix()
    {
        NonCombatRunBgmController.OnMapClosed();
    }
}

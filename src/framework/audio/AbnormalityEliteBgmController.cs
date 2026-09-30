using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.acts;
using LibraryOfRuina.content.specialguests;
using LibraryOfRuina.content.specialguests.Kali;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.framework.audio;

internal static class AbnormalityEliteBgmController
{
    private const string LogTag = "AbnormalityEliteBGM";
    private const string HostNodeName = "LibraryOfRuinaAbnormalityEliteBgmHost";
    private const string PlayerNodeName = "AbnormalityEliteBgmPlayer";
    private const float MaxVolumeDb = 4f;
    private const float MinVolumeDb = -35f;
    private const float VolumeScale = 0.88f;

    private static readonly string[] TrackPaths =
    {
        "res://audio/bgm/abnormality_elite/abnormality_elite_act_1.ogg",
        "res://audio/bgm/abnormality_elite/abnormality_elite_act_2.ogg",
        "res://audio/bgm/abnormality_elite/abnormality_elite_act_3.ogg"
    };


    private static Node? _hostNode;
    private static AudioStreamPlayer? _player;
    private static CombatStateLike? _activeCombatState;
    private static string? _activeTrackPath;
    private static bool _initialized;
    private static bool _isRunning;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        CombatManager.Instance.CombatSetUp += OnCombatSetUp;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
        RunManager.Instance.RoomExited += OnRoomExited;
    }

    public static bool HasBgmForCombat(CombatStateLike? combatState)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            return false;
        }

        return ResolveTrackPath(combatState) != null;
    }

    internal static bool IsRunning => _isRunning;

    internal static bool IsEligibleAct(ActModel? act) =>
        LibraryOfRuinaActModel.IsLibraryAct(act);

    public static void RefreshVolumeFromSettings()
    {
        if (_isRunning && IsAlive(_player))
        {
            _player!.VolumeDb = ResolveEffectiveVolumeDb();
        }
    }

    public static void OnRunCleaningUp()
    {
        if (_isRunning)
        {
            StopSession(restoreRunMusic: false);
        }
    }

    public static void StopRuntimeSession()
    {
        StopSession(restoreRunMusic: true);
    }

    private static void OnCombatSetUp(CombatStateLike combatState)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            StopSession(restoreRunMusic: false);
            return;
        }

        string? trackPath = ResolveTrackPath(combatState);
        if (trackPath == null)
        {
            StopSession(restoreRunMusic: false);
            return;
        }

        StartSession(combatState, trackPath);
    }

    private static void OnCombatEnded(CombatRoom room)
    {
        if (!_isRunning || _activeCombatState == null)
        {
            return;
        }

        if (!ReferenceEquals(room.CombatState, _activeCombatState))
        {
            return;
        }

        StopSession(
            restoreRunMusic: !NonCombatRunBgmController.IsCombatLossPending
                && (RunManager.Instance?.IsInProgress ?? false));
    }

    private static void OnRoomExited()
    {
        if (_isRunning)
        {
            StopSession(restoreRunMusic: false);
        }
    }

    private static void StartSession(CombatStateLike combatState, string trackPath)
    {
        if (_isRunning &&
            ReferenceEquals(_activeCombatState, combatState) &&
            string.Equals(_activeTrackPath, trackPath, StringComparison.Ordinal))
        {
            RefreshVolumeFromSettings();
            return;
        }

        StopSession(restoreRunMusic: false);

        Node? host = EnsureHost();
        if (host == null)
        {
            Log.Error("[" + LogTag + "] Host node is null.");
            return;
        }

        AudioStream? stream = LoadLoopStream(trackPath);
        if (stream == null)
        {
            return;
        }

        AudioStreamPlayer player = EnsurePlayer(host);
        NRunMusicController.Instance?.StopMusic();
        player.Stop();
        player.Stream = stream;
        player.VolumeDb = ResolveEffectiveVolumeDb();
        LibraryBgmPlaybackCoordinator.StopBeforePlayback(player);
        player.Play();

        _activeCombatState = combatState;
        _activeTrackPath = trackPath;
        _isRunning = true;

        string encounterId = combatState.Encounter?.Id.Entry ?? "UNKNOWN_ENCOUNTER";
        Log.Info("[" + LogTag + "] Started act " + ResolveActNumber(combatState) + " elite track for " + encounterId + ": " + trackPath);
    }

    private static void StopSession(bool restoreRunMusic)
    {
        if (!_isRunning && !IsAlive(_player))
        {
            _activeCombatState = null;
            _activeTrackPath = null;
            return;
        }

        if (IsAlive(_player))
        {
            _player!.Stop();
            _player.Stream = null;
            _player.VolumeDb = MinVolumeDb;
        }

        _isRunning = false;
        _activeCombatState = null;
        _activeTrackPath = null;

        if (restoreRunMusic)
        {
            RestoreRunMusicSafely();
        }
    }

    private static string? ResolveTrackPath(CombatStateLike? combatState)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            return null;
        }

        if (combatState?.Encounter == null)
        {
            return null;
        }

        if (!IsEligibleAct(combatState.RunState.Act))
        {
            return null;
        }

        if (combatState.Encounter.RoomType != RoomType.Elite)
        {
            return null;
        }

        if (EncounterBgmController.HasBgmForEncounter(combatState))
        {
            return null;
        }

        if (combatState.Encounter is ISpecialGuestEncounterStage)
        {
            return null;
        }

        if (combatState.Encounter is RedMistElite)
        {
            return null;
        }

        int actNumber = ResolveActNumber(combatState);
        if (actNumber < 1)
        {
            return null;
        }

        int trackIndex = Math.Min(actNumber, TrackPaths.Length) - 1;
        return TrackPaths[trackIndex];
    }

    private static int ResolveActNumber(CombatStateLike combatState)
    {
        return Math.Max(1, combatState.RunState.CurrentActIndex + 1);
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

    private static AudioStreamPlayer EnsurePlayer(Node host)
    {
        if (IsAlive(_player))
        {
            return _player!;
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
        return player;
    }

    private static AudioStream? LoadLoopStream(string trackPath)
    {
        AudioStream? stream = ResourceLoader.Load<AudioStream>(trackPath);
        if (stream == null)
        {
            LorLog.ErrorOnce(
                "AbnormalityEliteBgmController.LoadLoopStream",
                "[" + LogTag + "] Unable to load audio stream: " + trackPath);

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
        float effectiveScale = Mathf.Pow(Mathf.Clamp(rawVolume, 0f, 1f), 2f) * VolumeScale;
        if (effectiveScale <= 0f)
        {
            return MinVolumeDb;
        }

        return MaxVolumeDb + Mathf.LinearToDb(effectiveScale);
    }

    private static void RestoreRunMusicSafely()
    {
        if (MainMenuBgmController.TryResumeOwnedRunMusic())
        {
            return;
        }

        NRunMusicController? runMusicController = NRunMusicController.Instance;
        if (runMusicController == null)
        {
            Log.Warn("[" + LogTag + "] Run music restore skipped: NRunMusicController.Instance is null.");
            return;
        }

        try
        {
            VanillaPrivate.RunMusicControllerCurrentAmbience.Set(runMusicController, null);
            runMusicController.UpdateMusic();
            runMusicController.UpdateTrack();
            Log.Info("[" + LogTag + "] Restored run music after elite BGM session.");
        }
        catch (Exception ex)
        {
            Log.Error("[" + LogTag + "] Failed to restore run music: " + ex);
        }
    }

    private static bool IsAlive(GodotObject? obj)
    {
        return obj != null
            && GodotObject.IsInstanceValid(obj)
            && (obj is not Node node
                || (!node.IsQueuedForDeletion() && node.IsInsideTree()));
    }
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.SetBgmVol))]
internal static class AbnormalityEliteBgmVolumePatch
{
    private static void Postfix()
    {
        AbnormalityEliteBgmController.RefreshVolumeFromSettings();
    }
}

using System;
using Godot;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.framework.audio;

/// <summary>
/// 当前遭遇 BGM 会话的状态机。同一时间最多一个会话，全部状态是静态的：
/// <list type="bullet">
/// <item>开始：第一个登记的怪物按遭遇配置开始（<see cref="Start"/>）；同一场战斗、同一遭遇类型再登记只加入死亡监听。</item>
/// <item>推进：目标曲目只增不减；按死亡（<see cref="TrackProgressionMode.OnMonsterDeath"/>）、回合开始时按回合阈值，
/// 或按遭遇报告的阶段/敌人状态（<see cref="TrackProgressionMode.Dynamic"/>）提高目标，一次淡入淡出只前进一首，结束后接着追目标。</item>
/// <item>结束：战斗结束、新战斗布置、离开房间、关闭运行期副作用、或被外部停止。离开房间与局内清理时记下当前曲目与播放位置，
/// 同一局、同一坐标、同一遭遇再开始时从那里接着放。</item>
/// </list>
/// 事件订阅只在会话开始时建立、结束时全部撤掉。
/// </summary>
internal static class BgmSession
{
    private sealed record SuspendedSessionSnapshot(
        string SessionKey,
        Type EncounterType,
        int TrackIndex,
        float PlaybackPositionSeconds);

    private static readonly HashSet<Creature> RegisteredCreatures = new();

    private static CombatStateLike? _activeCombatState;
    private static Type? _activeEncounterType;
    private static EncounterBgmConfig? _activeConfig;
    private static bool _isRunning;
    private static bool _isTransitioning;
    private static int _currentTrackIndex;
    private static int _targetTrackIndex;
    private static float _activeMaxVolumeDb = BgmCrossfader.MaxVolumeDb;
    private static SuspendedSessionSnapshot? _suspendedSession;

    private static bool _isCombatEndSubscribed;
    private static bool _isCombatSetUpSubscribed;
    private static bool _isTurnStartedSubscribed;
    private static bool _isRoomExitedSubscribed;

    internal static bool IsRunning => _isRunning;

    /// <summary>
    /// 让 <paramref name="creature"/> 的死亡参与按死亡推进；同一生物只订阅一次。会话结束时统一退订。
    /// </summary>
    internal static void TrackCreature(Creature creature)
    {
        if (RegisteredCreatures.Add(creature))
        {
            creature.Died += OnMonsterDied;
        }
    }

    internal static void UntrackCreature(Creature creature)
    {
        if (RegisteredCreatures.Remove(creature))
        {
            creature.Died -= OnMonsterDied;
        }
    }

    internal static void ForceCurrentEncounterTrack(string trackPath, string logTag, float volumeScale)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            return;
        }

        if (!_isRunning || _activeCombatState == null || _activeEncounterType == null)
        {
            return;
        }

        var config = EncounterBgmConfig.RoundBased(
            logTag,
            new[] { trackPath },
            volumeScale);
        Log.Info("[" + logTag + "] Force current encounter track: " + trackPath);
        Start(config, _activeEncounterType, _activeCombatState, forceRestart: true);
    }

    internal static void RefreshCurrentEncounterTrack()
    {
        if (!_isRunning || _activeCombatState == null || _activeConfig == null)
        {
            return;
        }

        int resolvedTarget = ResolveTargetTrackIndex(_activeConfig, _activeCombatState.RoundNumber);
        if (resolvedTarget <= _targetTrackIndex)
        {
            return;
        }

        _targetTrackIndex = resolvedTarget;
        Log.Info(
            "[" + _activeConfig.LogTag + "] Dynamic phase refresh, target track -> "
            + _targetTrackIndex);
        TryAdvanceTrack();
    }

    internal static void OnRunCleaningUp(bool graceful)
    {
        if (!_isRunning || _activeCombatState == null || _activeCombatState.Encounter == null)
        {
            return;
        }

        if (!graceful || !CombatManager.Instance.IsInProgress)
        {
            StopSession(restoreRunMusic: false);
            return;
        }

        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        Log.Info("[" + logTag + "] Run cleanup while combat BGM is active, suspending session.");
        StopSession(restoreRunMusic: false, preserveSuspendedSession: true);
    }

    internal static void StopRuntimeSession()
    {
        if (_isRunning)
        {
            StopSession(restoreRunMusic: true);
        }
    }

    internal static void Start(
        EncounterBgmConfig config,
        Type encounterType,
        CombatStateLike CombatState,
        bool forceRestart = false)
    {
        if (ReverberationEnsembleBgmController.IsActScope)
        {
            return;
        }

        if (!forceRestart &&
            _isRunning &&
            ReferenceEquals(_activeCombatState, CombatState) &&
            _activeEncounterType == encounterType)
        {
            return;
        }

        if (_isRunning || _activeCombatState != null)
        {
            StopSession(restoreRunMusic: false);
        }

        _activeCombatState = CombatState;
        _activeEncounterType = encounterType;
        _activeConfig = config;
        string sessionKey = BuildSessionKey(CombatState, encounterType);
        int initialTrackIndex = 0;
        if (CombatState.Encounter is LibraryOfRuina.content.liberation.Natural.NaturalFloorLiberationEncounter { CurrentPhase: 5 })
        {
            initialTrackIndex = EncounterBgmController.ResolveLiberationPhaseTrackIndex(5);
        }

        float initialPlaybackPositionSeconds = 0f;
        if (TryConsumeSuspendedSession(sessionKey, encounterType, config, out SuspendedSessionSnapshot? suspendedSession)
            && suspendedSession != null)
        {
            initialTrackIndex = suspendedSession.TrackIndex;
            initialPlaybackPositionSeconds = suspendedSession.PlaybackPositionSeconds;
            Log.Info(
                "[" + config.LogTag + "] Resuming suspended track " + initialTrackIndex +
                " at " + initialPlaybackPositionSeconds.ToString("0.00") + "s.");
        }

        _currentTrackIndex = initialTrackIndex;
        _targetTrackIndex = initialTrackIndex;
        _activeMaxVolumeDb = BgmCrossfader.ResolveEffectiveMaxVolumeDb(config);

        BgmCrossfader.EnsureHostAndPlayers();
        if (!BgmCrossfader.HasHostAndPlayers)
        {
            return;
        }

        EnsureCombatEndSubscription();
        EnsureCombatSetUpSubscription();
        EnsureTurnStartedSubscription();
        EnsureRoomExitedSubscription();

        AudioStream? firstTrack = BgmCrossfader.LoadTrack(config, _currentTrackIndex);
        if (firstTrack == null)
        {
            Log.Error("[" + config.LogTag + "] Failed to load initial track " + _currentTrackIndex + ".");
            return;
        }

        NRunMusicController.Instance?.StopMusic();
        BgmCrossfader.PlayFirstTrack(firstTrack, _activeMaxVolumeDb, initialPlaybackPositionSeconds);
        _isRunning = true;
        Log.Info("[" + config.LogTag + "] Started track " + _currentTrackIndex + ". IsPlaying=" + BgmCrossfader.ActivePlayer!.IsPlaying());

        _targetTrackIndex = Math.Max(_currentTrackIndex, ResolveTargetTrackIndex(config, CombatState.RoundNumber));
        TryAdvanceTrack();
    }

    private static void OnMonsterDied(Creature creature)
    {
        if (!_isRunning || _activeCombatState == null || _activeConfig == null)
        {
            return;
        }

        if (!ReferenceEquals(creature.CombatState, _activeCombatState))
        {
            return;
        }

        if (_activeConfig.ProgressionMode != TrackProgressionMode.OnMonsterDeath)
        {
            return;
        }

        if (_targetTrackIndex < _activeConfig.TrackPaths.Length - 1)
        {
            _targetTrackIndex++;
            Log.Info("[" + _activeConfig.LogTag + "] Monster died, target track -> " + _targetTrackIndex);
            TryAdvanceTrack();
        }
    }

    private static void EnsureTurnStartedSubscription()
    {
        if (_isTurnStartedSubscribed)
        {
            return;
        }

        CombatManager? combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            return;
        }

        combatManager.TurnStarted += OnTurnStarted;
        _isTurnStartedSubscribed = true;
    }

    private static void OnTurnStarted(CombatStateLike state)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            StopSession(restoreRunMusic: true);
            return;
        }

        if (!_isRunning || _activeCombatState == null || _activeConfig == null)
        {
            return;
        }

        if (!ReferenceEquals(state, _activeCombatState))
        {
            return;
        }

        RefreshActiveVolumeFromGlobalSettings();

        if (_activeConfig.ProgressionMode is not (TrackProgressionMode.OnRoundThreshold or TrackProgressionMode.Dynamic))
        {
            return;
        }

        int resolvedTarget = ResolveTargetTrackIndex(_activeConfig, state.RoundNumber);
        if (resolvedTarget <= _targetTrackIndex)
        {
            return;
        }

        _targetTrackIndex = resolvedTarget;
        Log.Info(
            "[" + _activeConfig.LogTag + "] Round " + state.RoundNumber +
            ", target track -> " + _targetTrackIndex);
        TryAdvanceTrack();
    }

    private static void TryAdvanceTrack()
    {
        if (!_isRunning || _isTransitioning || _activeConfig == null)
        {
            return;
        }

        if (_targetTrackIndex <= _currentTrackIndex)
        {
            return;
        }

        StartCrossfade(_currentTrackIndex + 1);
    }

    private static void StartCrossfade(int nextTrackIndex)
    {
        if (!BgmCrossfader.HasHostAndPlayers || _activeConfig == null)
        {
            return;
        }

        string logTag = _activeConfig.LogTag;
        AudioStream? nextTrack = BgmCrossfader.LoadTrack(_activeConfig, nextTrackIndex);
        if (nextTrack == null)
        {
            _currentTrackIndex = nextTrackIndex;
            TryAdvanceTrack();
            return;
        }

        if (!BgmCrossfader.TryStartIncoming(nextTrack, out AudioStreamPlayer fromPlayer, out AudioStreamPlayer toPlayer))
        {
            return;
        }

        _isTransitioning = true;

        BgmCrossfader.RunFade(fromPlayer, toPlayer, _activeMaxVolumeDb, () =>
        {
            _currentTrackIndex = nextTrackIndex;
            _isTransitioning = false;
            // 换曲期间更新的音量已记入会话，完成后应用到刚接管播放的新曲目。
            BgmCrossfader.SetActivePlayerVolume(_activeMaxVolumeDb);
            Log.Info("[" + logTag + "] Crossfade finished. Current track=" + _currentTrackIndex);
            TryAdvanceTrack();
        });
    }

    private static void EnsureCombatEndSubscription()
    {
        if (_isCombatEndSubscribed)
        {
            return;
        }

        CombatManager? combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            return;
        }

        combatManager.CombatEnded += OnCombatEnded;
        _isCombatEndSubscribed = true;
    }

    private static void EnsureCombatSetUpSubscription()
    {
        if (_isCombatSetUpSubscribed)
        {
            return;
        }

        CombatManager? combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            return;
        }

        combatManager.CombatSetUp += OnCombatSetUp;
        _isCombatSetUpSubscribed = true;
    }

    private static void EnsureRoomExitedSubscription()
    {
        if (_isRoomExitedSubscribed)
        {
            return;
        }

        RunManager? runManager = RunManager.Instance;
        if (runManager == null)
        {
            return;
        }

        runManager.RoomExited += OnRoomExited;
        _isRoomExitedSubscribed = true;
    }

    private static void OnCombatEnded(CombatRoom room)
    {
        if (_activeEncounterType == null || room.Encounter == null || room.Encounter.GetType() != _activeEncounterType)
        {
            return;
        }

        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        Log.Info("[" + logTag + "] Combat ended.");
        bool shouldRestoreRunMusic = !NonCombatRunBgmController.IsCombatLossPending
            && (RunManager.Instance?.IsInProgress ?? false);
        StopSession(shouldRestoreRunMusic);
    }

    private static void OnCombatSetUp(CombatStateLike state)
    {
        if (!_isRunning || _activeCombatState == null)
        {
            return;
        }

        if (ReferenceEquals(state, _activeCombatState))
        {
            return;
        }

        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        string nextEncounter = state.Encounter?.Id.Entry ?? state.Encounter?.GetType().Name ?? "UNKNOWN_ENCOUNTER";
        Log.Info("[" + logTag + "] New combat setup detected (" + nextEncounter + "), stopping previous BGM session.");
        StopSession(restoreRunMusic: false);
    }

    private static void OnRoomExited()
    {
        if (!_isRunning)
        {
            return;
        }

        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        Log.Info("[" + logTag + "] Room exited while BGM session active, forcing stop.");
        StopSession(restoreRunMusic: false, preserveSuspendedSession: true);
    }

    private static void StopSession(bool restoreRunMusic, bool preserveSuspendedSession = false)
    {
        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        SuspendedSessionSnapshot? suspendedSession = preserveSuspendedSession ? CaptureSuspendedSession() : null;

        BgmCrossfader.KillFadeTween();
        _isTransitioning = false;
        _isRunning = false;
        _currentTrackIndex = 0;
        _targetTrackIndex = 0;
        _activeMaxVolumeDb = BgmCrossfader.MaxVolumeDb;
        _activeCombatState = null;
        _activeEncounterType = null;
        _activeConfig = null;
        if (preserveSuspendedSession)
        {
            _suspendedSession = suspendedSession;
            if (_suspendedSession != null)
            {
                Log.Info(
                    "[" + logTag + "] Suspended track " + _suspendedSession.TrackIndex +
                    " at " + _suspendedSession.PlaybackPositionSeconds.ToString("0.00") + "s.");
            }
        }
        else
        {
            _suspendedSession = null;
        }

        Log.Info("[" + logTag + "] StopSession restoreRunMusic=" + restoreRunMusic);

        foreach (Creature creature in RegisteredCreatures)
        {
            creature.Died -= OnMonsterDied;
        }

        RegisteredCreatures.Clear();

        BgmCrossfader.StopAndDisposePlayers();

        if (_isCombatEndSubscribed)
        {
            CombatManager? combatManager = CombatManager.Instance;
            if (combatManager != null)
            {
                combatManager.CombatEnded -= OnCombatEnded;
            }

            _isCombatEndSubscribed = false;
        }

        if (_isCombatSetUpSubscribed)
        {
            CombatManager? combatManager = CombatManager.Instance;
            if (combatManager != null)
            {
                combatManager.CombatSetUp -= OnCombatSetUp;
            }

            _isCombatSetUpSubscribed = false;
        }

        if (_isTurnStartedSubscribed)
        {
            CombatManager? combatManager = CombatManager.Instance;
            if (combatManager != null)
            {
                combatManager.TurnStarted -= OnTurnStarted;
            }

            _isTurnStartedSubscribed = false;
        }

        if (_isRoomExitedSubscribed)
        {
            RunManager? runManager = RunManager.Instance;
            if (runManager != null)
            {
                runManager.RoomExited -= OnRoomExited;
            }

            _isRoomExitedSubscribed = false;
        }

        if (restoreRunMusic)
        {
            RestoreRunMusicSafely(logTag);
        }
    }

    private static SuspendedSessionSnapshot? CaptureSuspendedSession()
    {
        if (_activeCombatState == null || _activeEncounterType == null || _activeConfig == null)
        {
            return null;
        }

        string sessionKey = BuildSessionKey(_activeCombatState, _activeEncounterType);
        int trackIndex = _currentTrackIndex;
        float playbackPositionSeconds = 0f;
        AudioStreamPlayer? player = BgmCrossfader.ActivePlayer;
        AudioStreamPlayer? inactivePlayer = BgmCrossfader.InactivePlayer;
        if (BgmCrossfader.IsAlive(player) && player!.Stream != null)
        {
            try
            {
                if (_isTransitioning && BgmCrossfader.IsAlive(inactivePlayer) && inactivePlayer!.Stream != null)
                {
                    player = inactivePlayer;
                    trackIndex = Math.Min(_currentTrackIndex + 1, _activeConfig.TrackPaths.Length - 1);
                }

                if (BgmCrossfader.IsAlive(player))
                {
                    playbackPositionSeconds = MathF.Max(0f, player!.GetPlaybackPosition());
                }
            }
            catch (ObjectDisposedException)
            {
                playbackPositionSeconds = 0f;
            }
        }

        return new SuspendedSessionSnapshot(sessionKey, _activeEncounterType, trackIndex, playbackPositionSeconds);
    }

    private static bool TryConsumeSuspendedSession(
        string sessionKey,
        Type encounterType,
        EncounterBgmConfig config,
        out SuspendedSessionSnapshot? suspendedSession)
    {
        suspendedSession = null;
        SuspendedSessionSnapshot? candidate = _suspendedSession;
        _suspendedSession = null;
        if (candidate == null)
        {
            return false;
        }

        if (candidate.SessionKey != sessionKey || candidate.EncounterType != encounterType)
        {
            return false;
        }

        if (candidate.TrackIndex < 0 || candidate.TrackIndex >= config.TrackPaths.Length)
        {
            return false;
        }

        suspendedSession = candidate;
        return true;
    }

    private static string BuildSessionKey(CombatStateLike CombatState, Type encounterType)
    {
        string seed = CombatState.RunState.Rng.StringSeed;
        string location = CombatState.RunState.CurrentActIndex + ":" + (CombatState.RunState.CurrentMapCoord?.ToString() ?? "null");
        string encounterId = CombatState.Encounter?.Id.Entry ?? encounterType.FullName ?? encounterType.Name;
        return seed + "|" + location + "|" + encounterId;
    }

    private static void RestoreRunMusicSafely(string logTag)
    {
        if (MainMenuBgmController.TryResumeOwnedRunMusic())
        {
            return;
        }

        NRunMusicController? runMusicController = NRunMusicController.Instance;
        if (runMusicController == null)
        {
            Log.Warn("[" + logTag + "] Run music restore skipped: NRunMusicController.Instance is null.");
            return;
        }

        bool ambienceCacheReset = ResetRunMusicAmbienceCache(runMusicController, logTag);
        try
        {
            runMusicController.UpdateMusic();
            runMusicController.UpdateTrack();
            Log.Info("[" + logTag + "] Restored run music after encounter BGM session. ambienceCacheReset=" + ambienceCacheReset);
        }
        catch (Exception ex)
        {
            Log.Error("[" + logTag + "] Failed to restore run music: " + ex);
        }
    }

    private static bool ResetRunMusicAmbienceCache(NRunMusicController runMusicController, string logTag)
    {
        if (!VanillaPrivate.RunMusicControllerCurrentAmbience.IsAvailable)
        {
            Log.Error("[" + logTag + "] Failed to reset run music ambience cache: NRunMusicController._currentAmbience field was not found.");
            return false;
        }

        try
        {
            VanillaPrivate.RunMusicControllerCurrentAmbience.Set(runMusicController, null);
            Log.Info("[" + logTag + "] Reset run music ambience cache before restore.");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("[" + logTag + "] Failed to reset run music ambience cache: " + ex);
            return false;
        }
    }

    private static void RefreshActiveVolumeFromGlobalSettings()
    {
        if (_activeConfig == null)
        {
            return;
        }

        float resolvedMaxVolumeDb = BgmCrossfader.ResolveEffectiveMaxVolumeDb(_activeConfig);
        if (Mathf.IsEqualApprox(resolvedMaxVolumeDb, _activeMaxVolumeDb))
        {
            return;
        }

        _activeMaxVolumeDb = resolvedMaxVolumeDb;
        if (_isTransitioning)
        {
            return;
        }

        BgmCrossfader.SetActivePlayerVolume(_activeMaxVolumeDb);
    }

    private static int ResolveTargetTrackIndex(EncounterBgmConfig config, int roundNumber)
    {
        if (config.ProgressionMode == TrackProgressionMode.Dynamic
            && config.DynamicTrackResolver != null
            && _activeCombatState != null)
        {
            return Math.Clamp(
                config.DynamicTrackResolver(_activeCombatState),
                0,
                config.TrackPaths.Length - 1);
        }

        if (config.ProgressionMode != TrackProgressionMode.OnRoundThreshold || config.RoundThresholds.Length == 0)
        {
            return 0;
        }

        int targetIndex = 0;
        for (int i = 0; i < config.RoundThresholds.Length; i++)
        {
            if (roundNumber < config.RoundThresholds[i])
            {
                break;
            }

            targetIndex = Math.Min(i + 1, config.TrackPaths.Length - 1);
        }

        return targetIndex;
    }
}

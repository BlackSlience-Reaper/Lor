using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.events.WarpTrain;

public partial class NWarpTrainEventPresentationController : Node
{
    private const string ControllerNodeName = "WarpTrainEventPresentationController";
    private const string LogTag = "WarpTrainEventBGM";

    private readonly Dictionary<string, Texture2D?> _textureCache = new(StringComparer.Ordinal);

    private AudioStreamPlayer? _loopPlayer;
    private AudioStreamPlayer? _voicePlayer;

    private WarpTrainPage? _lastRenderedPage;
    private string? _activeLoopPath;
    private CancellationTokenSource? _voiceSequenceCts;
    private bool _runMusicSuspended;
    private bool _suppressRunMusicRestoreOnExitTree;


    public static NWarpTrainEventPresentationController? GetFromCurrentRoom(bool createIfMissing)
    {
        NEventRoom? room = NEventRoom.Instance;
        if (room == null)
        {
            return null;
        }

        NWarpTrainEventPresentationController? controller =
            room.GetNodeOrNull<NWarpTrainEventPresentationController>(ControllerNodeName);

        if (controller != null || !createIfMissing)
        {
            return controller;
        }

        controller = new NWarpTrainEventPresentationController
        {
            Name = ControllerNodeName
        };

        room.AddChild(controller);
        return controller;
    }

    public override void _ExitTree()
    {
        StopAllAudio(restoreRunMusic: !_suppressRunMusicRestoreOnExitTree);
        base._ExitTree();
    }

    public void ApplyPresentationForPage(WarpTrainPage page, bool playVoiceWhenPageChanges)
    {
        SetPortrait(page);
        UpdateLoop(page);

        bool pageChanged = _lastRenderedPage != page;
        if (playVoiceWhenPageChanges && pageChanged)
        {
            PlayPageEnterVoice(page);
        }

        _lastRenderedPage = page;
    }

    public void StopAllAudio(bool restoreRunMusic = true)
    {
        StopVoiceAndQueuedSequence();
        StopLoop();

        if (restoreRunMusic)
        {
            _suppressRunMusicRestoreOnExitTree = false;
            RestoreRunMusicIfNeeded();
        }
        else
        {
            _suppressRunMusicRestoreOnExitTree = true;
        }
    }

    private void SetPortrait(WarpTrainPage page)
    {
        Texture2D? portrait = LoadTexture(ResolvePortraitPath(page));
        if (portrait != null)
        {
            NEventRoom.Instance?.SetPortrait(portrait);
        }
    }

    private void UpdateLoop(WarpTrainPage page)
    {
        string? loopPath = ResolveLoopPath(page);
        if (loopPath == null)
        {
            StopLoop();
            return;
        }

        EnsureRunMusicSuspended();
        PlayLoop(loopPath);
    }

    private Texture2D? LoadTexture(string path)
    {
        if (_textureCache.TryGetValue(path, out Texture2D? cached))
        {
            return cached;
        }

        Texture2D? loaded = null;
        try
        {
            loaded = PreloadManager.Cache.GetTexture2D(path);
        }
        catch
        {
            loaded = null;
        }

        _textureCache[path] = loaded;
        return loaded;
    }

    private void EnsureAudioPlayers()
    {
        if (_loopPlayer != null && _voicePlayer != null)
        {
            return;
        }

        _loopPlayer = new AudioStreamPlayer
        {
            Name = "WarpTrainLoopPlayer",
            Bus = "Master"
        };

        _voicePlayer = new AudioStreamPlayer
        {
            Name = "WarpTrainVoicePlayer",
            Bus = "Master"
        };

        AddChild(_loopPlayer);
        AddChild(_voicePlayer);
    }

    private void PlayLoop(string path)
    {
        EnsureAudioPlayers();
        if (_loopPlayer == null)
        {
            return;
        }

        if (_activeLoopPath == path && _loopPlayer.Playing)
        {
            return;
        }

        AudioStream? stream = LoadAudio(path, loop: true);
        if (stream == null)
        {
            StopLoop();
            return;
        }

        _activeLoopPath = path;
        _loopPlayer.Stop();
        _loopPlayer.Stream = stream;
        LibrarySfxMixer.ConfigurePlayer(
            _loopPlayer,
            path,
            category: LibraryAudioCategory.Music);
        LibraryBgmPlaybackCoordinator.StopBeforePlayback(_loopPlayer);
        _loopPlayer.Play();
    }

    private void StopLoop()
    {
        _activeLoopPath = null;
        if (_loopPlayer == null)
        {
            return;
        }

        _loopPlayer.Stop();
        _loopPlayer.Stream = null;
    }

    private void PlayVoice(string path)
    {
        EnsureAudioPlayers();
        if (_voicePlayer == null)
        {
            return;
        }

        AudioStream? stream = LoadAudio(path, loop: false);
        if (stream == null)
        {
            return;
        }

        _voicePlayer.Stop();
        _voicePlayer.Stream = stream;
        LibrarySfxMixer.ConfigurePlayer(
            _voicePlayer,
            path,
            category: LibraryAudioCategory.Dialogue);
        _voicePlayer.Play();
    }

    private AudioStream? LoadAudio(string path, bool loop)
    {
        AudioStream? stream;
        try
        {
            stream = PreloadManager.Cache.GetAsset<AudioStream>(path);
        }
        catch
        {
            return null;
        }

        if (stream is AudioStreamOggVorbis ogg)
        {
            AudioStreamOggVorbis copy = (AudioStreamOggVorbis)ogg.Duplicate();
            copy.Loop = loop;
            return copy;
        }

        return stream;
    }

    private void PlayPageEnterVoice(WarpTrainPage page)
    {
        StopVoiceAndQueuedSequence();

        switch (page)
        {
            case WarpTrainPage.Page3:
                PlayVoice(WarpTrainEvent.MaryVoicePath1);
                break;
            case WarpTrainPage.Page4:
                PlayVoice(WarpTrainEvent.TommyVoicePath1);
                break;
            case WarpTrainPage.Page5:
                PlayVoice(WarpTrainEvent.MaryVoicePath2);
                break;
            case WarpTrainPage.Page6:
                PlayVoice(WarpTrainEvent.TommyVoicePath2);
                break;
            case WarpTrainPage.Page9:
                _voiceSequenceCts = new CancellationTokenSource();
                TaskHelper.RunSafely(PlayPageNineSequence(_voiceSequenceCts.Token));
                break;
        }
    }

    private async Task PlayPageNineSequence(CancellationToken cancellationToken)
    {
        PlayVoice(WarpTrainEvent.TownsfolkCheerPath1);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            PlayVoice(WarpTrainEvent.TownsfolkCheerPath2);
        }
    }

    private void StopVoiceAndQueuedSequence()
    {
        if (_voiceSequenceCts != null)
        {
            _voiceSequenceCts.Cancel();
            _voiceSequenceCts.Dispose();
            _voiceSequenceCts = null;
        }

        if (_voicePlayer == null)
        {
            return;
        }

        _voicePlayer.Stop();
        _voicePlayer.Stream = null;
    }

    private static string ResolvePortraitPath(WarpTrainPage page)
    {
        return page switch
        {
            WarpTrainPage.Page1 or WarpTrainPage.Page2 or WarpTrainPage.Page7 or WarpTrainPage.Page11 or WarpTrainPage.Page12
                => WarpTrainEvent.WarpBackgroundPath,
            WarpTrainPage.Page3 or WarpTrainPage.Page4 or WarpTrainPage.Page5 or WarpTrainPage.Page6 or WarpTrainPage.Page9
                => WarpTrainEvent.BlackBackgroundPath,
            WarpTrainPage.Page8 or WarpTrainPage.Page10
                => WarpTrainEvent.LoveTownBackgroundPath,
            _ => WarpTrainEvent.WarpBackgroundPath
        };
    }

    private static string? ResolveLoopPath(WarpTrainPage page)
    {
        return page switch
        {
            WarpTrainPage.Page1 or WarpTrainPage.Page2 or WarpTrainPage.Page7 => WarpTrainEvent.WarpTrainBgmPath,
            WarpTrainPage.Page8 or WarpTrainPage.Page9 or WarpTrainPage.Page10 => WarpTrainEvent.LoveTownEventBgmPath,
            _ => null
        };
    }

    private void EnsureRunMusicSuspended()
    {
        if (_runMusicSuspended)
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
            runMusicController.StopMusic();
            _runMusicSuspended = true;
            _suppressRunMusicRestoreOnExitTree = false;
            Log.Info("[" + LogTag + "] Suspended run music for event loop.");
        }
        catch (Exception ex)
        {
            Log.Warn("[" + LogTag + "] Failed to suspend run music: " + ex.Message);
        }
    }

    private void RestoreRunMusicIfNeeded()
    {
        if (!_runMusicSuspended)
        {
            return;
        }

        _runMusicSuspended = false;

        if (!(RunManager.Instance?.IsInProgress ?? false))
        {
            return;
        }

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

        bool ambienceCacheReset = ResetRunMusicAmbienceCache(runMusicController);
        try
        {
            runMusicController.UpdateMusic();
            runMusicController.UpdateTrack();
            Log.Info("[" + LogTag + "] Restored run music after event. ambienceCacheReset=" + ambienceCacheReset);
        }
        catch (Exception ex)
        {
            Log.Error("[" + LogTag + "] Failed to restore run music: " + ex);
        }
    }

    private static bool ResetRunMusicAmbienceCache(NRunMusicController runMusicController)
    {
        if (!VanillaPrivate.RunMusicControllerCurrentAmbience.IsAvailable)
        {
            Log.Error("[" + LogTag + "] Failed to reset run music ambience cache: field was not found.");
            return false;
        }

        try
        {
            VanillaPrivate.RunMusicControllerCurrentAmbience.Set(runMusicController, null);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("[" + LogTag + "] Failed to reset run music ambience cache: " + ex);
            return false;
        }
    }
}

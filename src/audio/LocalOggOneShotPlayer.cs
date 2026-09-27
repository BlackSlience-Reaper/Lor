using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;

namespace LibraryOfRuina.audio;

internal static class LocalOggOneShotPlayer
{
    private const string HostNodeName = "LibraryOfRuinaLocalOggHost";
    private const double UnknownAudioFallbackSeconds = 30.0;
    private const double PlaybackFallbackPaddingSeconds = 1.0;

    private static Node? _hostNode;
    private static int _nextPlayerId;
    private static readonly Dictionary<string, AudioStreamPlayer> ExclusivePlayers = new();

    public static void Play(
        string audioPath,
        float volumeDb = 0f,
        LibraryAudioCategory category = LibraryAudioCategory.SoundEffect)
    {
        _ = TaskHelper.RunSafely(PlayAsync(audioPath, volumeDb, category));
    }

    public static void StopExclusive(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
        {
            return;
        }

        if (!ExclusivePlayers.TryGetValue(slot, out AudioStreamPlayer? player) || !IsAlive(player))
        {
            ExclusivePlayers.Remove(slot);
            return;
        }

        try
        {
            player.Stop();
        }
        catch
        {
            
        }
    }

    public static void PlayExclusive(
        string slot,
        string audioPath,
        float volumeDb = 0f,
        LibraryAudioCategory category = LibraryAudioCategory.SoundEffect)
    {
        if (string.IsNullOrWhiteSpace(slot) || string.IsNullOrWhiteSpace(audioPath))
        {
            return;
        }

        AudioStream? stream = ResourceLoader.Load<AudioStream>(audioPath);
        if (stream == null)
        {
            Log.Error("[LocalOggOneShotPlayer] Unable to load audio stream: " + audioPath);
            return;
        }

        Node? host = EnsureHost();
        if (host == null)
        {
            Log.Error("[LocalOggOneShotPlayer] Unable to resolve host node.");
            return;
        }

        AudioStreamPlayer player = EnsureExclusivePlayer(slot, host);
        try
        {
            player.Stop();
            player.Stream = stream;
            LibrarySfxMixer.ConfigurePlayer(player, audioPath, volumeDb, category);
            player.Play();
        }
        catch
        {
            
        }
    }

    public static async Task PlayAsync(
        string audioPath,
        float volumeDb = 0f,
        LibraryAudioCategory category = LibraryAudioCategory.SoundEffect)
    {
        if (string.IsNullOrWhiteSpace(audioPath))
        {
            return;
        }

        AudioStream? stream = ResourceLoader.Load<AudioStream>(audioPath);
        if (stream == null)
        {
            Log.Error("[LocalOggOneShotPlayer] Unable to load audio stream: " + audioPath);
            return;
        }

        Node? host = EnsureHost();
        if (host == null)
        {
            Log.Error("[LocalOggOneShotPlayer] Unable to resolve host node.");
            return;
        }

        var completion = new TaskCompletionSource();
        AudioStreamPlayer player = CreatePlayer(stream, audioPath, volumeDb, category);
        SceneTree? tree = host.GetTree();

        void CompleteAndFree()
        {
            if (IsAlive(player))
            {
                try
                {
                    player.Stop();
                    player.QueueFree();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            completion.TrySetResult();
        }

        player.Finished += CompleteAndFree;
        player.TreeExiting += () => completion.TrySetResult();
        try
        {
            host.AddChildSafely(player);
            player.Play();
        }
        catch (Exception exception)
        {
            Log.Error(
                "[LocalOggOneShotPlayer] Failed to start audio stream "
                + audioPath
                + ": "
                + exception);
            CompleteAndFree();
        }

        if (tree != null && !completion.Task.IsCompleted)
        {
            double length = stream.GetLength();
            if (length <= 0)
            {
                length = UnknownAudioFallbackSeconds;
            }

            _ = TaskHelper.RunSafely(CompleteAfterTimeout(
                tree,
                player,
                completion,
                audioPath,
                length + PlaybackFallbackPaddingSeconds));
        }

        await completion.Task;
    }

    private static async Task CompleteAfterTimeout(
        SceneTree tree,
        AudioStreamPlayer player,
        TaskCompletionSource completion,
        string audioPath,
        double seconds)
    {
        // 音频按实际时间播放，超时计时不能随战斗加速提前结束。
        SceneTreeTimer timer = tree.CreateTimer(seconds, ignoreTimeScale: true);
        await tree.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        if (completion.Task.IsCompleted)
        {
            return;
        }

        Log.Warn(
            "[LocalOggOneShotPlayer] Playback timed out; completing wait for "
            + audioPath
            + ".");
        if (IsAlive(player))
        {
            player.Stop();
            player.QueueFree();
        }

        completion.TrySetResult();
    }

    private static AudioStreamPlayer EnsureExclusivePlayer(string slot, Node host)
    {
        if (ExclusivePlayers.TryGetValue(slot, out AudioStreamPlayer? existing) && IsAlive(existing))
        {
            if (existing.GetParent() == null)
            {
                host.AddChildSafely(existing);
            }

            return existing;
        }

        var player = new AudioStreamPlayer
        {
            Name = "Exclusive_" + slot,
            Bus = "Master",
            VolumeDb = 0f,
        };
        host.AddChildSafely(player);
        ExclusivePlayers[slot] = player;
        return player;
    }

    private static AudioStreamPlayer CreatePlayer(
        AudioStream stream,
        string audioPath,
        float volumeDb,
        LibraryAudioCategory category)
    {
        var player = new AudioStreamPlayer
        {
            Name = "OneShot_" + _nextPlayerId++,
            Stream = stream
        };

        LibrarySfxMixer.ConfigurePlayer(player, audioPath, volumeDb, category);
        return player;
    }

    private static Node? EnsureHost()
    {
        if (IsAlive(_hostNode))
        {
            CleanupExclusivePlayersIfNeeded();
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
        CleanupExclusivePlayersIfNeeded();
        return _hostNode;
    }

    private static void CleanupExclusivePlayersIfNeeded()
    {
        if (!IsAlive(_hostNode))
        {
            ExclusivePlayers.Clear();
            return;
        }

        
        foreach ((string slot, AudioStreamPlayer player) in new List<KeyValuePair<string, AudioStreamPlayer>>(ExclusivePlayers))
        {
            if (!IsAlive(player))
            {
                ExclusivePlayers.Remove(slot);
            }
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

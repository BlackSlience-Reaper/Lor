using System;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;

namespace LibraryOfRuina.audio;

internal static class LocalOggLoopPlayer
{
    private const string HostNodeName = "LibraryOfRuinaLocalOggHost";

    private static Node? _hostNode;
    private static int _nextPlayerId;

    public static LoopHandle? StartLoop(
        string audioPath,
        float volumeDb = 0f,
        LibraryAudioCategory category = LibraryAudioCategory.SoundEffect)
    {
        if (string.IsNullOrWhiteSpace(audioPath))
        {
            return null;
        }

        AudioStream? stream = ResourceLoader.Load<AudioStream>(audioPath);
        if (stream == null)
        {
            Log.Error("[LocalOggLoopPlayer] Unable to load audio stream: " + audioPath);
            return null;
        }

        Node? host = EnsureHost();
        if (host == null)
        {
            Log.Error("[LocalOggLoopPlayer] Unable to resolve host node.");
            return null;
        }

        AudioStream loopStream = stream;
        if (stream is AudioStreamOggVorbis ogg)
        {
            AudioStreamOggVorbis duplicated = (AudioStreamOggVorbis)ogg.Duplicate();
            duplicated.Loop = true;
            loopStream = duplicated;
        }

        var player = new AudioStreamPlayer
        {
            Name = "Loop_" + _nextPlayerId++,
            Stream = loopStream
        };

        LibrarySfxMixer.ConfigurePlayer(player, audioPath, volumeDb, category);

        host.AddChildSafely(player);
        player.Play();

        return new LoopHandle(player);
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

    private static bool IsAlive(GodotObject? obj)
    {
        return obj != null
            && GodotObject.IsInstanceValid(obj)
            && (obj is not Node node
                || (!node.IsQueuedForDeletion() && node.IsInsideTree()));
    }

    internal sealed class LoopHandle : IDisposable
    {
        private AudioStreamPlayer? _player;

        public LoopHandle(AudioStreamPlayer player)
        {
            _player = player;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Stop()
        {
            AudioStreamPlayer? player = _player;
            _player = null;
            if (player == null || !IsAlive(player))
            {
                return;
            }

            try
            {
                player.Stop();
                player.QueueFree();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}


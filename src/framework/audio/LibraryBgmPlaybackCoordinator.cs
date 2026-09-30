using System;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;

namespace LibraryOfRuina.framework.audio;

internal static class LibraryBgmPlaybackCoordinator
{
    private static readonly StringName FmodServerName = new("FmodServer");

    internal static bool IsStoppingCompetingMusic { get; private set; }

    internal static void StopBeforePlayback(params AudioStreamPlayer[] preservedPlayers)
    {
        if (IsStoppingCompetingMusic || preservedPlayers.Length == 0)
        {
            return;
        }

        IsStoppingCompetingMusic = true;
        try
        {
            // 清理全局音乐句柄；停止回调不能在此次接管期间重新启动主界面音乐。
            NAudioManager.Instance?.StopMusic();
            StopFmodMusicBus();
            StopMusicPlayers(preservedPlayers[0].GetTree().Root, preservedPlayers);
        }
        finally
        {
            IsStoppingCompetingMusic = false;
        }
    }

    private static void StopFmodMusicBus()
    {
        if (!Engine.HasSingleton(FmodServerName))
        {
            return;
        }

        GodotObject server = Engine.GetSingleton(FmodServerName);
        GodotObject? bus = server.Call("get_bus", "bus:/master/music").AsGodotObject();
        if (bus == null || !bus.HasMethod("stop_all_events"))
        {
            return;
        }

        // 即刻停止音乐总线内的原版和模组事件，保留音效、环境音与对白总线。
        long immediateStop = ClassDB.ClassGetIntegerConstant(
            FmodServerName,
            "FMOD_STUDIO_STOP_IMMEDIATE");
        bus.Call("stop_all_events", immediateStop);
    }

    private static void StopMusicPlayers(Node node, AudioStreamPlayer[] preservedPlayers)
    {
        if (node.IsQueuedForDeletion())
        {
            return;
        }

        switch (node)
        {
            case AudioStreamPlayer player when Array.IndexOf(preservedPlayers, player) < 0:
                if (player.Playing && IsMusicPlayer(player, player.Bus))
                {
                    player.Stop();
                    Log.Info("[LibraryBGM] Stopped competing music: " + player.GetPath());
                }
                break;

            case AudioStreamPlayer2D player:
                if (player.Playing && IsMusicPlayer(player, player.Bus))
                {
                    player.Stop();
                    Log.Info("[LibraryBGM] Stopped competing music: " + player.GetPath());
                }
                break;

            case AudioStreamPlayer3D player:
                if (player.Playing && IsMusicPlayer(player, player.Bus))
                {
                    player.Stop();
                    Log.Info("[LibraryBGM] Stopped competing music: " + player.GetPath());
                }
                break;
        }

        foreach (Node child in node.GetChildren())
        {
            StopMusicPlayers(child, preservedPlayers);
        }
    }

    private static bool IsMusicPlayer(Node player, StringName bus)
    {
        // 全树扫描只读取节点与总线信息；读取 Stream 会创建具体音频类型的托管包装，
        // 在游戏原生引擎与 GodotSharp 绑定不匹配时可能触发 WAV 类型初始化失败并崩溃。
        if (HasMusicName(player.Name.ToString()))
        {
            return true;
        }

        // 沿发送目标识别音乐子总线，全程不访问音频资源。
        int busIndex = AudioServer.GetBusIndex(bus);
        for (int remaining = AudioServer.BusCount; busIndex > 0 && remaining > 0; remaining--)
        {
            if (HasMusicName(AudioServer.GetBusName(busIndex).ToString()))
            {
                return true;
            }

            busIndex = AudioServer.GetBusIndex(AudioServer.GetBusSend(busIndex));
        }

        return false;
    }

    private static bool HasMusicName(string name) =>
        name.Contains("Bgm", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Music", StringComparison.OrdinalIgnoreCase);
}

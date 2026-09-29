using System;
using Godot;
using LibraryOfRuina.core.settings;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.audio;

internal enum LibraryAudioCategory
{
    SoundEffect,
    Music,
    Dialogue
}

internal static class LibrarySfxMixer
{
    internal const string BusName = "LibraryOfRuinaSfx";

    private const string SfxResourceRoot = "res://audio/sfx/";

    private static readonly object Sync = new();
    private static bool _busCreationLogged;

    internal static void Initialize()
    {
        RefreshMasterVolume();
    }

    internal static void ConfigurePlayer(
        AudioStreamPlayer player,
        string audioPath,
        float sourceVolumeDb = 0f,
        LibraryAudioCategory category = LibraryAudioCategory.SoundEffect)
    {
        player.VolumeDb = sourceVolumeDb;
        if (category != LibraryAudioCategory.SoundEffect
            || !IsModSfxPath(audioPath))
        {
            player.Bus = "Master";
            return;
        }

        int busIndex = EnsureBus();
        player.Bus = busIndex >= 0 ? BusName : "Master";
    }

    internal static void RefreshMasterVolume()
    {
        int busIndex = EnsureBus();
        if (busIndex < 0)
        {
            return;
        }

        float volumeScale = (float)Math.Clamp(LibraryOfRuinaSettings.ModSfxVolume, 0d, 1d);
        bool muted = !LibraryOfRuinaSettings.ModSfxEnabled || volumeScale <= 0f;
        AudioServer.SetBusMute(busIndex, muted);
        if (!muted)
        {
            AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(volumeScale));
        }
    }

    internal static bool IsModSfxPath(string audioPath) =>
        !string.IsNullOrWhiteSpace(audioPath)
        && audioPath.StartsWith(SfxResourceRoot, StringComparison.OrdinalIgnoreCase);

    private static int EnsureBus()
    {
        lock (Sync)
        {
            int existingIndex = AudioServer.GetBusIndex(BusName);
            if (existingIndex >= 0)
            {
                return existingIndex;
            }

            try
            {
                AudioServer.AddBus();
                int busIndex = AudioServer.GetBusCount() - 1;
                AudioServer.SetBusName(busIndex, BusName);
                AudioServer.SetBusSend(busIndex, "Master");

                if (!_busCreationLogged)
                {
                    _busCreationLogged = true;
                    Log.Info("[LibraryOfRuina.Audio] Created the LibraryOfRuinaSfx mixer bus.");
                }

                return busIndex;
            }
            catch (Exception exception)
            {
                Log.Error("[LibraryOfRuina.Audio] Failed to create the SFX mixer bus: " + exception);
                return -1;
            }
        }
    }
}

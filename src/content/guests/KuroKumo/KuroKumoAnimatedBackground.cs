using System;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.content.guests.KuroKumo;

public partial class KuroKumoAnimatedBackground : TextureRect
{
    [Export(PropertyHint.File, "*.json")]
    public string ManifestPath = "res://images/backgrounds/kuro_kumo_normal/frames/manifest.json";

    private readonly List<Texture2D> _frames = new();
    private readonly List<double> _durationsSeconds = new();
    private Texture2D? _fallbackTexture;
    private int _currentFrameIndex;
    private double _frameElapsedSeconds;

    public override void _Ready()
    {
        _fallbackTexture = Texture;
        SetProcess(false);

        if (!TryLoadFramesFromManifest())
        {
            ApplyStaticFallback("Failed to load animated manifest or frame sequence.");
            return;
        }

        _currentFrameIndex = 0;
        _frameElapsedSeconds = 0d;
        Texture = _frames[0];

        
        SetProcess(_frames.Count > 1);

        double totalDurationMs = 0d;
        foreach (double durationSeconds in _durationsSeconds)
        {
            totalDurationMs += durationSeconds * 1000d;
        }

        Log.Info(
            $"[KuroKumoAnimatedBackground] Loaded {_frames.Count} frames, total {totalDurationMs:F0} ms, manifest={ManifestPath}");
    }

    public override void _Process(double delta)
    {
        if (_frames.Count <= 1)
        {
            return;
        }

        _frameElapsedSeconds += delta;
        while (_frameElapsedSeconds >= _durationsSeconds[_currentFrameIndex])
        {
            _frameElapsedSeconds -= _durationsSeconds[_currentFrameIndex];
            _currentFrameIndex = (_currentFrameIndex + 1) % _frames.Count;
            Texture = _frames[_currentFrameIndex];
        }
    }

    private bool TryLoadFramesFromManifest()
    {
        _frames.Clear();
        _durationsSeconds.Clear();

        if (string.IsNullOrWhiteSpace(ManifestPath))
        {
            Log.Warn("[KuroKumoAnimatedBackground] ManifestPath is empty.");
            return false;
        }

        string normalizedManifestPath = NormalizeResPath(ManifestPath);
        if (!FileAccess.FileExists(normalizedManifestPath))
        {
            Log.Warn("[KuroKumoAnimatedBackground] Manifest not found: " + normalizedManifestPath);
            return false;
        }

        string manifestJson;
        using (FileAccess manifestFile = FileAccess.Open(normalizedManifestPath, FileAccess.ModeFlags.Read))
        {
            if (manifestFile == null)
            {
                Log.Warn("[KuroKumoAnimatedBackground] Unable to open manifest: " + normalizedManifestPath);
                return false;
            }

            manifestJson = manifestFile.GetAsText();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(manifestJson);
            if (!document.RootElement.TryGetProperty("frames", out JsonElement framesElement) ||
                framesElement.ValueKind != JsonValueKind.Array ||
                framesElement.GetArrayLength() == 0)
            {
                Log.Warn("[KuroKumoAnimatedBackground] Manifest has no usable frames: " + normalizedManifestPath);
                return false;
            }

            foreach (JsonElement frameElement in framesElement.EnumerateArray())
            {
                if (!TryParseFrame(frameElement, out string framePath, out int durationMs))
                {
                    Log.Warn("[KuroKumoAnimatedBackground] Invalid frame entry in manifest: " + normalizedManifestPath);
                    return false;
                }

                Texture2D? texture = ResourceLoader.Load<Texture2D>(framePath);
                if (texture == null)
                {
                    Log.Warn("[KuroKumoAnimatedBackground] Missing frame texture: " + framePath);
                    return false;
                }

                _frames.Add(texture);
                _durationsSeconds.Add(Math.Max(0.001d, durationMs / 1000d));
            }
        }
        catch (Exception ex)
        {
            Log.Warn("[KuroKumoAnimatedBackground] Failed to parse manifest: " + ex.Message);
            return false;
        }

        return _frames.Count > 0;
    }

    private static bool TryParseFrame(JsonElement frameElement, out string framePath, out int durationMs)
    {
        framePath = string.Empty;
        durationMs = 33;

        if (!frameElement.TryGetProperty("path", out JsonElement pathElement) ||
            pathElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? rawPath = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return false;
        }

        framePath = NormalizeResPath(rawPath);

        if (frameElement.TryGetProperty("duration_ms", out JsonElement durationElement) &&
            durationElement.ValueKind == JsonValueKind.Number &&
            durationElement.TryGetInt32(out int parsedDurationMs) &&
            parsedDurationMs > 0)
        {
            durationMs = parsedDurationMs;
        }

        return true;
    }

    private void ApplyStaticFallback(string reason)
    {
        SetProcess(false);
        _frames.Clear();
        _durationsSeconds.Clear();
        _currentFrameIndex = 0;
        _frameElapsedSeconds = 0d;

        if (_fallbackTexture != null)
        {
            Texture = _fallbackTexture;
        }

        Log.Warn("[KuroKumoAnimatedBackground] " + reason + " Fallback to static first frame.");
    }

    private static string NormalizeResPath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }
}

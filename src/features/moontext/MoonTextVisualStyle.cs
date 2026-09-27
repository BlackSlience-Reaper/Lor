using System;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.features.moontext;

internal static class MoonTextVisualStyle
{
    private const string PrimaryFontPath = "res://fonts/NANUMBARUNGOTHIC.ttf";
    private const float AsciiFallbackScale = 0.5f;

    private static readonly Color TextColor = new(0.9f, 0.15f, 0.15f);
    private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.92f);

    private static Font? _cachedPrimaryFont;
    private static bool _triedLoadPrimaryFont;
    private static bool _loggedFontLoadIssue;

    public const int FontSize = 42;
    public const float CharacterDelaySeconds = 0.1f;
    public const float FloatDelaySeconds = 5f;
    public const float FloatDurationSeconds = 2f;
    public const float FloatScale = 0.7f;
    public const float FloatDistance = 50f;
    public const float JitterIntervalSeconds = 0.4f;
    public const float JitterStrength = 1f;
    public const float CharacterSpacing = 5f;
    public const float TiltMinDegrees = 8f;
    public const float TiltMaxDegrees = 16f;

    public static void ConfigureCharacterLabel(RichTextLabel label, string text, bool bbcodeEnabled)
    {
        label.BbcodeEnabled = bbcodeEnabled;
        label.Text = text;
        label.FitContent = true;
        label.ScrollActive = false;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.Size = Vector2.Zero;
        label.Visible = false;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.FocusMode = Control.FocusModeEnum.None;

        label.AddThemeFontSizeOverride("normal_font_size", FontSize);
        label.AddThemeColorOverride("default_color", TextColor);
        label.AddThemeColorOverride("font_shadow_color", ShadowColor);
        label.AddThemeConstantOverride("shadow_outline_size", 3);
        label.AddThemeConstantOverride("shadow_offset_x", 3);
        label.AddThemeConstantOverride("shadow_offset_y", 3);

        Font? fontOverride = ResolveFontForLabel(label);
        if (fontOverride != null)
        {
            label.AddThemeFontOverride("normal_font", fontOverride);
        }
    }

    public static float CalculateAdvance(RichTextLabel label, char character)
    {
        if (!IsAscii(character))
        {
            return FontSize;
        }

        Font? font = label.GetThemeFont("normal_font");
        int fontSize = label.GetThemeFontSize("normal_font_size");
        if (font != null)
        {
            float advance = font.GetCharSize(character, fontSize).X;
            if (advance > 0f)
            {
                return advance;
            }
        }

        return fontSize * AsciiFallbackScale;
    }

    public static float CreateTextTiltDegrees()
    {
        float magnitude = (float)GD.RandRange(TiltMinDegrees, TiltMaxDegrees);
        return GD.Randf() < 0.5f ? -magnitude : magnitude;
    }

    public static float CreateTextTiltDegrees(RandomNumberGenerator rng)
    {
        float magnitude = rng.RandfRange(TiltMinDegrees, TiltMaxDegrees);
        return rng.Randf() < 0.5f ? -magnitude : magnitude;
    }

    public static Vector2 RandomPointNearAreaEdge(Rect2 area)
    {
        float minX = Mathf.Min(area.Position.X, area.Position.X + area.Size.X);
        float maxX = Mathf.Max(area.Position.X, area.Position.X + area.Size.X);
        float minY = Mathf.Min(area.Position.Y, area.Position.Y + area.Size.Y);
        float maxY = Mathf.Max(area.Position.Y, area.Position.Y + area.Size.Y);
        return RandomPointNearAreaEdge(minX, maxX, minY, maxY);
    }

    public static Vector2 RandomPointNearAreaEdge(float minX, float maxX, float minY, float maxY)
    {
        float width = Mathf.Max(1f, maxX - minX);
        float height = Mathf.Max(1f, maxY - minY);

        float edgeBandX = Mathf.Clamp(width * 0.18f, 40f, width * 0.45f);
        float edgeBandY = Mathf.Clamp(height * 0.18f, 40f, height * 0.45f);

        int side = (int)GD.Randi() % 4;
        return side switch
        {
            0 => new Vector2(
                (float)GD.RandRange(minX, minX + edgeBandX),
                (float)GD.RandRange(minY, maxY)),
            1 => new Vector2(
                (float)GD.RandRange(maxX - edgeBandX, maxX),
                (float)GD.RandRange(minY, maxY)),
            2 => new Vector2(
                (float)GD.RandRange(minX, maxX),
                (float)GD.RandRange(minY, minY + edgeBandY)),
            _ => new Vector2(
                (float)GD.RandRange(minX, maxX),
                (float)GD.RandRange(maxY - edgeBandY, maxY))
        };
    }

    public static Vector2 RandomPointNearAreaEdge(RandomNumberGenerator rng, float minX, float maxX, float minY, float maxY)
    {
        float width = Mathf.Max(1f, maxX - minX);
        float height = Mathf.Max(1f, maxY - minY);

        float edgeBandX = Mathf.Clamp(width * 0.18f, 40f, width * 0.45f);
        float edgeBandY = Mathf.Clamp(height * 0.18f, 40f, height * 0.45f);

        return rng.RandiRange(0, 3) switch
        {
            0 => new Vector2(
                rng.RandfRange(minX, minX + edgeBandX),
                rng.RandfRange(minY, maxY)),
            1 => new Vector2(
                rng.RandfRange(maxX - edgeBandX, maxX),
                rng.RandfRange(minY, maxY)),
            2 => new Vector2(
                rng.RandfRange(minX, maxX),
                rng.RandfRange(minY, minY + edgeBandY)),
            _ => new Vector2(
                rng.RandfRange(minX, maxX),
                rng.RandfRange(maxY - edgeBandY, maxY))
        };
    }

    private static Font? ResolveFontForLabel(Control context)
    {
        Font? primaryFont = GetPrimaryFont();
        if (primaryFont != null)
        {
            return primaryFont;
        }

        Font? themeFont = context.GetThemeFont("normal_font");
        return themeFont ?? ThemeDB.FallbackFont;
    }

    private static Font? GetPrimaryFont()
    {
        if (_triedLoadPrimaryFont)
        {
            return _cachedPrimaryFont;
        }

        _triedLoadPrimaryFont = true;
        try
        {
            _cachedPrimaryFont = ResourceLoader.Load<Font>(PrimaryFontPath);
        }
        catch (Exception ex)
        {
            if (!_loggedFontLoadIssue)
            {
                Log.Warn("[MoonText] Failed to load primary font, fallback to theme font: " + ex.Message);
                _loggedFontLoadIssue = true;
            }

            return null;
        }

        if (_cachedPrimaryFont == null && !_loggedFontLoadIssue)
        {
            Log.Warn("[MoonText] Primary font resource not found, fallback to theme font: " + PrimaryFontPath);
            _loggedFontLoadIssue = true;
        }

        return _cachedPrimaryFont;
    }

    private static bool IsAscii(char character) => character < 128;
}

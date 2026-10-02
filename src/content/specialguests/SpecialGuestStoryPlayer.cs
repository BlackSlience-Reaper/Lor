using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.content.specialguests;

/// <summary>
/// Full-screen, line-by-line story player used by every special guest.
/// It is constructed in code so a missing optional art resource can never stop
/// combat teardown or leave the action executor permanently paused.
/// </summary>
public static class SpecialGuestStoryPlayer
{
    private static SpecialGuestStoryOverlay? _activeOverlay;

    public static async Task PlayAsync(SpecialGuestStorySequence sequence)
    {
        if (sequence.Lines.Count == 0)
        {
            return;
        }

        Control? host = ResolveHost();
        if (host == null)
        {
            Log.Error($"[SpecialGuestStory] No overlay host for {sequence.Id}; treating local story as completed.");
            return;
        }

        SpecialGuestStoryOverlay? overlay = null;
        try
        {
            _activeOverlay?.CompleteImmediately();
            overlay = new SpecialGuestStoryOverlay(sequence)
            {
                Name = "SpecialGuestStory_" + SanitizeNodeName(sequence.Id),
            };
            _activeOverlay = overlay;
            host.AddChildSafely(overlay);
            overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            overlay.MoveToFront();
            await overlay.Completion;
        }
        catch (Exception exception)
        {
            Log.Error($"[SpecialGuestStory] Playback failed for {sequence.Id}; releasing local barrier: {exception}");
            overlay?.CompleteImmediately();
        }
        finally
        {
            if (overlay != null && GodotObject.IsInstanceValid(overlay))
            {
                overlay.QueueFreeSafely();
            }

            if (ReferenceEquals(_activeOverlay, overlay))
            {
                _activeOverlay = null;
            }
        }
    }

    public static void AbortActiveStory()
    {
        SpecialGuestStoryOverlay? overlay = _activeOverlay;
        _activeOverlay = null;
        overlay?.CompleteImmediately();
        if (overlay != null && GodotObject.IsInstanceValid(overlay))
        {
            overlay.QueueFreeSafely();
        }
    }

    private static Control? ResolveHost() =>
        NRun.Instance?.GlobalUi?.AboveTopBarVfxContainer
        ?? NCombatRoom.Instance?.CombatVfxContainer;

    private static string SanitizeNodeName(string value) =>
        value.Replace('/', '_').Replace(':', '_').Replace('.', '_');
}

internal sealed partial class SpecialGuestStoryOverlay : Control
{
    private const double CharactersPerSecond = 45d;
    internal const double LongPressSeconds = 2d;
    private const string VoiceSlot = "SpecialGuestStoryVoice";
    private const string SoundEffectSlot = "SpecialGuestStorySoundEffect";
    private const float SilentVolumeDb = -40f;
    private static readonly Vector2 DesignSize = new(1920f, 1080f);
    internal static readonly Vector2 SkipHintPosition = new(1090f, 1022f);
    internal static readonly Vector2 SkipHintSize = new(470f, 44f);
    private static readonly SpecialGuestStoryArtLayout DefaultArtLayout = new();
    private static readonly Color DialogueTextColor =
        new(0.84705883f, 0.83137256f, 0.7254902f);

    private readonly SpecialGuestStorySequence _sequence;
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TextureRect _blackBackground = new();
    private readonly Control _designRoot = new();
    private readonly TextureRect _cg = new();
    private readonly TextureRect _expression = new();
    private readonly TextureRect _dialogueOverlay = new();
    private readonly TextureRect _nameplate = new();
    private readonly TextureRect _advanceIcon = new();
    private readonly TextureRect _edgeFrame = new();
    private readonly Label _speaker = new();
    private readonly Label _text = new();
    private readonly Label _hint = new();
    private readonly AudioStreamPlayer _storyBgm = new();

    private int _lineIndex;
    private string _fullText = string.Empty;
    private double _visibleCharacters;
    private bool _lineComplete;
    private bool _inputHeld;
    private double _heldSeconds;
    private double _advancePulseSeconds;
    private bool _longPressConsumed;
    private bool _isComplete;
    private Tween? _storyBgmTween;
    private bool _isFadingRunMusic;
    private float _runMusicRestoreVolume;

    public Task Completion => _completion.Task;

    public SpecialGuestStoryOverlay(SpecialGuestStorySequence sequence)
    {
        _sequence = sequence;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
        SetProcessUnhandledInput(true);
        BuildUi();
        Resized += UpdateDesignTransform;
        TreeExiting += OnTreeExiting;
    }

    public override void _Ready()
    {
        UpdateDesignTransform();
        ShowLineSafely(0);
        GrabFocus();
    }

    public override void _Process(double delta)
    {
        if (_isComplete)
        {
            return;
        }

        if (!_lineComplete)
        {
            _visibleCharacters += delta * CharactersPerSecond;
            int count = Math.Min(_fullText.Length, (int)_visibleCharacters);
            _text.Text = _fullText[..count];
            if (count >= _fullText.Length)
            {
                CompleteCurrentLine();
            }
        }
        else
        {
            _advancePulseSeconds += delta;
            float alpha = 0.6f
                          + Mathf.PingPong((float)_advancePulseSeconds * 0.25f, 0.4f);
            _advanceIcon.Modulate = new Color(1f, 1f, 1f, alpha);
        }

        if (_inputHeld && !_longPressConsumed)
        {
            _heldSeconds += delta;
            if (_heldSeconds >= LongPressSeconds)
            {
                _longPressConsumed = true;
                CompleteImmediately();
            }
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (HandleInput(inputEvent))
        {
            AcceptEvent();
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (HandleInput(inputEvent))
        {
            GetViewport()?.SetInputAsHandled();
        }
    }

    public void CompleteImmediately()
    {
        if (_isComplete)
        {
            return;
        }

        _isComplete = true;
        try
        {
            LocalOggOneShotPlayer.StopExclusive(VoiceSlot);
            LocalOggOneShotPlayer.StopExclusive(SoundEffectSlot);
            StopStoryBgm();
        }
        catch (Exception exception)
        {
            Log.Error($"[SpecialGuestStory] Failed to stop story audio: {exception}");
        }
        finally
        {
            _completion.TrySetResult();
        }
    }

    private bool HandleInput(InputEvent inputEvent)
    {
        if (_isComplete
            || !IsConfirmInput(
                inputEvent,
                out bool pressed,
                out bool released,
                out bool canLongPressSkip))
        {
            return false;
        }

        if (pressed)
        {
            _inputHeld = canLongPressSkip;
            _heldSeconds = 0d;
            _longPressConsumed = false;
        }

        if (released)
        {
            _inputHeld = false;
            if (!_longPressConsumed)
            {
                Advance();
            }

            _heldSeconds = 0d;
        }

        return true;
    }

    private static bool IsConfirmInput(
        InputEvent inputEvent,
        out bool pressed,
        out bool released,
        out bool canLongPressSkip)
    {
        pressed = false;
        released = false;
        canLongPressSkip = false;

        switch (inputEvent)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse when !mouse.DoubleClick:
                pressed = mouse.Pressed;
                released = !mouse.Pressed;
                canLongPressSkip = true;
                return true;
            case InputEventScreenTouch touch:
                pressed = touch.Pressed;
                released = !touch.Pressed;
                canLongPressSkip = true;
                return true;
            case InputEventKey { Echo: false } key
                when key.IsAction(GameApi.Confirm):
                pressed = key.Pressed;
                released = !key.Pressed;
                canLongPressSkip = true;
                return true;
            case InputEventJoypadButton joypad
                when joypad.IsAction(GameApi.Confirm):
                pressed = joypad.Pressed;
                released = !joypad.Pressed;
                canLongPressSkip = true;
                return true;
            default:
                return false;
        }
    }

    private void Advance()
    {
        if (!_lineComplete)
        {
            CompleteCurrentLine();
            return;
        }

        int next = _lineIndex + 1;
        if (next >= _sequence.Lines.Count)
        {
            CompleteImmediately();
            return;
        }

        ShowLineSafely(next);
    }

    private void CompleteCurrentLine()
    {
        _visibleCharacters = _fullText.Length;
        _text.Text = _fullText;
        _lineComplete = true;
        _advancePulseSeconds = 0d;
        _advanceIcon.Visible = true;
    }

    private void ShowLineSafely(int index)
    {
        try
        {
            ShowLine(index);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"[SpecialGuestStory] Failed to show line {index} of {_sequence.Id}; "
                + $"completing local story safely: {exception}");
            CompleteImmediately();
        }
    }

    private void ShowLine(int index)
    {
        _lineIndex = index;
        SpecialGuestStoryLine line = _sequence.Lines[index];
        _fullText = line.Text.GetFormattedText();
        _visibleCharacters = 0d;
        _lineComplete = _fullText.Length == 0;
        _advancePulseSeconds = 0d;
        _text.Text = _lineComplete ? _fullText : string.Empty;
        _advanceIcon.Visible = _lineComplete;
        _advanceIcon.Modulate = Colors.White;
        string speaker = line.Speaker?.GetFormattedText() ?? string.Empty;
        _speaker.Text = speaker;
        _speaker.Visible = !string.IsNullOrWhiteSpace(speaker);
        _nameplate.Visible = _speaker.Visible;
        ApplyTexture(_cg, line.CgTexturePath);
        ApplyTexture(_expression, line.ExpressionTexturePath);
        ApplyArtLayout(line.ArtLayout);
        ApplyBgmCue(line.BgmCue);

        if (!string.IsNullOrWhiteSpace(line.SoundEffectPath))
        {
            LocalOggOneShotPlayer.PlayExclusive(
                SoundEffectSlot,
                line.SoundEffectPath,
                line.SoundEffectVolumeDb);
        }

        LocalOggOneShotPlayer.StopExclusive(VoiceSlot);
        if (!string.IsNullOrWhiteSpace(line.VoicePath))
        {
            LocalOggOneShotPlayer.PlayExclusive(
                VoiceSlot,
                line.VoicePath,
                line.VoiceVolumeDb,
                LibraryAudioCategory.Dialogue);
        }
    }

    private void ApplyBgmCue(SpecialGuestStoryBgmCue? cue)
    {
        if (cue == null || cue.Action == SpecialGuestStoryBgmAction.Continue)
        {
            return;
        }

        switch (cue.Action)
        {
            case SpecialGuestStoryBgmAction.FadeIn:
                StartStoryBgm(cue);
                break;
            case SpecialGuestStoryBgmAction.FadeOut:
                FadeOutStoryBgm(cue.FadeSeconds);
                break;
            case SpecialGuestStoryBgmAction.Stop:
                StopStoryBgm();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cue), cue.Action, "Unknown story BGM action.");
        }
    }

    private void StartStoryBgm(SpecialGuestStoryBgmCue cue)
    {
        if (string.IsNullOrWhiteSpace(cue.TrackPath))
        {
            Log.Error($"[SpecialGuestStory] FadeIn cue has no track path in {_sequence.Id} line {_lineIndex}.");
            return;
        }

        AudioStream? stream = ResourceLoader.Load<AudioStream>(
            cue.TrackPath);
        if (stream == null)
        {
            Log.Error("[SpecialGuestStory] Missing story BGM: " + cue.TrackPath);
            return;
        }

        AudioStream loopStream = stream;
        if (stream is AudioStreamOggVorbis ogg)
        {
            AudioStreamOggVorbis duplicate = (AudioStreamOggVorbis)ogg.Duplicate();
            duplicate.Loop = true;
            loopStream = duplicate;
        }

        KillStoryBgmTween();
        _storyBgm.Stop();
        _storyBgm.Stream = loopStream;
        float fadeSeconds = Math.Max(0f, cue.FadeSeconds);
        float targetVolumeDb = ResolveStoryBgmVolumeDb(cue.VolumeDb);
        _storyBgm.VolumeDb = fadeSeconds > 0f
            ? SilentVolumeDb
            : targetVolumeDb;
        _storyBgm.Play();
        if (fadeSeconds <= 0f)
        {
            return;
        }

        Tween tween = CreateTween();
        _storyBgmTween = tween;
        tween.TweenProperty(
            _storyBgm,
            "volume_db",
            targetVolumeDb,
            fadeSeconds);
        tween.Finished += () =>
        {
            if (ReferenceEquals(_storyBgmTween, tween))
            {
                _storyBgmTween = null;
            }
        };
    }

    private static float ResolveStoryBgmVolumeDb(float cueVolumeDb)
    {
        try
        {
            float rawVolume = SaveManager.Instance?.SettingsSave?.VolumeBgm ?? 1f;
            float effectiveScale = Mathf.Pow(
                Mathf.Clamp(rawVolume, 0f, 1f),
                2f);
            return effectiveScale <= 0f
                ? SilentVolumeDb
                : Math.Max(
                    SilentVolumeDb,
                    cueVolumeDb + Mathf.LinearToDb(effectiveScale));
        }
        catch (Exception exception)
        {
            Log.Warn(
                "[SpecialGuestStory] Failed to read global BGM volume; "
                + "using cue volume: "
                + exception.Message);
            return cueVolumeDb;
        }
    }

    private void FadeOutStoryBgm(float requestedFadeSeconds)
    {
        if (!_storyBgm.Playing)
        {
            FadeOutRunMusic(requestedFadeSeconds);
            return;
        }

        KillStoryBgmTween();
        float fadeSeconds = Math.Max(0f, requestedFadeSeconds);
        if (fadeSeconds <= 0f)
        {
            _storyBgm.Stop();
            _storyBgm.Stream = null;
            return;
        }

        Tween tween = CreateTween();
        _storyBgmTween = tween;
        tween.TweenProperty(_storyBgm, "volume_db", SilentVolumeDb, fadeSeconds);
        tween.Finished += () =>
        {
            if (!ReferenceEquals(_storyBgmTween, tween))
            {
                return;
            }

            _storyBgmTween = null;
            _storyBgm.Stop();
            _storyBgm.Stream = null;
        };
    }

    private void FadeOutRunMusic(float requestedFadeSeconds)
    {
        KillStoryBgmTween();
        NAudioManager? audioManager = NAudioManager.Instance;
        NRunMusicController? runMusic = NRunMusicController.Instance;
        if (audioManager == null || runMusic == null)
        {
            runMusic?.StopMusic();
            return;
        }

        _runMusicRestoreVolume = SaveManager.Instance.SettingsSave.VolumeBgm;
        float fadeSeconds = Math.Max(0f, requestedFadeSeconds);
        if (fadeSeconds <= 0f)
        {
            runMusic.StopMusic();
            audioManager.SetBgmVol(_runMusicRestoreVolume);
            return;
        }

        _isFadingRunMusic = true;
        Tween tween = CreateTween();
        _storyBgmTween = tween;
        tween.TweenMethod(
            Callable.From<float>(audioManager.SetBgmVol),
            _runMusicRestoreVolume,
            0f,
            fadeSeconds);
        tween.Finished += () => CompleteRunMusicFade(tween);
    }

    private void CompleteRunMusicFade(Tween tween)
    {
        if (!ReferenceEquals(_storyBgmTween, tween))
        {
            return;
        }

        _storyBgmTween = null;
        _isFadingRunMusic = false;
        NRunMusicController.Instance?.StopMusic();
        NAudioManager.Instance?.SetBgmVol(_runMusicRestoreVolume);
    }

    private void StopStoryBgm()
    {
        KillStoryBgmTween();
        _storyBgm.Stop();
        _storyBgm.Stream = null;
    }

    private void KillStoryBgmTween()
    {
        Tween? tween = _storyBgmTween;
        _storyBgmTween = null;
        if (tween != null && IsInstanceValid(tween))
        {
            tween.Kill();
        }

        if (_isFadingRunMusic)
        {
            _isFadingRunMusic = false;
            NRunMusicController.Instance?.StopMusic();
            NAudioManager.Instance?.SetBgmVol(_runMusicRestoreVolume);
        }
    }

    private static void ApplyTexture(TextureRect target, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            target.Texture = null;
            target.Visible = false;
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
        target.Texture = texture;
        target.Visible = texture != null;
        if (texture == null)
        {
            Log.Error("[SpecialGuestStory] Missing texture: " + path);
        }
    }

    private void BuildUi()
    {
        ConfigureUiTexture(_blackBackground, TextureRect.StretchModeEnum.Scale);
        ApplyTexture(_blackBackground, SpecialGuestStoryResources.BlackBackground);
        AddRootFullRect(_blackBackground);

        _designRoot.Name = "LibraryOfRuinaStoryCanvas";
        _designRoot.Size = DesignSize;
        _designRoot.ClipContents = true;
        _designRoot.MouseFilter = MouseFilterEnum.Ignore;
        this.AddChildSafely(_designRoot);

        _storyBgm.Name = "StoryBgm";
        _storyBgm.Bus = "Master";
        _storyBgm.VolumeDb = SilentVolumeDb;
        this.AddChildSafely(_storyBgm);

        ConfigureStoryTexture(_cg);
        AddDesignFullRect(_cg);
        ConfigureStoryTexture(_expression);
        AddDesignFullRect(_expression);

        ConfigureUiTexture(_dialogueOverlay, TextureRect.StretchModeEnum.Scale);
        ApplyTexture(_dialogueOverlay, SpecialGuestStoryResources.DialogueOverlay);
        AddDesignFullRect(_dialogueOverlay);

        ConfigureUiTexture(_nameplate, TextureRect.StretchModeEnum.Scale);
        ApplyTexture(_nameplate, SpecialGuestStoryResources.Nameplate);
        _designRoot.AddChildSafely(_nameplate);
        _nameplate.Position = new Vector2(157f, 710.41f);
        _nameplate.Size = new Vector2(416.77f, 173.59f);

        Font? storyFont = LoadStoryFont();

        _speaker.Position = new Vector2(210.642f, 756.375f);
        _speaker.Size = new Vector2(296.486f, 65.44f);
        _speaker.PivotOffset = _speaker.Size * 0.5f;
        _speaker.RotationDegrees = -16f;
        _speaker.AddThemeFontSizeOverride("font_size", 45);
        _speaker.AddThemeColorOverride("font_color", Colors.White);
        _speaker.HorizontalAlignment = HorizontalAlignment.Left;
        _speaker.VerticalAlignment = VerticalAlignment.Center;
        _speaker.MouseFilter = MouseFilterEnum.Ignore;
        ApplyFont(_speaker, storyFont);
        _designRoot.AddChildSafely(_speaker);

        _text.Position = new Vector2(321.5f, 839.2f);
        _text.Size = new Vector2(1277f, 230f);
        _text.AddThemeFontSizeOverride("font_size", 35);
        _text.AddThemeColorOverride("font_color", DialogueTextColor);
        _text.AddThemeConstantOverride("line_spacing", 20);
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.HorizontalAlignment = HorizontalAlignment.Center;
        _text.VerticalAlignment = VerticalAlignment.Center;
        _text.MouseFilter = MouseFilterEnum.Ignore;
        ApplyFont(_text, storyFont);
        _designRoot.AddChildSafely(_text);

        ConfigureUiTexture(_advanceIcon, TextureRect.StretchModeEnum.Scale);
        ApplyTexture(_advanceIcon, SpecialGuestStoryResources.AdvanceIcon);
        _advanceIcon.Position = new Vector2(1581f, 995f);
        _advanceIcon.Size = new Vector2(41.5f, 50f);
        _advanceIcon.Visible = false;
        _designRoot.AddChildSafely(_advanceIcon);

        _hint.Text = new LocString("gameplay_ui", "SPECIAL_GUEST_STORY.skip_hint").GetFormattedText();
        _hint.Position = SkipHintPosition;
        _hint.Size = SkipHintSize;
        _hint.AddThemeFontSizeOverride("font_size", 20);
        _hint.AddThemeColorOverride(
            "font_color",
            new Color(DialogueTextColor, 0.78f));
        _hint.HorizontalAlignment = HorizontalAlignment.Right;
        _hint.VerticalAlignment = VerticalAlignment.Center;
        _hint.MouseFilter = MouseFilterEnum.Ignore;
        ApplyFont(_hint, storyFont);
        _designRoot.AddChildSafely(_hint);

        ConfigureUiTexture(_edgeFrame, TextureRect.StretchModeEnum.Scale);
        ApplyTexture(_edgeFrame, SpecialGuestStoryResources.EdgeFrame);
        AddDesignFullRect(_edgeFrame);
    }

    private static void ConfigureStoryTexture(TextureRect texture)
    {
        texture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        texture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        texture.MouseFilter = MouseFilterEnum.Ignore;
        texture.PivotOffset = DesignSize * 0.5f;
    }

    private static void ConfigureUiTexture(
        TextureRect texture,
        TextureRect.StretchModeEnum stretchMode)
    {
        texture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        texture.StretchMode = stretchMode;
        texture.MouseFilter = MouseFilterEnum.Ignore;
    }

    private Font? LoadStoryFont()
    {
        return SpecialGuestStoryResources.LoadFont(LocManager.Instance.Language);
    }

    private static void ApplyFont(Label label, Font? font)
    {
        if (font != null)
        {
            label.AddThemeFontOverride("font", font);
        }
    }

    private void ApplyArtLayout(SpecialGuestStoryArtLayout? layout)
    {
        SpecialGuestStoryArtLayout resolved = layout ?? DefaultArtLayout;
        Vector2 scale = Vector2.One * resolved.Scale;
        Vector2 position = new(resolved.OffsetX, resolved.OffsetY);
        ApplyArtLayout(_cg, scale, position);
        ApplyArtLayout(_expression, scale, position);
    }

    private static void ApplyArtLayout(
        TextureRect texture,
        Vector2 scale,
        Vector2 position)
    {
        texture.Scale = scale;
        texture.Position = position;
    }

    private void UpdateDesignTransform()
    {
        if (Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        float scale = Mathf.Min(Size.X / DesignSize.X, Size.Y / DesignSize.Y);
        _designRoot.Scale = Vector2.One * scale;
        _designRoot.Position = (Size - DesignSize * scale) * 0.5f;
    }

    private void AddRootFullRect(Control child)
    {
        this.AddChildSafely(child);
        child.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void AddDesignFullRect(Control child)
    {
        _designRoot.AddChildSafely(child);
        child.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void OnTreeExiting()
    {
        CompleteImmediately();
    }
}

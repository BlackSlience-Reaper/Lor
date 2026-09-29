using Godot;
using LibraryOfRuina.features.moontext;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.abnormalities.ScorchedGirl;

public partial class ScorchedGirlBackgroundTextController : Control
{
    private static readonly string[] BackgroundTextKeys =
    {
        "SCORCHED_GIRL_MONSTER.backgroundText.0",
        "SCORCHED_GIRL_MONSTER.backgroundText.1",
        "SCORCHED_GIRL_MONSTER.backgroundText.2",
        "SCORCHED_GIRL_MONSTER.backgroundText.3",
        "SCORCHED_GIRL_MONSTER.backgroundText.4"
    };

    private const float SpawnIntervalSeconds = 5f;
    private static readonly Vector2 RandomAreaX = new(150f, 1300f);
    private static readonly Vector2 RandomAreaY = new(200f, 650f);

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<ActiveText> _activeTexts = [];
    private readonly List<int> _shuffledTextIndices = [];
    private int _shuffledTextCursor;
    private int _lastBackgroundTextIndex = -1;
    private Timer? _spawnTimer;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        _rng.Randomize();

        _spawnTimer = new Timer
        {
            WaitTime = SpawnIntervalSeconds,
            OneShot = false,
            Autostart = true,
            ProcessCallback = Timer.TimerProcessCallback.Idle
        };
        _spawnTimer.Timeout += OnSpawnTimeout;
        AddChild(_spawnTimer);
    }

    public override void _ExitTree()
    {
        if (_spawnTimer != null)
        {
            _spawnTimer.Timeout -= OnSpawnTimeout;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _activeTexts.Count - 1; i >= 0; i--)
        {
            ActiveText text = _activeTexts[i];
            if (UpdateActiveText(text, dt))
            {
                continue;
            }

            if (IsInstanceValid(text.Root))
            {
                text.Root.QueueFree();
            }

            _activeTexts.RemoveAt(i);
        }
    }

    private void OnSpawnTimeout()
    {
        string? locKey = GetNextBackgroundTextKey();
        if (locKey == null)
        {
            return;
        }

        string line = MonsterModel.L10NMonsterLookup(locKey).GetFormattedText();
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        ActiveText? activeText = BuildActiveText(line);
        if (activeText != null)
        {
            _activeTexts.Add(activeText);
        }
    }

    private string? GetNextBackgroundTextKey()
    {
        if (BackgroundTextKeys.Length == 0)
        {
            return null;
        }

        if (_shuffledTextCursor >= _shuffledTextIndices.Count)
        {
            RefillShuffledTextIndices();
        }

        if (_shuffledTextCursor >= _shuffledTextIndices.Count)
        {
            return null;
        }

        int index = _shuffledTextIndices[_shuffledTextCursor++];
        _lastBackgroundTextIndex = index;
        return BackgroundTextKeys[index];
    }

    private void RefillShuffledTextIndices()
    {
        _shuffledTextIndices.Clear();

        for (int i = 0; i < BackgroundTextKeys.Length; i++)
        {
            _shuffledTextIndices.Add(i);
        }

        for (int i = _shuffledTextIndices.Count - 1; i > 0; i--)
        {
            int swapIndex = _rng.RandiRange(0, i);
            (_shuffledTextIndices[i], _shuffledTextIndices[swapIndex]) = (_shuffledTextIndices[swapIndex], _shuffledTextIndices[i]);
        }

        
        if (_shuffledTextIndices.Count > 1 && _lastBackgroundTextIndex >= 0 && _shuffledTextIndices[0] == _lastBackgroundTextIndex)
        {
            for (int i = 1; i < _shuffledTextIndices.Count; i++)
            {
                if (_shuffledTextIndices[i] != _lastBackgroundTextIndex)
                {
                    (_shuffledTextIndices[0], _shuffledTextIndices[i]) = (_shuffledTextIndices[i], _shuffledTextIndices[0]);
                    break;
                }
            }
        }

        _shuffledTextCursor = 0;
    }

    private ActiveText? BuildActiveText(string line)
    {
        MoonTextBbcodeParser.ParsedCharacterStream parsed = MoonTextBbcodeParser.ParsePerCharacter(line);
        if (parsed.WrappedCharacters.Count == 0)
        {
            return null;
        }

        const float ySlope = 0f;
        Vector2 spawnPos = MoonTextVisualStyle.RandomPointNearAreaEdge(
            _rng,
            RandomAreaX.X,
            RandomAreaX.Y,
            RandomAreaY.X,
            RandomAreaY.Y);

        Control root = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            Position = spawnPos,
            Visible = true,
            RotationDegrees = MoonTextVisualStyle.CreateTextTiltDegrees(_rng),
            Modulate = Colors.White
        };
        AddChild(root);

        List<RichTextLabel> labels = [];
        List<Vector2> basePositions = [];
        List<Vector2> jitterOffsets = [];

        Vector2 cursor = Vector2.Zero;
        for (int i = 0; i < parsed.WrappedCharacters.Count; i++)
        {
            char rawChar = parsed.RawCharacters[i];
            if (rawChar == '\r')
            {
                continue;
            }

            if (rawChar == '\n')
            {
                cursor.X = 0f;
                cursor.Y += MoonTextVisualStyle.FontSize;
                continue;
            }

            RichTextLabel label = CreateCharacterLabel(parsed.WrappedCharacters[i]);
            root.AddChild(label);
            label.Position = cursor;

            labels.Add(label);
            basePositions.Add(cursor);
            jitterOffsets.Add(Vector2.Zero);

            float advance = MoonTextVisualStyle.CalculateAdvance(label, rawChar);
            cursor.X += advance + MoonTextVisualStyle.CharacterSpacing;
            cursor.Y += ySlope;
        }

        if (labels.Count == 0)
        {
            root.QueueFree();
            return null;
        }

        Rect2 bounds = CalculateBounds(labels);
        root.Size = bounds.Size;
        for (int i = 0; i < labels.Count; i++)
        {
            Vector2 adjusted = labels[i].Position - bounds.Position;
            labels[i].Position = adjusted;
            basePositions[i] = adjusted;
        }

        return new ActiveText
        {
            Root = root,
            Labels = labels,
            BasePositions = basePositions,
            JitterOffsets = jitterOffsets,
            CharacterVisible = new bool[labels.Count],
            NextJitterUpdateSeconds = new float[labels.Count],
        };
    }

    private static RichTextLabel CreateCharacterLabel(string wrappedCharacter)
    {
        RichTextLabel label = new RichTextLabel();
        MoonTextVisualStyle.ConfigureCharacterLabel(label, wrappedCharacter, bbcodeEnabled: true);
        return label;
    }

    private bool UpdateActiveText(ActiveText text, float dt)
    {
        if (!IsInstanceValid(text.Root))
        {
            return false;
        }

        text.Elapsed += dt;

        RevealCharacters(text);
        UpdateJitterOffsets(text);
        float ratio = UpdateFloatAndFade(text, out float floatOffset);
        UpdatePositions(text, floatOffset);

        return ratio < 1f;
    }

    private void RevealCharacters(ActiveText text)
    {
        for (int i = 0; i < text.Labels.Count; i++)
        {
            if (text.CharacterVisible[i])
            {
                continue;
            }

            float revealAt = i * MoonTextVisualStyle.CharacterDelaySeconds;
            if (text.Elapsed < revealAt)
            {
                break;
            }

            text.CharacterVisible[i] = true;
            text.Labels[i].Visible = true;
            RandomizeJitter(text, i);
            text.NextJitterUpdateSeconds[i] = text.Elapsed + MoonTextVisualStyle.JitterIntervalSeconds;
        }
    }

    private void UpdateJitterOffsets(ActiveText text)
    {
        if (text.Root.Modulate.A <= 0f)
        {
            return;
        }

        for (int i = 0; i < text.Labels.Count; i++)
        {
            if (!text.CharacterVisible[i] || text.Elapsed < text.NextJitterUpdateSeconds[i])
            {
                continue;
            }

            RandomizeJitter(text, i);
            while (text.NextJitterUpdateSeconds[i] <= text.Elapsed)
            {
                text.NextJitterUpdateSeconds[i] += MoonTextVisualStyle.JitterIntervalSeconds;
            }
        }
    }

    private static float UpdateFloatAndFade(ActiveText text, out float floatOffset)
    {
        float floatStart = text.Labels.Count * MoonTextVisualStyle.CharacterDelaySeconds + MoonTextVisualStyle.FloatDelaySeconds;
        if (text.Elapsed < floatStart)
        {
            floatOffset = 0f;
            text.Root.Modulate = Colors.White;

            foreach (RichTextLabel label in text.Labels)
            {
                label.Scale = Vector2.One;
            }

            return 0f;
        }

        float ratio = Mathf.Clamp((text.Elapsed - floatStart) / MoonTextVisualStyle.FloatDurationSeconds, 0f, 1f);
        floatOffset = Mathf.Lerp(0f, -MoonTextVisualStyle.FloatDistance, ratio);
        text.Root.Modulate = new Color(1f, 1f, 1f, 1f - ratio);

        Vector2 targetScale = new Vector2(MoonTextVisualStyle.FloatScale, MoonTextVisualStyle.FloatScale);
        foreach (RichTextLabel label in text.Labels)
        {
            label.Scale = Vector2.One.Lerp(targetScale, ratio);
        }

        return ratio;
    }

    private static void UpdatePositions(ActiveText text, float floatOffset)
    {
        Vector2 verticalOffset = new Vector2(0f, floatOffset);
        for (int i = 0; i < text.Labels.Count; i++)
        {
            text.Labels[i].Position = text.BasePositions[i] + text.JitterOffsets[i] + verticalOffset;
        }
    }

    private void RandomizeJitter(ActiveText text, int index)
    {
        text.JitterOffsets[index] = new Vector2(
            _rng.RandfRange(-MoonTextVisualStyle.JitterStrength, MoonTextVisualStyle.JitterStrength),
            _rng.RandfRange(-MoonTextVisualStyle.JitterStrength, MoonTextVisualStyle.JitterStrength));
    }

    private static Rect2 CalculateBounds(IReadOnlyList<RichTextLabel> labels)
    {
        Rect2 bounds = new Rect2(labels[0].Position, labels[0].GetCombinedMinimumSize());
        for (int i = 1; i < labels.Count; i++)
        {
            Rect2 rect = new Rect2(labels[i].Position, labels[i].GetCombinedMinimumSize());
            bounds = bounds.Merge(rect);
        }

        return bounds;
    }

    private sealed class ActiveText
    {
        public required Control Root { get; init; }

        public required List<RichTextLabel> Labels { get; init; }

        public required List<Vector2> BasePositions { get; init; }

        public required List<Vector2> JitterOffsets { get; init; }

        public required bool[] CharacterVisible { get; init; }

        public required float[] NextJitterUpdateSeconds { get; init; }

        public float Elapsed { get; set; }
    }
}

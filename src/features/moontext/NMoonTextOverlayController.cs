using System;
using System.Linq;
using Godot;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.features.moontext;

internal partial class NMoonTextOverlayController : Control
{
    public const string ControllerNodeName = "MoonTextOverlayController";

    private static readonly Rect2 DefaultSpawnArea = new Rect2(0f, 200f, 1350f, 450f);

    private readonly List<ActiveMoonTextInstance> _activeInstances = [];
    private RandomLoopState? _randomLoop;
    private SequenceState? _sequence;
    private float _elapsedSeconds;
    private NCombatRoom? _combatRoom;
    private int[] _randomLoopLineOrder = [];
    private int _randomLoopLineCursor;
    private int _lastRandomLoopLineIndex = -1;

    public NCombatRoom? CombatRoom => _combatRoom;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _ExitTree()
    {
        _randomLoop = null;
        _sequence = null;
        _combatRoom = null;
        _randomLoopLineOrder = [];
        _randomLoopLineCursor = 0;
        _lastRandomLoopLineIndex = -1;
        ClearActiveInstances();
    }

    public void BindRoom(NCombatRoom room)
    {
        _combatRoom = room;
    }

    public void ShowLine(Creature speaker, LocString line)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled || speaker.IsDead)
        {
            return;
        }

        if (!TryResolveSpeakerPosition(speaker, out Vector2 globalPosition))
        {
            return;
        }

        string formattedText = line.GetFormattedText();
        if (string.IsNullOrWhiteSpace(formattedText))
        {
            return;
        }

        SpawnText(formattedText, globalPosition);
    }

    public void StartRandomLoop(IReadOnlyList<LocString> lines, float intervalSeconds, Rect2 spawnArea)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled || lines.Count == 0)
        {
            _randomLoop = null;
            _randomLoopLineOrder = [];
            _randomLoopLineCursor = 0;
            _lastRandomLoopLineIndex = -1;
            return;
        }

        float sanitizedInterval = Mathf.Max(0.01f, intervalSeconds);
        _randomLoop = new RandomLoopState(lines.ToArray(), sanitizedInterval, spawnArea, _elapsedSeconds + sanitizedInterval);
        _randomLoopLineOrder = [];
        _randomLoopLineCursor = 0;
        _lastRandomLoopLineIndex = -1;
    }

    public void StartRandomLoop(object owner, string scope, IReadOnlyList<LocString> lines, float intervalSeconds, Rect2 spawnArea)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled || lines.Count == 0)
        {
            StopRandomLoop(owner, scope);
            return;
        }

        float sanitizedInterval = Mathf.Max(0.01f, intervalSeconds);
        _randomLoop = new RandomLoopState(owner, scope, lines.ToArray(), sanitizedInterval, spawnArea, _elapsedSeconds + sanitizedInterval);
        ResetRandomLoopOrder();
    }

    public void StopRandomLoop(object owner, string scope)
    {
        if (_randomLoop == null
            || !ReferenceEquals(_randomLoop.Owner, owner)
            || !string.Equals(_randomLoop.Scope, scope, StringComparison.Ordinal))
        {
            return;
        }

        _randomLoop = null;
        _sequence = null;
        ResetRandomLoopOrder();
    }

    public void StartSequence(IReadOnlyList<MoonTextSequenceEntry> entries)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled || entries.Count == 0)
        {
            _sequence = null;
            return;
        }

        MoonTextSequenceEntry[] orderedEntries = entries
            .OrderBy(entry => entry.AbsoluteTriggerTimeSeconds)
            .ToArray();

        if (orderedEntries.Length == 0)
        {
            _sequence = null;
            return;
        }

        _sequence = new SequenceState(orderedEntries, _elapsedSeconds);
    }

    public override void _Process(double delta)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled)
        {
            if (_activeInstances.Count > 0 || _randomLoop != null || _sequence != null)
            {
                _randomLoop = null;
                _sequence = null;
                ClearActiveInstances();
            }

            return;
        }

        _elapsedSeconds += (float)delta;
        TickRandomLoop();
        TickSequence();
        TickActiveInstances((float)delta);
    }

    private void TickRandomLoop()
    {
        if (_randomLoop == null || _randomLoop.Lines.Count == 0)
        {
            return;
        }

        int guard = 0;
        while (_elapsedSeconds >= _randomLoop.NextTriggerTimeSeconds && guard++ < 64)
        {
            int index = GetNextRandomLoopLineIndex(_randomLoop.Lines.Count);
            if (index < 0)
            {
                return;
            }

            LocString line = _randomLoop.Lines[index];
            string formattedText = line.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(formattedText))
            {
                SpawnText(formattedText, RandomPointInArea(_randomLoop.SpawnArea));
            }

            _randomLoop.NextTriggerTimeSeconds += _randomLoop.IntervalSeconds;
        }
    }

    private int GetNextRandomLoopLineIndex(int lineCount)
    {
        if (lineCount <= 0)
        {
            return -1;
        }

        if (_randomLoopLineCursor >= _randomLoopLineOrder.Length)
        {
            RefillRandomLoopLineOrder(lineCount);
        }

        if (_randomLoopLineCursor >= _randomLoopLineOrder.Length)
        {
            return -1;
        }

        int index = _randomLoopLineOrder[_randomLoopLineCursor++];
        _lastRandomLoopLineIndex = index;
        return index;
    }

    private void RefillRandomLoopLineOrder(int lineCount)
    {
        _randomLoopLineOrder = new int[lineCount];
        for (int i = 0; i < lineCount; i++)
        {
            _randomLoopLineOrder[i] = i;
        }

        for (int i = lineCount - 1; i > 0; i--)
        {
            int swapIndex = GD.RandRange(0, i);
            (_randomLoopLineOrder[i], _randomLoopLineOrder[swapIndex]) = (_randomLoopLineOrder[swapIndex], _randomLoopLineOrder[i]);
        }

        
        if (lineCount > 1 && _lastRandomLoopLineIndex >= 0 && _randomLoopLineOrder[0] == _lastRandomLoopLineIndex)
        {
            for (int i = 1; i < lineCount; i++)
            {
                if (_randomLoopLineOrder[i] != _lastRandomLoopLineIndex)
                {
                    (_randomLoopLineOrder[0], _randomLoopLineOrder[i]) = (_randomLoopLineOrder[i], _randomLoopLineOrder[0]);
                    break;
                }
            }
        }

        _randomLoopLineCursor = 0;
    }

    private void ResetRandomLoopOrder()
    {
        _randomLoopLineOrder = [];
        _randomLoopLineCursor = 0;
        _lastRandomLoopLineIndex = -1;
    }

    private void TickSequence()
    {
        if (_sequence == null)
        {
            return;
        }

        while (_sequence.NextIndex < _sequence.Entries.Count)
        {
            MoonTextSequenceEntry entry = _sequence.Entries[_sequence.NextIndex];
            float triggerTime = _sequence.StartTimeSeconds + entry.AbsoluteTriggerTimeSeconds;
            if (_elapsedSeconds < triggerTime)
            {
                break;
            }

            string formattedText = entry.Line.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(formattedText))
            {
                SpawnText(formattedText, RandomPointInArea(DefaultSpawnArea));
            }

            _sequence.NextIndex++;
        }

        if (_sequence.NextIndex >= _sequence.Entries.Count)
        {
            _sequence = null;
        }
    }

    private void TickActiveInstances(float deltaSeconds)
    {
        for (int i = _activeInstances.Count - 1; i >= 0; i--)
        {
            if (_activeInstances[i].Tick(deltaSeconds))
            {
                continue;
            }

            _activeInstances.RemoveAt(i);
        }
    }

    private void SpawnText(string text, Vector2 globalPosition)
    {
        ActiveMoonTextInstance? instance = ActiveMoonTextInstance.Create(this, text, globalPosition);
        if (instance != null)
        {
            _activeInstances.Add(instance);
        }
    }

    private bool TryResolveSpeakerPosition(Creature speaker, out Vector2 globalPosition)
    {
        globalPosition = Vector2.Zero;
        NCombatRoom? room = _combatRoom;
        if (room == null || !IsInstanceValid(room))
        {
            return false;
        }

        NCreature? creatureNode = room.GetCreatureNode(speaker);
        if (creatureNode == null || !IsInstanceValid(creatureNode))
        {
            return false;
        }

        if (creatureNode.Visuals.TalkPosition != null)
        {
            globalPosition = creatureNode.Visuals.TalkPosition.GlobalPosition;
            return true;
        }

        Vector2 result = creatureNode.VfxSpawnPosition + new Vector2(0f, (0f - creatureNode.Hitbox.Size.Y) * 0.5f * 0.75f);
        if (speaker.Side == CombatSide.Player)
        {
            result.X += creatureNode.Hitbox.Size.X * 0.75f;
        }
        else
        {
            result.X -= creatureNode.Hitbox.Size.X * 0.75f;
        }

        globalPosition = result;
        return true;
    }

    private static Vector2 RandomPointInArea(Rect2 area)
    {
        return MoonTextVisualStyle.RandomPointNearAreaEdge(area);
    }

    private void ClearActiveInstances()
    {
        foreach (ActiveMoonTextInstance instance in _activeInstances)
        {
            instance.Dispose();
        }

        _activeInstances.Clear();
    }

    private sealed class RandomLoopState
    {
        public RandomLoopState(
            IReadOnlyList<LocString> lines,
            float intervalSeconds,
            Rect2 spawnArea,
            float nextTriggerTimeSeconds)
            : this(null, string.Empty, lines, intervalSeconds, spawnArea, nextTriggerTimeSeconds)
        {
        }

        public RandomLoopState(
            object? owner,
            string scope,
            IReadOnlyList<LocString> lines,
            float intervalSeconds,
            Rect2 spawnArea,
            float nextTriggerTimeSeconds)
        {
            Owner = owner;
            Scope = scope;
            Lines = lines;
            IntervalSeconds = intervalSeconds;
            SpawnArea = spawnArea;
            NextTriggerTimeSeconds = nextTriggerTimeSeconds;
        }

        public object? Owner { get; }

        public string Scope { get; }

        public IReadOnlyList<LocString> Lines { get; }

        public float IntervalSeconds { get; }

        public Rect2 SpawnArea { get; }

        public float NextTriggerTimeSeconds { get; set; }
    }

    private sealed class SequenceState(IReadOnlyList<MoonTextSequenceEntry> entries, float startTimeSeconds)
    {
        public IReadOnlyList<MoonTextSequenceEntry> Entries { get; } = entries;

        public float StartTimeSeconds { get; } = startTimeSeconds;

        public int NextIndex { get; set; }
    }

    private sealed class ActiveMoonTextInstance
    {
        private readonly Control _root;
        private readonly List<RichTextLabel> _labels = [];
        private readonly List<Vector2> _basePositions = [];
        private readonly List<Vector2> _jitterOffsets = [];
        private readonly bool[] _charVisible;
        private readonly float[] _nextJitterUpdateSeconds;
        private readonly float _charOffsetY;

        private float _elapsedSeconds;
        private float _floatOffset;

        private ActiveMoonTextInstance(Control root, int charCount)
        {
            _root = root;
            _charVisible = new bool[charCount];
            _nextJitterUpdateSeconds = new float[charCount];
            _charOffsetY = 0f;
        }

        public static ActiveMoonTextInstance? Create(NMoonTextOverlayController owner, string text, Vector2 globalPosition)
        {
            MoonTextBbcodeParser.ParsedCharacterStream parsed = MoonTextBbcodeParser.ParsePerCharacter(text);
            if (parsed.WrappedCharacters.Count == 0)
            {
                return null;
            }

            Control root = new Control
            {
                Name = "MoonTextInstance",
                MouseFilter = MouseFilterEnum.Ignore,
                FocusMode = FocusModeEnum.None,
                Visible = true,
                GlobalPosition = globalPosition,
                RotationDegrees = MoonTextVisualStyle.CreateTextTiltDegrees(),
                Modulate = Colors.White,
            };

            owner.AddChildSafely(root);

            ActiveMoonTextInstance instance = new ActiveMoonTextInstance(root, parsed.WrappedCharacters.Count);
            instance.CreateCharacterLabels(parsed);
            return instance;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!IsInstanceValid(_root))
            {
                return false;
            }

            _elapsedSeconds += deltaSeconds;

            RevealCharacters();
            UpdateJitterOffsets();
            float ratio = UpdateFloatAndFade();
            UpdatePositions();

            if (ratio >= 1f)
            {
                Dispose();
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            if (IsInstanceValid(_root))
            {
                _root.Visible = false;
                _root.QueueFreeSafely();
            }
        }

        private void CreateCharacterLabels(MoonTextBbcodeParser.ParsedCharacterStream parsed)
        {
            Vector2 cursor = Vector2.Zero;
            for (int i = 0; i < parsed.WrappedCharacters.Count; i++)
            {
                RichTextLabel label = CreateCharacterLabel(parsed.WrappedCharacters[i]);
                _root.AddChildSafely(label);

                label.Position = cursor;
                _labels.Add(label);
                _basePositions.Add(cursor);
                _jitterOffsets.Add(Vector2.Zero);

                float advance = MoonTextVisualStyle.CalculateAdvance(label, parsed.RawCharacters[i]);
                cursor.X += advance + MoonTextVisualStyle.CharacterSpacing;
                cursor.Y += _charOffsetY;
            }

            NormalizeBounds();
        }

        private static RichTextLabel CreateCharacterLabel(string wrappedCharacter)
        {
            RichTextLabel label = new RichTextLabel();
            MoonTextVisualStyle.ConfigureCharacterLabel(label, wrappedCharacter, bbcodeEnabled: true);
            return label;
        }

        private void NormalizeBounds()
        {
            if (_labels.Count == 0)
            {
                return;
            }

            RichTextLabel first = _labels[0];
            Rect2 bounds = new Rect2(first.Position, first.GetCombinedMinimumSize());

            for (int i = 1; i < _labels.Count; i++)
            {
                RichTextLabel label = _labels[i];
                Rect2 rect = new Rect2(label.Position, label.GetCombinedMinimumSize());
                bounds = bounds.Merge(rect);
            }

            _root.Size = bounds.Size;
            for (int i = 0; i < _labels.Count; i++)
            {
                Vector2 normalizedPosition = _labels[i].Position - bounds.Position;
                _labels[i].Position = normalizedPosition;
                _basePositions[i] = normalizedPosition;
            }
        }

        private void RevealCharacters()
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                if (_charVisible[i])
                {
                    continue;
                }

                float revealAt = i * MoonTextVisualStyle.CharacterDelaySeconds;
                if (_elapsedSeconds < revealAt)
                {
                    break;
                }

                _charVisible[i] = true;
                _labels[i].Visible = true;
                RandomizeJitter(i);
                _nextJitterUpdateSeconds[i] = _elapsedSeconds + MoonTextVisualStyle.JitterIntervalSeconds;
            }
        }

        private void UpdateJitterOffsets()
        {
            if (_root.Modulate.A <= 0f)
            {
                return;
            }

            for (int i = 0; i < _labels.Count; i++)
            {
                if (!_charVisible[i] || _elapsedSeconds < _nextJitterUpdateSeconds[i])
                {
                    continue;
                }

                RandomizeJitter(i);
                while (_nextJitterUpdateSeconds[i] <= _elapsedSeconds)
                {
                    _nextJitterUpdateSeconds[i] += MoonTextVisualStyle.JitterIntervalSeconds;
                }
            }
        }

        private float UpdateFloatAndFade()
        {
            float floatStart = _labels.Count * MoonTextVisualStyle.CharacterDelaySeconds + MoonTextVisualStyle.FloatDelaySeconds;
            if (_elapsedSeconds < floatStart)
            {
                _floatOffset = 0f;
                _root.Modulate = Colors.White;

                foreach (RichTextLabel label in _labels)
                {
                    label.Scale = Vector2.One;
                }

                return 0f;
            }

            float ratio = Mathf.Clamp((_elapsedSeconds - floatStart) / MoonTextVisualStyle.FloatDurationSeconds, 0f, 1f);
            _floatOffset = Mathf.Lerp(0f, -MoonTextVisualStyle.FloatDistance, ratio);
            _root.Modulate = new Color(1f, 1f, 1f, 1f - ratio);

            Vector2 targetScale = new Vector2(MoonTextVisualStyle.FloatScale, MoonTextVisualStyle.FloatScale);
            foreach (RichTextLabel label in _labels)
            {
                label.Scale = Vector2.One.Lerp(targetScale, ratio);
            }

            return ratio;
        }

        private void UpdatePositions()
        {
            Vector2 verticalOffset = new Vector2(0f, _floatOffset);
            for (int i = 0; i < _labels.Count; i++)
            {
                _labels[i].Position = _basePositions[i] + _jitterOffsets[i] + verticalOffset;
            }
        }

        private void RandomizeJitter(int index)
        {
            _jitterOffsets[index] = new Vector2(
                (float)GD.RandRange(-MoonTextVisualStyle.JitterStrength, MoonTextVisualStyle.JitterStrength),
                (float)GD.RandRange(-MoonTextVisualStyle.JitterStrength, MoonTextVisualStyle.JitterStrength));
        }
    }
}


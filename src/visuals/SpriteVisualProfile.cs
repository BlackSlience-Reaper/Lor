using System;
using System.Linq;
using Godot;

namespace LibraryOfRuina.visuals;

internal enum SpriteVisualAnchorMode
{
    VisibleBottomCenter,
    ImageBottomCenter,
    ManualBottomCenter,
    Center
}

internal enum SpriteAnimationFrameSelection
{
    First,
    Cycle,
    Random
}

internal sealed class SpriteVisualVariantDefinition
{
    internal SpriteVisualVariantDefinition(
        string key,
        string idleFrameKey,
        string idleTexturePath)
    {
        Key = key;
        IdleFrameKey = idleFrameKey;
        IdleTexturePath = idleTexturePath;
    }

    internal string Key { get; }

    internal string IdleFrameKey { get; }

    internal string IdleTexturePath { get; }

    internal Vector2 Position { get; private set; }

    internal Vector2? ScaleValue { get; private set; }

    internal float? CharacterAnchorX { get; private set; }

    internal float? CharacterAnchorY { get; private set; }

    internal bool FlipH { get; private set; }

    internal bool IdleOnlyLayout { get; private set; }

    internal SpriteVisualVariantDefinition At(float x, float y)
    {
        Position = new Vector2(x, y);
        return this;
    }

    internal SpriteVisualVariantDefinition Scale(float uniformScale) =>
        Scale(uniformScale, uniformScale);

    internal SpriteVisualVariantDefinition Scale(float x, float y)
    {
        if (!float.IsFinite(x)
            || !float.IsFinite(y)
            || Mathf.IsZeroApprox(x)
            || Mathf.IsZeroApprox(y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                "Sprite variant scale components must be finite and non-zero.");
        }

        ScaleValue = new Vector2(x, y);
        return this;
    }

    internal SpriteVisualVariantDefinition AnchorX(float x)
    {
        CharacterAnchorX = x;
        return this;
    }

    internal SpriteVisualVariantDefinition AnchorY(float y)
    {
        CharacterAnchorY = y;
        return this;
    }

    internal SpriteVisualVariantDefinition Anchor(float x, float y) =>
        AnchorX(x).AnchorY(y);

    internal SpriteVisualVariantDefinition Flip()
    {
        FlipH = true;
        return this;
    }

    internal SpriteVisualVariantDefinition IdleOnly()
    {
        IdleOnlyLayout = true;
        return this;
    }
}

internal sealed class SpriteFrameDefinition
{
    internal SpriteFrameDefinition(
        string key,
        string texturePath)
    {
        Key = key;
        TexturePath = texturePath;
    }

    internal string Key { get; }

    internal string TexturePath { get; }

    /// <summary>
    /// The variant this frame belongs to. Null means the frame is intentionally
    /// shared by every variant.
    /// </summary>
    internal string? VariantKey { get; private set; }

    internal Vector2 NudgeValue { get; private set; }

    internal Vector2? ScaleValue { get; private set; }

    internal float? CharacterAnchorX { get; private set; }

    internal float? CharacterAnchorY { get; private set; }

    internal float? FrameOffsetY { get; private set; }

    internal bool GroundToIdleValue { get; private set; }

    internal float GroundingExtraY { get; private set; }

    internal SpriteFrameDefinition Nudge(float x, float y)
    {
        NudgeValue = new Vector2(x, y);
        return this;
    }

    internal SpriteFrameDefinition Scale(float uniformScale) =>
        Scale(uniformScale, uniformScale);

    internal SpriteFrameDefinition Scale(float x, float y)
    {
        if (!float.IsFinite(x)
            || !float.IsFinite(y)
            || Mathf.IsZeroApprox(x)
            || Mathf.IsZeroApprox(y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                "Sprite frame scale components must be finite and non-zero.");
        }

        ScaleValue = new Vector2(x, y);
        return this;
    }

    internal SpriteFrameDefinition ForVariant(string variantKey)
    {
        if (string.IsNullOrWhiteSpace(variantKey))
        {
            throw new ArgumentException(
                "Sprite frame variant names cannot be empty.",
                nameof(variantKey));
        }

        if (VariantKey != null
            && !string.Equals(
                VariantKey,
                variantKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Sprite frame '{Key}' is already owned by variant "
                + $"'{VariantKey}'.");
        }

        VariantKey = variantKey;
        return this;
    }

    internal SpriteFrameDefinition AnchorX(float x)
    {
        CharacterAnchorX = x;
        return this;
    }

    internal SpriteFrameDefinition AnchorY(float y)
    {
        CharacterAnchorY = y;
        return this;
    }

    internal SpriteFrameDefinition Anchor(float x, float y) =>
        AnchorX(x).AnchorY(y);

    internal SpriteFrameDefinition OffsetY(float y)
    {
        FrameOffsetY = y;
        return this;
    }

    internal SpriteFrameDefinition GroundToIdle(float extraY = 0f)
    {
        GroundToIdleValue = true;
        GroundingExtraY = extraY;
        return this;
    }
}

internal sealed class SpriteAnimationDefinition
{
    internal SpriteAnimationDefinition(
        SpriteVisualTriggerType triggerType,
        IReadOnlyList<string> frameKeys,
        IReadOnlyList<string> triggerNames)
    {
        TriggerType = triggerType;
        FrameKeys = frameKeys;
        TriggerNames = triggerNames;
    }

    internal SpriteVisualTriggerType TriggerType { get; }

    internal IReadOnlyList<string> FrameKeys { get; }

    internal IReadOnlyList<string> TriggerNames { get; }

    internal string? VariantKey { get; private set; }

    internal string? ResultVariantKey { get; private set; }

    internal SpriteAnimationFrameSelection FrameSelection { get; private set; }

    internal float DisplayDurationSeconds { get; set; }

    internal float LungeDurationSeconds { get; set; }

    internal float HoldDurationSeconds { get; set; }

    internal float ReturnDurationSeconds { get; set; }

    internal SpriteAnimationDefinition ForVariant(string variantKey)
    {
        VariantKey = variantKey;
        return this;
    }

    internal SpriteAnimationDefinition SwitchToVariant(string variantKey)
    {
        ResultVariantKey = variantKey;
        return this;
    }

    internal SpriteAnimationDefinition Cycle()
    {
        FrameSelection = SpriteAnimationFrameSelection.Cycle;
        return this;
    }

    internal SpriteAnimationDefinition Random()
    {
        FrameSelection = SpriteAnimationFrameSelection.Random;
        return this;
    }
}

/// <summary>
/// Single source of truth for a non-Spine monster's sprite resources, variants,
/// per-frame placement, and trigger animations.
/// </summary>
internal sealed class SpriteVisualProfile
{
    internal const string DefaultVariantKey = "default";

    private readonly Dictionary<string, SpriteVisualVariantDefinition> _variants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpriteFrameDefinition> _frames =
        new(StringComparer.Ordinal);
    private readonly List<SpriteAnimationDefinition> _animations = [];
    private IReadOnlyList<string>? _assetPaths;

    internal string InitialVariantKey { get; private set; } =
        DefaultVariantKey;

    internal SpriteVisualAnchorMode AnchorMode { get; private set; } =
        SpriteVisualAnchorMode.VisibleBottomCenter;

    internal float? ManualBottomAnchor { get; private set; }

    internal IReadOnlyDictionary<string, SpriteVisualVariantDefinition>
        Variants => _variants;

    internal IReadOnlyDictionary<string, SpriteFrameDefinition> Frames =>
        _frames;

    internal IReadOnlyList<SpriteAnimationDefinition> Animations =>
        _animations;

    internal string DefaultIdleTexturePath =>
        _variants.TryGetValue(
            InitialVariantKey,
            out SpriteVisualVariantDefinition? variant)
            ? variant.IdleTexturePath
            : throw new InvalidOperationException(
                $"Initial sprite variant '{InitialVariantKey}' is not registered.");

    internal IReadOnlyList<string> AssetPaths =>
        _assetPaths ??= _variants.Values
            .Select(static variant => variant.IdleTexturePath)
            .Concat(_frames.Values.Select(static frame => frame.TexturePath))
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    internal SpriteVisualVariantDefinition Variant(
        string key,
        string idleTexturePath)
    {
        EnsureName(key, nameof(key));
        EnsurePath(idleTexturePath, nameof(idleTexturePath));
        if (_variants.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Duplicate sprite variant key '{key}'.");
        }

        string idleFrameKey = $"@idle:{key}";
        var variant = new SpriteVisualVariantDefinition(
            key,
            idleFrameKey,
            idleTexturePath);
        _variants.Add(key, variant);
        _frames.Add(
            idleFrameKey,
            new SpriteFrameDefinition(idleFrameKey, idleTexturePath)
                .ForVariant(key));
        _assetPaths = null;
        return variant;
    }

    internal SpriteFrameDefinition Frame(
        string key,
        string texturePath)
    {
        EnsureName(key, nameof(key));
        EnsurePath(texturePath, nameof(texturePath));
        if (_frames.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Duplicate sprite frame key '{key}'.");
        }

        var frame = new SpriteFrameDefinition(key, texturePath);
        _frames.Add(key, frame);
        _assetPaths = null;
        return frame;
    }

    internal SpriteAnimationDefinition Swap(
        string frameKey,
        float displayDurationSeconds,
        params string[] triggerNames) =>
        Swap(
            [frameKey],
            displayDurationSeconds,
            triggerNames);

    internal SpriteAnimationDefinition Swap(
        IReadOnlyList<string> frameKeys,
        float displayDurationSeconds,
        params string[] triggerNames)
    {
        var animation = AddAnimation(
            SpriteVisualTriggerType.TimedSwap,
            frameKeys,
            triggerNames);
        animation.DisplayDurationSeconds = PositiveDuration(
            displayDurationSeconds,
            nameof(displayDurationSeconds));
        return animation;
    }

    internal SpriteAnimationDefinition Sequence(
        IReadOnlyList<string> frameKeys,
        float displayDurationSeconds,
        params string[] triggerNames)
    {
        var animation = AddAnimation(
            SpriteVisualTriggerType.TimedSequence,
            frameKeys,
            triggerNames);
        animation.DisplayDurationSeconds = PositiveDuration(
            displayDurationSeconds,
            nameof(displayDurationSeconds));
        return animation;
    }

    internal SpriteAnimationDefinition Lunge(
        string frameKey,
        float lungeDurationSeconds,
        float holdDurationSeconds,
        float returnDurationSeconds,
        params string[] triggerNames) =>
        Lunge(
            [frameKey],
            lungeDurationSeconds,
            holdDurationSeconds,
            returnDurationSeconds,
            triggerNames);

    internal SpriteAnimationDefinition Lunge(
        IReadOnlyList<string> frameKeys,
        float lungeDurationSeconds,
        float holdDurationSeconds,
        float returnDurationSeconds,
        params string[] triggerNames)
    {
        var animation = AddAnimation(
            SpriteVisualTriggerType.LungeAttack,
            frameKeys,
            triggerNames);
        animation.LungeDurationSeconds = NonNegativeDuration(
            lungeDurationSeconds,
            nameof(lungeDurationSeconds));
        animation.HoldDurationSeconds = NonNegativeDuration(
            holdDurationSeconds,
            nameof(holdDurationSeconds));
        animation.ReturnDurationSeconds = NonNegativeDuration(
            returnDurationSeconds,
            nameof(returnDurationSeconds));
        float totalDuration = animation.LungeDurationSeconds
            + animation.HoldDurationSeconds
            + animation.ReturnDurationSeconds;
        if (!float.IsFinite(totalDuration) || totalDuration <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lungeDurationSeconds),
                "A sprite lunge must have a positive total duration.");
        }

        return animation;
    }

    internal SpriteVisualProfile InitialVariant(string variantKey)
    {
        EnsureName(variantKey, nameof(variantKey));
        InitialVariantKey = variantKey;
        return this;
    }

    internal SpriteVisualProfile Centered()
    {
        AnchorMode = SpriteVisualAnchorMode.Center;
        ManualBottomAnchor = null;
        return this;
    }

    internal SpriteVisualProfile ImageBottom()
    {
        AnchorMode = SpriteVisualAnchorMode.ImageBottomCenter;
        ManualBottomAnchor = null;
        return this;
    }

    internal SpriteVisualProfile ManualBottom(float bottom)
    {
        AnchorMode = SpriteVisualAnchorMode.ManualBottomCenter;
        ManualBottomAnchor = Math.Max(0f, bottom);
        return this;
    }

    internal void Validate()
    {
        if (_variants.Count == 0)
        {
            throw new InvalidOperationException(
                "A sprite visual profile requires at least one variant.");
        }

        if (!_variants.ContainsKey(InitialVariantKey))
        {
            throw new InvalidOperationException(
                $"Initial sprite variant '{InitialVariantKey}' is not registered.");
        }

        foreach (SpriteFrameDefinition frame in _frames.Values)
        {
            if (frame.VariantKey != null
                && !_variants.ContainsKey(frame.VariantKey))
            {
                throw new InvalidOperationException(
                    $"Sprite frame '{frame.Key}' references unknown variant "
                    + $"'{frame.VariantKey}'.");
            }
        }

        var triggerKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpriteAnimationDefinition animation in _animations)
        {
            ValidateAnimationDuration(animation);

            if (animation.VariantKey != null
                && !_variants.ContainsKey(animation.VariantKey))
            {
                throw new InvalidOperationException(
                    $"Sprite animation references unknown variant '{animation.VariantKey}'.");
            }

            if (animation.ResultVariantKey != null
                && !_variants.ContainsKey(animation.ResultVariantKey))
            {
                throw new InvalidOperationException(
                    "Sprite animation switches to unknown variant "
                    + $"'{animation.ResultVariantKey}'.");
            }

            foreach (string frameKey in animation.FrameKeys)
            {
                if (!_frames.TryGetValue(
                        frameKey,
                        out SpriteFrameDefinition? frame))
                {
                    throw new InvalidOperationException(
                        $"Sprite animation references unknown frame '{frameKey}'.");
                }

                if (animation.VariantKey != null
                    && !string.Equals(
                        animation.VariantKey,
                        frame.VariantKey,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Sprite animation for variant '{animation.VariantKey}' "
                        + $"cannot reference frame '{frameKey}' owned by "
                        + $"variant '{frame.VariantKey ?? "*"}'.");
                }
            }

            foreach (string triggerName in animation.TriggerNames)
            {
                string lookupKey =
                    $"{animation.VariantKey ?? "*"}\u001f{triggerName}";
                if (!triggerKeys.Add(lookupKey))
                {
                    throw new InvalidOperationException(
                        $"Duplicate sprite trigger '{triggerName}' for variant "
                        + $"'{animation.VariantKey ?? "*"}'.");
                }
            }
        }
    }

    private static void ValidateAnimationDuration(
        SpriteAnimationDefinition animation)
    {
        if (animation.TriggerType != SpriteVisualTriggerType.LungeAttack)
        {
            PositiveDuration(
                animation.DisplayDurationSeconds,
                nameof(animation.DisplayDurationSeconds));
            return;
        }

        float lungeDuration = NonNegativeDuration(
            animation.LungeDurationSeconds,
            nameof(animation.LungeDurationSeconds));
        float holdDuration = NonNegativeDuration(
            animation.HoldDurationSeconds,
            nameof(animation.HoldDurationSeconds));
        float returnDuration = NonNegativeDuration(
            animation.ReturnDurationSeconds,
            nameof(animation.ReturnDurationSeconds));
        float totalDuration =
            lungeDuration + holdDuration + returnDuration;
        if (!float.IsFinite(totalDuration) || totalDuration <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(animation),
                totalDuration,
                "A sprite lunge must have a finite positive total duration.");
        }
    }

    private SpriteAnimationDefinition AddAnimation(
        SpriteVisualTriggerType triggerType,
        IReadOnlyList<string> frameKeys,
        IReadOnlyList<string> triggerNames)
    {
        if (frameKeys.Count == 0)
        {
            throw new ArgumentException(
                "A sprite animation requires at least one frame.",
                nameof(frameKeys));
        }

        if (triggerNames.Count == 0)
        {
            throw new ArgumentException(
                "A sprite animation requires at least one trigger.",
                nameof(triggerNames));
        }

        foreach (string frameKey in frameKeys)
        {
            EnsureName(frameKey, nameof(frameKeys));
        }
        foreach (string triggerName in triggerNames)
        {
            EnsureName(triggerName, nameof(triggerNames));
        }

        var animation = new SpriteAnimationDefinition(
            triggerType,
            frameKeys.ToArray(),
            triggerNames.ToArray());
        _animations.Add(animation);
        return animation;
    }

    private static float PositiveDuration(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Sprite animation duration must be finite and positive.");
        }

        return value;
    }

    private static float NonNegativeDuration(
        float value,
        string parameterName)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Sprite animation duration must be finite and non-negative.");
        }

        return value;
    }

    private static void EnsureName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Sprite profile names cannot be empty.",
                parameterName);
        }
    }

    private static void EnsurePath(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Sprite texture paths cannot be empty.",
                parameterName);
        }
    }
}

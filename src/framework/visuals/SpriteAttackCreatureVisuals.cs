using System;
using System.Linq;
using Godot;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.framework.visuals;

// ============================================================================
// 接口
// ============================================================================

/// <summary>
/// 由 <c>NonSpineAnimationTriggerBridgePatch</c> 调用。
/// 当游戏对怪物调用 SetAnimationTrigger("Attack") 且怪物没有 Spine 骨骼时，
/// 会转到这里来驱动静态贴图的视觉切换。
/// </summary>
internal interface INonSpineVisualTriggerHandler
{
    bool TryPlayTrigger(string triggerName);
}

/// <summary>
/// 预留接口：允许外部在下次攻击动画前预设一个 Lunge 偏移。
/// 目前基类实现为空，由子类按需覆盖。
/// </summary>
internal interface ITargetedAttackLungeVisuals
{
    void SetNextAttackLungeOffset(Vector2 offset);

    void ClearNextAttackLungeOffset();
}

internal interface IContinuousAttackVisuals
{
    void BeginContinuousAttackChain();

    void EndContinuousAttackChain();
}

// ============================================================================
// 动画类型 & 参数
// ============================================================================

/// <summary>
/// 非 Spine 怪物 Profile 支持的三种视觉触发器类型。
/// </summary>
public enum SpriteVisualTriggerType
{
    /// <summary>冲刺攻击：切到攻击贴图 → 等待 → 回待机</summary>
    LungeAttack,
    /// <summary>定时切换：切到攻击贴图 → 等 N 秒 → 回待机（最常用）</summary>
    TimedSwap,
    /// <summary>序列帧：多张贴图按帧率依次播放</summary>
    TimedSequence
}

/// <summary>
/// Profile 解析后的一次动画播放参数。
///
/// 两个偏移来源的优先级：
/// - DisplayOffsetProvider：回调函数，接收实际显示的贴图，返回偏移。
///   用于锚点对齐——攻击贴图和待机贴图尺寸不同时，自动算出偏移让角色不跳帧。
/// - EndPosition：固定偏移值（DisplayOffsetProvider 为 null 时生效）。
/// </summary>
public readonly record struct SpriteVisualTriggerSpec
{
    /// <summary>动画类型</summary>
    public required SpriteVisualTriggerType TriggerType { get; init; }

    /// <summary>
    /// 攻击贴图。对于 LungeAttack/TimedSwap，这就是唯一显示贴图。
    /// 对于 TimedSequence，这是第一帧；完整帧序列见 Frames。
    /// </summary>
    public required Texture2D Texture { get; init; }

    /// <summary>序列帧数组（仅 TimedSequence 使用）</summary>
    public IReadOnlyList<Texture2D>? Frames { get; init; }

    /// <summary>
    /// 锚点对齐回调：接收实际显示的贴图，返回应追加到攻击 Sprite Position 的偏移。
    /// 典型用法是传入一个调用 AlignAttackTextureAnchorToIdle 的 lambda / 方法引用。
    /// 非 null 时优先于 EndPosition。
    /// </summary>
    public Func<Texture2D, Vector2>? DisplayOffsetProvider { get; init; }

    /// <summary>Optional per-frame scale provider used by declarative profiles.</summary>
    public Func<Texture2D, Vector2>? DisplayScaleProvider { get; init; }

    /// <summary>
    /// LungeAttack: 冲刺终点位置（相对于 _attackPosition 的增量）。
    /// TimedSwap: 显示偏移（DisplayOffsetProvider 为 null 时生效）。
    /// </summary>
    public Vector2 EndPosition { get; init; }

    /// <summary>LungeAttack: 冲刺终点缩放。TimedSwap: 显示缩放（不为零时覆盖默认缩放）。</summary>
    public Vector2 EndScale { get; init; }

    /// <summary>LungeAttack 冲刺阶段时长（秒）</summary>
    public float LungeDurationSeconds { get; init; }

    /// <summary>LungeAttack 在终点停留时长（秒）</summary>
    public float HoldDurationSeconds { get; init; }

    /// <summary>LungeAttack 回退到待机时长（秒）</summary>
    public float ReturnDurationSeconds { get; init; }

    /// <summary>TimedSwap / TimedSequence 的总显示时长（秒）</summary>
    public float DisplayDurationSeconds { get; init; }
}

// ============================================================================
// 基类
// ============================================================================

/// <summary>
/// 静态贴图怪物视觉的抽象基类。
///
/// <pre>
/// NCreatureVisuals
/// └── MotionRoot (Node2D)
///     ├── Visuals (Sprite2D, 待机贴图)
///     └── AttackVisuals (Sprite2D, 攻击贴图)
/// </pre>
/// 子类只声明 <see cref="SpriteProfile"/>；基类在 <c>_Ready</c> 时校验并加载
/// Profile 中的路径，再按触发名执行换帧、序列或突进。逐帧缩放、偏移和角色
/// X 锚点全部保存在 Profile，不在子类中保存纹理字段或按图片宽度判断。
/// <pre>
/// 游戏引擎: NCreature.SetAnimationTrigger("Attack")
///   → NonSpineAnimationTriggerBridgePatch (检测无 Spine)
///   → visuals.TryPlayTrigger("Attack")
///   → SpriteVisualProfile 查找当前 Variant 的触发定义
///   → 基类根据 spec.TriggerType 分发到 PlayLungeAttack / PlayTimedSwap / PlayTimedSequence
///   → Tween 倒计时 → OnActiveTweenFinished → RestoreIdleState
/// </pre>
/// </summary>
public abstract partial class SpriteAttackCreatureVisuals
    : NCreatureVisuals,
      INonSpineVisualTriggerHandler,
      ITargetedAttackLungeVisuals,
      IContinuousAttackVisuals,
      IChaosIdleVisuals
{
    // ---- 核心节点 ----

    /// <summary>运动根节点，移动它 = 整体移动（待机+攻击一起动）</summary>
    private Node2D _motionRoot = null!;

    /// <summary>待机贴图 Sprite（平时显示）</summary>
    private Sprite2D _idleVisuals = null!;

    /// <summary>攻击贴图 Sprite（攻击/Hit 时显示，平时隐藏）</summary>
    private Sprite2D _attackVisuals = null!;

    // ---- 动画状态 ----

    /// <summary>当前正在运行的 Tween；null 表示空闲</summary>
    private Tween? _activeTween;
    private int _continuousAttackChainDepth;
    private bool _wasChaoed;

    // ---- 快照基准值（_Ready 时记录，RestoreIdleState 时恢复） ----

    private Vector2 _motionRootIdlePosition;
    private Vector2 _idlePosition;
    private Vector2 _idleScale;
    private Vector2 _attackPosition;
    private Vector2 _attackScale;
    private bool _idleVisible;

    // ---- 锚点模式 ----

    /// <summary>是否使用底边中心对齐（默认是正中心对齐）</summary>
    private bool _useBottomCenterAnchor;

    /// <summary>是否自动扫描贴图像素来找到最底部不透明像素行</summary>
    private bool _useVisibleBottomCenterAnchor;

    /// <summary>手动指定底边 Y 值（像素空间）</summary>
    private float? _manualBottomAnchor;

    /// <summary>贴图路径 → 可见底边 Y 的缓存，避免重复扫描像素</summary>
    private static readonly Dictionary<string, float> VisibleBottomByTexturePath = new();

    private readonly Dictionary<string, Texture2D> _profileTextures =
        new(StringComparer.Ordinal);
    private readonly Dictionary<SpriteAnimationDefinition, int>
        _profileAnimationCursors = new();
    private SpriteVisualProfile? _loadedProfile;
    private string? _currentVariantKey;

    internal virtual SpriteVisualProfile? SpriteProfile => null;

    internal string? CurrentSpriteVariantKey => _currentVariantKey;

    // ========================================================================
    // 生命周期
    // ========================================================================

    /// <summary>
    /// 子类覆盖 _Ready 时<b>必须</b>先调用 base._Ready()。
    /// 基类完成：获取节点 → 应用 Tuning → 快照基准值 → 恢复初始状态。
    /// </summary>
    public override void _Ready()
    {
        base._Ready();

        _motionRoot = GetNodeOrNull<Node2D>("%MotionRoot") ?? this;
        _idleVisuals = GetNodeOrNull<Sprite2D>("%Visuals")
            ?? throw new InvalidOperationException($"{GetType().Name} requires a %Visuals Sprite2D node.");
        _attackVisuals = GetNodeOrNull<Sprite2D>("%AttackVisuals")
            ?? throw new InvalidOperationException($"{GetType().Name} requires a %AttackVisuals Sprite2D node.");

        // 快照所有基准值——后续 RestoreIdleState 会回到这个状态
        _motionRootIdlePosition = _motionRoot.Position;
        _idlePosition = _idleVisuals.Position;
        _idleScale = _idleVisuals.Scale;
        _attackPosition = _attackVisuals.Position;
        _attackScale = _attackVisuals.Scale;
        _idleVisible = _idleVisuals.Visible;

        InitializeSpriteProfile();

        // 3. 确保初始状态：待机显示，攻击隐藏
        RestoreIdleState();

        // “动画效果”低档：换图时淡入淡出（高档由 Spine 身体接管，子类在这之后才建）
        if (AnimationEffects.CrossfadeEnabled)
        {
            SpriteCrossfade.Attach(this, _idleVisuals, _attackVisuals);
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        RefreshChaosIdle();
    }

    /// <summary>
    /// 节点从场景树移除时清理 Tween，防止访问已释放对象导致崩溃。
    /// </summary>
    public override void _ExitTree()
    {
        if (_activeTween != null && _activeTween.IsValid())
        {
            _activeTween.Kill();
        }
        _activeTween = null;
        _wasChaoed = false;
        base._ExitTree();
    }

    // ========================================================================
    // ITargetedAttackLungeVisuals（预留接口，空实现）
    // ========================================================================

    public void SetNextAttackLungeOffset(Vector2 offset)
    {
    }

    public void ClearNextAttackLungeOffset()
    {
    }

    public void BeginContinuousAttackChain()
    {
        _continuousAttackChainDepth++;
    }

    public void EndContinuousAttackChain()
    {
        if (_continuousAttackChainDepth <= 0)
        {
            return;
        }

        _continuousAttackChainDepth--;
        if (_continuousAttackChainDepth > 0)
        {
        }
    }

    // ========================================================================
    // INonSpineVisualTriggerHandler —— 动画触发入口
    // ========================================================================

    /// <summary>
    /// 游戏触发动画的主入口。由 <c>NonSpineAnimationTriggerBridgePatch</c> 调用。
    /// 子类只通过 <see cref="SpriteProfile"/> 声明触发。
    /// </summary>
    /// <param name="triggerName">游戏传来的 trigger 名称，如 "Attack", "Hit", "Fire" 等</param>
    /// <returns>true 表示成功处理；false 表示不认识这个 trigger，交给 AnimationPlayer 兜底</returns>
    public bool TryPlayTrigger(string triggerName)
    {
        BeforeResolveSpriteTrigger(triggerName);
        if (triggerName is "Idle" or "Hit"
            && MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this))
        {
            InterruptActiveAnimation();
            return true;
        }

        if (!TryGetProfileTriggerSpec(
                triggerName,
                out SpriteVisualTriggerSpec spec))
        {
            return false;
        }

        switch (spec.TriggerType)
        {
            case SpriteVisualTriggerType.LungeAttack:
                PlayLungeAttack(spec);
                return true;
            case SpriteVisualTriggerType.TimedSwap:
                PlayTimedSwap(spec);
                return true;
            case SpriteVisualTriggerType.TimedSequence:
                PlayTimedSequence(spec);
                return true;
            default:
                return false;
        }
    }

    private static Texture2D LoadRequiredTexture(string path)
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
        if (!GodotTextureSafety.IsValid(texture))
        {
            throw new InvalidOperationException($"Unable to load texture: {path}");
        }

        GodotTextureSafety.RegisterSourcePath(texture, path);
        return texture;
    }

    private void InitializeSpriteProfile()
    {
        SpriteVisualProfile? profile = SpriteProfile;
        if (profile == null)
        {
            return;
        }

        profile.Validate();
        _loadedProfile = profile;
        _profileTextures.Clear();
        _profileAnimationCursors.Clear();

        var texturesByPath = new Dictionary<string, Texture2D>(
            StringComparer.Ordinal);
        foreach (string assetPath in profile.AssetPaths)
        {
            if (!ResourceLoader.Exists(assetPath))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} sprite profile references a missing "
                    + $"resource: {assetPath}");
            }
        }

        foreach (SpriteFrameDefinition frame in profile.Frames.Values)
        {
            if (!texturesByPath.TryGetValue(
                    frame.TexturePath,
                    out Texture2D? texture))
            {
                texture = LoadRequiredTexture(frame.TexturePath);
                texturesByPath.Add(frame.TexturePath, texture);
            }

            _profileTextures.Add(frame.Key, texture);
        }

        switch (profile.AnchorMode)
        {
            case SpriteVisualAnchorMode.VisibleBottomCenter:
                UseVisibleBottomCenterSpriteAnchor();
                break;
            case SpriteVisualAnchorMode.ImageBottomCenter:
                UseBottomCenterSpriteAnchor();
                break;
            case SpriteVisualAnchorMode.ManualBottomCenter:
                UseManualBottomCenterSpriteAnchor(
                    profile.ManualBottomAnchor ?? 0f);
                break;
            case SpriteVisualAnchorMode.Center:
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(profile.AnchorMode),
                    profile.AnchorMode,
                    null);
        }

        SetSpriteVisualVariant(profile.InitialVariantKey);
    }

    protected void SetSpriteVisualVariant(string variantKey)
    {
        SpriteVisualProfile profile = _loadedProfile
            ?? throw new InvalidOperationException(
                $"{GetType().Name} has no loaded sprite profile.");
        if (!profile.Variants.TryGetValue(
                variantKey,
                out SpriteVisualVariantDefinition? variant))
        {
            throw new ArgumentOutOfRangeException(
                nameof(variantKey),
                variantKey,
                $"{GetType().Name} does not declare that sprite variant.");
        }

        _currentVariantKey = variantKey;
        SetIdleTexture(GetProfileTexture(variant.IdleFrameKey));

        if (variant.ScaleValue is { } scale)
        {
            if (variant.IdleOnlyLayout)
            {
                SetIdleVisualLayout(variant.Position, scale);
            }
            else
            {
                SetSpriteVisualLayout(variant.Position, scale);
            }
        }
        else if (variant.Position != Vector2.Zero)
        {
            _idlePosition = variant.Position;
            if (!variant.IdleOnlyLayout)
            {
                _attackPosition = variant.Position;
            }
        }

        SetSpriteFlipH(variant.FlipH);
        RestoreIdleState();
    }

    protected Texture2D GetProfileTexture(string frameKey)
    {
        if (_profileTextures.TryGetValue(
                frameKey,
                out Texture2D? texture))
        {
            return texture;
        }

        throw new ArgumentOutOfRangeException(
            nameof(frameKey),
            frameKey,
            $"{GetType().Name} does not declare that sprite frame.");
    }

    // 当前形态专属的动作优先，其次是不分形态的
    private static SpriteAnimationDefinition? FindProfileAnimation(
        SpriteVisualProfile profile,
        string variantKey,
        string triggerName) =>
        profile.Animations
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.VariantKey,
                    variantKey,
                    StringComparison.Ordinal)
                && candidate.TriggerNames.Contains(
                    triggerName,
                    StringComparer.Ordinal))
        ?? profile.Animations.FirstOrDefault(candidate =>
            candidate.VariantKey == null
            && candidate.TriggerNames.Contains(
                triggerName,
                StringComparer.Ordinal));

    /// <summary>
    /// 只做触发里声明的换形态（<c>SwitchToVariant</c>），不换图。Spine 身体接管触发时用：
    /// 动作交给 Spine，形态照样切换（伪王座的变形）。
    /// </summary>
    protected void ApplyTriggerVariantSwitch(string triggerName)
    {
        if (_loadedProfile is { } profile
            && _currentVariantKey is { } variantKey
            && !string.IsNullOrWhiteSpace(triggerName)
            && FindProfileAnimation(profile, variantKey, triggerName)?.ResultVariantKey is { } result
            && result != variantKey)
        {
            SetSpriteVisualVariant(result);
        }
    }

    private bool TryGetProfileTriggerSpec(
        string triggerName,
        out SpriteVisualTriggerSpec spec,
        bool forStaticPose = false)
    {
        SpriteVisualProfile? profile = _loadedProfile;
        string? variantKey = _currentVariantKey;
        if (profile == null
            || variantKey == null
            || string.IsNullOrWhiteSpace(triggerName))
        {
            spec = default;
            return false;
        }

        SpriteAnimationDefinition? animation = FindProfileAnimation(profile, variantKey, triggerName);
        if (animation == null)
        {
            spec = default;
            return false;
        }

        IReadOnlyList<string> frameKeys = animation.FrameKeys;
        string firstFrameKey;
        if (forStaticPose)
        {
            firstFrameKey = frameKeys[0];
        }
        else if (animation.FrameSelection == SpriteAnimationFrameSelection.Cycle)
        {
            int cursor = _profileAnimationCursors.TryGetValue(
                animation,
                out int previous)
                ? (previous + 1) % frameKeys.Count
                : 0;
            _profileAnimationCursors[animation] = cursor;
            firstFrameKey = frameKeys[cursor];
        }
        else if (animation.FrameSelection
                 == SpriteAnimationFrameSelection.Random)
        {
            firstFrameKey = frameKeys[Random.Shared.Next(frameKeys.Count)];
        }
        else
        {
            firstFrameKey = frameKeys[0];
        }

        SpriteFrameDefinition firstFrame = profile.Frames[firstFrameKey];
        Texture2D firstTexture = GetProfileTexture(firstFrameKey);
        SpriteVisualVariantDefinition variant =
            profile.Variants[variantKey];

        Func<Texture2D, Vector2>? offsetProvider =
            animation.TriggerType == SpriteVisualTriggerType.LungeAttack
            && firstFrame.CharacterAnchorX == null
            && firstFrame.CharacterAnchorY == null
            && variant.CharacterAnchorX == null
            && variant.CharacterAnchorY == null
            && !firstFrame.GroundToIdleValue
            && firstFrame.FrameOffsetY == null
                ? null
                : texture => ResolveProfileFrameOffset(
                    animation,
                    variant,
                    texture);
        Func<Texture2D, Vector2>? scaleProvider =
            texture => ResolveProfileFrameScale(animation, texture);

        IReadOnlyList<Texture2D>? frames =
            animation.TriggerType == SpriteVisualTriggerType.TimedSequence
                ? frameKeys
                    .Select(GetProfileTexture)
                    .ToArray()
                : null;
        spec = new SpriteVisualTriggerSpec
        {
            TriggerType = animation.TriggerType,
            Texture = firstTexture,
            Frames = frames,
            DisplayOffsetProvider = offsetProvider,
            DisplayScaleProvider = scaleProvider,
            EndPosition = firstFrame.NudgeValue,
            EndScale = firstFrame.ScaleValue ?? Vector2.Zero,
            DisplayDurationSeconds =
                animation.DisplayDurationSeconds,
            LungeDurationSeconds =
                animation.LungeDurationSeconds,
            HoldDurationSeconds =
                animation.HoldDurationSeconds,
            ReturnDurationSeconds =
                animation.ReturnDurationSeconds
        };
        if (!forStaticPose && animation.ResultVariantKey is { } resultVariantKey)
        {
            SetSpriteVisualVariant(resultVariantKey);
        }
        return true;
    }

    private Vector2 ResolveProfileFrameOffset(
        SpriteAnimationDefinition animation,
        SpriteVisualVariantDefinition variant,
        Texture2D texture)
    {
        SpriteFrameDefinition frame =
            ResolveProfileFrame(animation, texture);
        Vector2 offset =
            animation.TriggerType == SpriteVisualTriggerType.LungeAttack
                ? Vector2.Zero
                : frame.NudgeValue;
        offset.Y += frame.FrameOffsetY ?? 0f;
        Vector2 frameScale = frame.ScaleValue ?? _attackScale;
        Texture2D idleTexture = GetProfileTexture(variant.IdleFrameKey);
        if (frame.CharacterAnchorY != null
            || variant.CharacterAnchorY != null)
        {
            float attackAnchorY = frame.CharacterAnchorY
                ?? texture.GetHeight() * 0.5f;
            float idleAnchorY = variant.CharacterAnchorY
                ?? idleTexture.GetHeight() * 0.5f;
            offset.Y += AlignAttackTextureAnchorYToIdle(
                texture,
                attackAnchorY,
                idleTexture,
                idleAnchorY,
                frameScale.Y);
        }
        else if (frame.GroundToIdleValue)
        {
            offset.Y += AlignAttackTextureAnchorYToIdle(
                    texture,
                    GetVisibleBottom(texture, texture.GetSize().Y),
                    idleTexture,
                    GetVisibleBottom(idleTexture, idleTexture.GetSize().Y),
                    frameScale.Y)
                + frame.GroundingExtraY;
        }

        if (frame.CharacterAnchorX == null
            && variant.CharacterAnchorX == null)
        {
            return offset;
        }

        float attackAnchorX =
            frame.CharacterAnchorX
            ?? (frame.Key == variant.IdleFrameKey
                ? variant.CharacterAnchorX
                : null)
            ?? texture.GetWidth() * 0.5f;
        float idleAnchorX =
            variant.CharacterAnchorX
            ?? idleTexture.GetWidth() * 0.5f;
        return AlignAttackTextureAnchorToIdle(
                texture,
                attackAnchorX,
                idleAnchorX,
                frameScale.X)
            + offset;
    }

    private Vector2 ResolveProfileFrameScale(
        SpriteAnimationDefinition animation,
        Texture2D texture) =>
        ResolveProfileFrame(animation, texture).ScaleValue
        ?? Vector2.Zero;

    private SpriteFrameDefinition ResolveProfileFrame(
        SpriteAnimationDefinition animation,
        Texture2D texture)
    {
        SpriteVisualProfile profile = _loadedProfile!;
        foreach (string frameKey in animation.FrameKeys)
        {
            if (ReferenceEquals(GetProfileTexture(frameKey), texture))
            {
                return profile.Frames[frameKey];
            }
        }

        return profile.Frames[animation.FrameKeys[0]];
    }

    protected virtual void BeforeResolveSpriteTrigger(string triggerName)
    {
    }

    // ========================================================================
    // Profile 布局应用
    // ========================================================================

    /// <summary>
    /// 设定<b>仅待机贴图</b>的位置和缩放。
    /// 调用后自动 RestoreIdleState。
    /// 适合：攻击贴图使用 MonsterVisualsPatch 默认布局，只调整待机的场景。
    /// </summary>
    private void SetIdleVisualLayout(Vector2 idlePosition, Vector2 idleScale)
    {
        _idlePosition = idlePosition;
        _idleScale = idleScale;
        RestoreIdleState();
    }

    /// <summary>
    /// 同时设定<b>待机和攻击贴图</b>为相同的位置和缩放。
    /// 调用后自动 RestoreIdleState。
    /// 适合：攻击贴图和待机贴图基本一致，不需要分开调整的场景。
    /// </summary>
    private void SetSpriteVisualLayout(Vector2 position, Vector2 scale)
    {
        _idlePosition = position;
        _idleScale = scale;
        _attackPosition = position;
        _attackScale = scale;
        RestoreIdleState();
    }

    /// <summary>
    /// 运行时动态更换待机贴图。
    /// 适合有多个形态的怪物（如 CobaltScar 在 Cobalt/BigWolf/Shadow 之间切换）。
    /// 更换后会自动重新计算锚点。
    /// </summary>
    protected void SetIdleTexture(Texture2D texture)
    {
        if (_idleVisuals == null)
        {
            return;
        }

        if (!GodotTextureSafety.TrySetTexture(_idleVisuals, texture))
        {
            return;
        }

        ApplySpriteAnchor(_idleVisuals);
    }

    /// <summary>
    /// 同时翻转待机和攻击贴图的水平方向。
    /// flipH=true 时贴图面向左侧（玩家方向）。
    /// </summary>
    protected void SetSpriteFlipH(bool flipH)
    {
        _idleVisuals.FlipH = flipH;
        _attackVisuals.FlipH = flipH;
    }

    // ========================================================================
    // 锚点系统 —— 对齐攻击贴图和待机贴图的角色位置
    // ========================================================================

    /// <summary>
    /// 将 Frame 的角色 X 锚点对齐到当前 Variant 的 idle 角色 X 锚点。
    /// </summary>
    private Vector2 AlignAttackTextureAnchorToIdle(
        Texture2D attackTexture,
        float attackAnchorX,
        float idleAnchorX,
        float attackScaleX)
    {
        // 贴图几何中心（像素空间）
        float idleCenterX = (_idleVisuals.Texture?.GetWidth() ?? 0f) * 0.5f;
        float attackCenterX = attackTexture.GetWidth() * 0.5f;

        // 翻转方向因子：FlipH 时贴图左右反转，偏移方向也跟着反转
        float idleFlip = _idleVisuals.FlipH ? -1f : 1f;
        float attackFlip = _attackVisuals.FlipH ? -1f : 1f;

        // 角色锚点相对于贴图中心的偏移（场景空间）
        // (anchorX - centerX): 贴图像素偏移
        // * scale.X: 转为场景坐标
        // * flip: 处理翻转方向
        float idleAnchorOffset = (idleAnchorX - idleCenterX) * _idleScale.X * idleFlip;
        float attackAnchorOffset = (attackAnchorX - attackCenterX) * attackScaleX * attackFlip;

        // 反推攻击贴图的 X 位置：使两个角色锚点在屏幕上重合
        //   idlePosition.X + idleAnchorOffset  =  屏幕上待机角色锚点的 X
        //   attackPosition.X + attackAnchorOffset + offset  = 屏幕上攻击角色锚点的 X
        //   令两者相等 → offset = idlePosition.X + idleAnchorOffset - attackPosition.X - attackAnchorOffset
        float alignedAttackX = _idlePosition.X + idleAnchorOffset - attackAnchorOffset;

        // 返回相对于攻击 Sprite 基准位置的额外偏移
        return new Vector2(alignedAttackX - _attackPosition.X, 0f);
    }

    private float AlignAttackTextureAnchorYToIdle(
        Texture2D attackTexture,
        float attackAnchorY,
        Texture2D idleTexture,
        float idleAnchorY,
        float attackScaleY)
    {
        float idleOriginY = ResolveTextureOriginY(idleTexture);
        float attackOriginY = ResolveTextureOriginY(attackTexture);
        float idleAnchorOffset = (idleAnchorY - idleOriginY) * _idleScale.Y;
        float attackAnchorOffset = (attackAnchorY - attackOriginY) * attackScaleY;
        return _idlePosition.Y + idleAnchorOffset
            - _attackPosition.Y - attackAnchorOffset;
    }

    private float ResolveTextureOriginY(Texture2D texture)
    {
        if (!_useBottomCenterAnchor)
        {
            return texture.GetHeight() * 0.5f;
        }

        if (_manualBottomAnchor is { } manualBottom)
        {
            return manualBottom;
        }

        return _useVisibleBottomCenterAnchor
            ? GetVisibleBottom(texture, texture.GetHeight())
            : texture.GetHeight();
    }

    /// <summary>
    /// 启用底边中心对齐模式。
    /// 默认情况下 Sprite 的 Position 是贴图的正中心。
    /// 调用此方法后改为：贴图底边中点对齐到 Position。
    ///
    /// <b>适合大多数 RPG 怪物</b>——怪物的"站立点"在贴图底部，底部对齐能让不同体型的怪物站在同一地平线上。
    ///
    /// 与 <see cref="UseVisibleBottomCenterSpriteAnchor"/> 的区别：
    /// 此方法使用整个贴图高度作为底边；如果贴图底部有大量透明区域，用 UseVisibleBottomCenter 更准确。
    /// </summary>
    private void UseBottomCenterSpriteAnchor()
    {
        _useBottomCenterAnchor = true;
        ApplySpriteAnchor(_idleVisuals);
        ApplySpriteAnchor(_attackVisuals);
    }

    /// <summary>
    /// 启用可见底边中心对齐（推荐）。
    /// 自动扫描贴图像素，找到最底部不透明度 &gt; 3% 的行作为底边。
    /// 结果按贴图路径缓存，不会重复扫描。
    ///
    /// <b>实际效果</b>：贴图的 Position 会对齐到"角色脚底"而非"PNG 文件底边"。
    /// </summary>
    private void UseVisibleBottomCenterSpriteAnchor()
    {
        _useVisibleBottomCenterAnchor = true;
        UseBottomCenterSpriteAnchor();
    }

    /// <summary>
    /// 手动指定底边对齐的 Y 值（贴图像素空间）。
    /// 一般不直接使用——优先用 UseVisibleBottomCenterSpriteAnchor() 自动检测。
    /// </summary>
    /// <param name="bottom">手动指定的底边 Y 值</param>
    private void UseManualBottomCenterSpriteAnchor(float bottom)
    {
        _manualBottomAnchor = Math.Max(0f, bottom);
        UseBottomCenterSpriteAnchor();
    }

    // ========================================================================
    // 动画播放 —— 基类内部实现，子类不需要直接调用
    // ========================================================================

    /// <summary>
    /// 播放 LungeAttack 动画：打断旧动画 → 显示攻击贴图 → 等待总时长 → 回待机。
    /// 当前实现为"切贴图 + 等待"，EndPosition/EndScale 的位移 Tween 有待后续增强。
    /// </summary>
    private void PlayLungeAttack(SpriteVisualTriggerSpec spec)
    {
        InterruptActiveAnimation();

        ShowAttackVisual(spec.Texture);
        Texture2D displayTexture = _attackVisuals.Texture ?? spec.Texture;
        if (spec.DisplayOffsetProvider?.Invoke(displayTexture) is { } displayOffset)
        {
            _attackVisuals.Position = _attackPosition + displayOffset;
        }
        Vector2 displayScale =
            spec.DisplayScaleProvider?.Invoke(displayTexture)
            ?? spec.EndScale;
        if (displayScale != Vector2.Zero)
        {
            _attackVisuals.Scale = displayScale;
        }

        float totalDuration = Math.Max(
            0f,
            spec.LungeDurationSeconds + spec.HoldDurationSeconds + spec.ReturnDurationSeconds);

        _activeTween = CreateTween();
        if (totalDuration <= 0f)
        {
            OnActiveTweenFinished();
            return;
        }

        _activeTween.TweenInterval(totalDuration);
        _activeTween.TweenCallback(Callable.From(OnActiveTweenFinished));
    }

    /// <summary>
    /// 播放 TimedSwap 动画：打断 → 显示攻击贴图（含偏移/缩放）→ 等待 → 回待机。
    /// </summary>
    private void PlayTimedSwap(SpriteVisualTriggerSpec spec)
    {
        InterruptActiveAnimation();

        ShowTimedVisual(spec.Texture, spec);

        _activeTween = CreateTween();
        _activeTween.TweenInterval(Math.Max(0f, spec.DisplayDurationSeconds));
        _activeTween.TweenCallback(Callable.From(OnActiveTweenFinished));
    }

    /// <summary>
    /// 播放 TimedSequence 动画：打断 → 逐帧显示 Frames → 回待机。
    /// 帧率 = Frames.Count / DisplayDurationSeconds，每帧均分时长。
    /// </summary>
    private void PlayTimedSequence(SpriteVisualTriggerSpec spec)
    {
        InterruptActiveAnimation();

        IReadOnlyList<Texture2D> frames = spec.Frames ?? [spec.Texture];
        if (frames.Count == 0 || !GodotTextureSafety.TryResolveTexture(frames[0], out _))
        {
            RestoreIdleState();
            return;
        }

        ShowTimedVisual(frames[0], spec);

        float displayDuration = Math.Max(0f, spec.DisplayDurationSeconds);
        if (displayDuration <= 0f)
        {
            OnActiveTweenFinished();
            return;
        }

        float frameDuration = displayDuration / frames.Count;
        _activeTween = CreateTween();
        for (int i = 1; i < frames.Count; i++)
        {
            Texture2D frame = frames[i];
            if (!GodotTextureSafety.TryResolveTexture(frame, out _))
            {
                continue;
            }

            _activeTween.TweenInterval(frameDuration);
            _activeTween.TweenCallback(Callable.From(() => ShowTimedVisual(frame, spec)));
        }

        _activeTween.TweenInterval(frameDuration);
        _activeTween.TweenCallback(Callable.From(OnActiveTweenFinished));
    }

    // ========================================================================
    // 内部辅助方法
    // ========================================================================

    /// <summary>
    /// 切换到攻击贴图：设置纹理 → 应用锚点 → 归位到基准位置和缩放 → 显示攻击 + 隐藏待机。
    /// 如果纹理无效，安全回退到 RestoreIdleState。
    /// </summary>
    private void ShowAttackVisual(Texture2D texture)
    {
        if (!GodotTextureSafety.TrySetTexture(_attackVisuals, texture))
        {
            RestoreIdleState();
            return;
        }

        ApplySpriteAnchor(_attackVisuals);
        _attackVisuals.Position = _attackPosition;
        _attackVisuals.Scale = _attackScale;
        _attackVisuals.Visible = true;
        _idleVisuals.Visible = false;
    }

    /// <summary>
    /// 显示定时视觉（TimedSwap / TimedSequence 共用）。
    /// 在 ShowAttackVisual 基础上额外应用：
    /// - DisplayOffsetProvider（优先，用于锚点对齐）
    /// - spec.EndPosition（DisplayOffsetProvider 为 null 时作为固定偏移）
    /// - spec.EndScale（不为零时覆盖默认缩放）
    /// </summary>
    private void ShowTimedVisual(Texture2D texture, SpriteVisualTriggerSpec spec)
    {
        ShowAttackVisual(texture);
        Texture2D displayTexture = _attackVisuals.Texture ?? texture;
        Vector2 displayOffset = spec.DisplayOffsetProvider?.Invoke(displayTexture) ?? spec.EndPosition;
        if (displayOffset != Vector2.Zero)
        {
            _attackVisuals.Position = _attackPosition + displayOffset;
        }
        Vector2 displayScale =
            spec.DisplayScaleProvider?.Invoke(displayTexture)
            ?? spec.EndScale;
        if (displayScale != Vector2.Zero)
        {
            _attackVisuals.Scale = displayScale;
        }
    }

    /// <summary>
    /// 打断正在播放的动画：Kill Tween → 恢复到待机状态。
    /// 在新动画开始之前调用，确保状态干净。
    /// </summary>
    private void InterruptActiveAnimation()
    {
        if (_activeTween != null && _activeTween.IsValid())
        {
            _activeTween.Kill();
        }
        _activeTween = null;
        RestoreIdleState();
    }

    /// <summary>
    /// Tween 自然结束时的回调。只需清空引用 + 恢复待机，不需要 Kill（Tween 已自己结束）。
    /// </summary>
    private void OnActiveTweenFinished()
    {
        _activeTween = null;
        RestoreIdleState();
    }

    /// <summary>
    /// 将所有节点恢复到 _Ready 快照的基准状态：
    /// MotionRoot 归位 → 待机贴图归位 + 显示 → 攻击贴图归位 + 隐藏。
    /// </summary>
    protected virtual void RestoreIdleState()
    {
        _motionRoot.Position = _motionRootIdlePosition;

        _idleVisuals.Position = _idlePosition;
        _idleVisuals.Scale = _idleScale;
        _idleVisuals.Visible = _idleVisible;

        _attackVisuals.Position = _attackPosition;
        _attackVisuals.Scale = _attackScale;
        _attackVisuals.Visible = false;

        if (MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this)
            && TryGetProfileTriggerSpec(
                "Hit",
                out SpriteVisualTriggerSpec spec,
                forStaticPose: true)
            && GodotTextureSafety.TryResolveTexture(spec.Texture, out _))
        {
            // 复用受击贴图的锚点和布局，保持静态姿态，不创建计时 Tween。
            ShowTimedVisual(spec.Texture, spec);
        }
    }

    public void RefreshChaosIdle()
    {
        if (_idleVisuals == null || !IsInstanceValid(_idleVisuals))
        {
            return;
        }

        bool isChaoed = MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this);
        if (_wasChaoed == isChaoed)
        {
            return;
        }

        _wasChaoed = isChaoed;
        if (GetParent() is NCreature { Entity.IsAlive: true })
        {
            BeforeResolveSpriteTrigger("Idle");
            InterruptActiveAnimation();
        }
    }

    // ========================================================================
    // Sprite 锚点计算
    // ========================================================================

    /// <summary>
    /// 根据当前锚点模式计算 Sprite 的 Offset 值。
    /// 默认模式（_useBottomCenterAnchor=false）：Godot 默认 Centered=true，不做任何处理。
    /// BottomCenter 模式：Centered=false，Offset 设置为 (-width/2, -bottom)，
    /// 使贴图底边中点对齐到 Sprite 的 Position。
    /// </summary>
    private void ApplySpriteAnchor(Sprite2D sprite)
    {
        if (!_useBottomCenterAnchor)
        {
            return;
        }

        sprite.Centered = false;
        Vector2 size = sprite.Texture?.GetSize() ?? Vector2.Zero;
        float bottom = _manualBottomAnchor
            ?? (_useVisibleBottomCenterAnchor
            ? GetVisibleBottom(sprite.Texture, size.Y)
            : size.Y);
        sprite.Offset = new Vector2(-size.X * 0.5f, -bottom);
    }

    /// <summary>
    /// 获取贴图的"可见底边"Y 坐标（贴图像素空间）。
    /// 带缓存：同一贴图路径只扫描一次。
    /// </summary>
    /// <param name="texture">贴图</param>
    /// <param name="fallbackHeight">扫描失败时的兜底值（通常为贴图高度）</param>
    protected static float GetVisibleBottom(Texture2D? texture, float fallbackHeight)
    {
        if (!GodotTextureSafety.IsValid(texture))
        {
            return fallbackHeight;
        }

        string path = texture.ResourcePath;
        if (!string.IsNullOrEmpty(path) && VisibleBottomByTexturePath.TryGetValue(path, out float cached))
        {
            return cached;
        }

        float bottom = ComputeVisibleBottom(texture, fallbackHeight);
        if (!string.IsNullOrEmpty(path))
        {
            VisibleBottomByTexturePath[path] = bottom;
        }

        return bottom;
    }

    /// <summary>
    /// 从下往上逐行扫描贴图像素，找到第一个不透明像素（alpha &gt; 0.03）的行号。
    /// 返回值为 y+1（因为 Godot 的 Y 轴向下，Offset 需要取反）。
    /// </summary>
    private static float ComputeVisibleBottom(Texture2D texture, float fallbackHeight)
    {
        Image? image = texture.GetImage();
        if (image == null || image.IsEmpty())
        {
            return fallbackHeight;
        }

        for (int y = image.GetHeight() - 1; y >= 0; y--)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A > 0.03f)
                {
                    return y + 1f;
                }
            }
        }

        return fallbackHeight;
    }
}

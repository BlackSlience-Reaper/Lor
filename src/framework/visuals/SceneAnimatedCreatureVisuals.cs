using System;
using Godot;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.framework.visuals;

/// <summary>
/// C# runtime host for a scriptless creature-visual scene. The scene owns
/// editable nodes and AnimationPlayer tracks; this class only routes combat
/// triggers and restores the current phase's idle animation.
/// 给了 <see cref="SpineSpec"/> 且加载成功时，由 Spine 身体接管全部触发：场景的两张 Sprite2D 藏起来，
/// AnimationPlayer 不再播放，场景动画只在 Spine 加载失败时兜底。
/// </summary>
internal abstract partial class SceneAnimatedCreatureVisuals
    : NCreatureVisuals,
      INonSpineVisualTriggerHandler,
      IContinuousAttackVisuals,
      IChaosIdleVisuals
{
    private const string IdleTrigger = "Idle";

    private AnimationPlayer? _animationPlayer;
    private Sprite2D? _idleVisuals;
    private Sprite2D? _attackVisuals;
    private int _continuousAttackChainDepth;
    private bool _wasChaoed;
    private string? _heldChaosAnimation;
    private RuntimeSpineBody? _spine;
    // 每份骨架只建一次：骨架原点按建立时那个动画库的待机贴图对齐，各库的待机贴图不同，所以第一次换到该库时才建
    private readonly Dictionary<RuntimeSpineBody.Spec, RuntimeSpineBody?> _spineBodies = new();
    private RuntimeSpineBody.Spec? _activeSpineSpec;
    private bool _spineReady;

    protected abstract string ResolveCurrentAnimationLibrary();

    /// <summary>
    /// 原版分层生成的 Spine 身体（见 <see cref="LayeredBossSpine"/>）；为 null 时只用场景动画。
    /// 动画里换姿势的时刻要和动画契约的命中时刻一致，怪物代码按契约结算伤害。
    /// </summary>
    internal virtual RuntimeSpineBody.Spec? SpineSpec => null;

    /// <summary>
    /// 当前动画库用的骨架；按形态换动画库的外观（自然层）每个库一副，默认都用 <see cref="SpineSpec"/>。
    /// 返回 null 的库只用场景动画。
    /// </summary>
    internal virtual RuntimeSpineBody.Spec? SpineSpecFor(string library) => SpineSpec;

    /// <summary>出场时先读进缓存的全部骨架；按形态换动画库的外观要列出各库的，战斗中换形态才不会现读。</summary>
    internal virtual IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        SpineSpecFor(ResolveCurrentAnimationLibrary()) is { } spec ? [spec] : [];

    protected virtual string NormalizeTriggerName(string triggerName) =>
        triggerName;

    internal AnimationPlayer AnimationPlayer => _animationPlayer
        ?? throw new InvalidOperationException(
            $"{GetType().Name} AnimationPlayer is not ready.");

    internal string CurrentAnimationName =>
        _animationPlayer?.CurrentAnimation ?? string.Empty;

    public override void _Ready()
    {
        base._Ready();

        _animationPlayer = GetNodeOrNull<AnimationPlayer>("%AnimationPlayer")
            ?? throw new InvalidOperationException(
                $"{GetType().Name} requires an %AnimationPlayer node.");
        _idleVisuals = GetNodeOrNull<Sprite2D>("%Visuals")
            ?? throw new InvalidOperationException(
                $"{GetType().Name} requires a %Visuals Sprite2D node.");
        _attackVisuals = GetNodeOrNull<Sprite2D>("%AttackVisuals")
            ?? throw new InvalidOperationException(
                $"{GetType().Name} requires an %AttackVisuals Sprite2D node.");
        _animationPlayer.AnimationFinished += OnAnimationFinished;

        if (!PlayCurrentIdle())
        {
            // The resolved phase library can be missing at runtime (for
            // example when a texture referenced by the library failed to
            // import, so the whole AnimationLibrary loaded as null). Never
            // rest in the scene's editor RESET pose: force the idle sprite
            // layout in code so the creature's default state is always its
            // idle instead of the attack pose.
            ApplyFallbackIdlePose();
            Log.Warn(
                $"[LibraryOfRuina] {GetType().Name} could not play its current "
                + $"Idle animation '{ResolveQualifiedAnimationName(IdleTrigger)}'; "
                + "falling back to the code-built idle pose.");
        }

        _spineReady = true;
        foreach (RuntimeSpineBody.Spec spec in AllSpineSpecs)
        {
            RuntimeSpineBody.Preload(spec);
        }

        SyncSpineBody();
    }

    // 每帧和每次触发前按当前动画库选骨架，换库（换形态）时切过去；没有骨架或加载失败的库退回场景动画。
    private void SyncSpineBody()
    {
        if (!_spineReady || _idleVisuals == null || _animationPlayer == null)
        {
            return;
        }

        RuntimeSpineBody.Spec? spec = SpineSpecFor(ResolveCurrentAnimationLibrary());
        if (ReferenceEquals(spec, _activeSpineSpec))
        {
            return;
        }

        _activeSpineSpec = spec;
        RuntimeSpineBody? body = null;
        if (spec != null && !_spineBodies.TryGetValue(spec, out body))
        {
            // 骨架原点对齐待机贴图的底边中点，所以先把这个库的待机动画（贴图、位置、缩放）落到 %Visuals 上再建
            body = PlayAnimation(ResolveQualifiedAnimationName(IdleTrigger))
                ? RuntimeSpineBody.TryCreate(_idleVisuals, spec)
                : null;
            _spineBodies[spec] = body;
        }
        else
        {
            body?.Play(IdleTrigger, holdHurtPose: false);
        }

        foreach (RuntimeSpineBody? other in _spineBodies.Values)
        {
            if (other != null)
            {
                other.Visible = other == body;
                other.ProcessMode = other == body ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            }
        }

        _spine = body;
        if (body == null)
        {
            PlayCurrentIdle();
            return;
        }

        _animationPlayer.Stop();
        _heldChaosAnimation = null;
        _idleVisuals.Visible = false;
        _attackVisuals!.Visible = false;
        _wasChaoed = MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this);
        body.SyncHoldHurt(_wasChaoed);
    }

    /// <summary>死亡补丁转来的 "Dead"，见 <c>SpineSpriteDeathAnimPatch</c>；没有 Spine 身体时不做事。</summary>
    internal void PlayDeath() => _spine?.Play("Dead", holdHurtPose: false);

    /// <summary>当前形态挂着 Spine 身体（死亡时长见 <see cref="AnimationEffects.DeathLength"/>）。</summary>
    internal bool HasSpineBody => _spine != null;

    public override void _Process(double delta)
    {
        base._Process(delta);
        SyncSpineBody();
        RefreshChaosIdle();
    }

    public override void _ExitTree()
    {
        if (_animationPlayer != null
            && IsInstanceValid(_animationPlayer))
        {
            _animationPlayer.AnimationFinished -= OnAnimationFinished;
        }

        _animationPlayer = null;
        _idleVisuals = null;
        _attackVisuals = null;
        _continuousAttackChainDepth = 0;
        _wasChaoed = false;
        _heldChaosAnimation = null;
        _spine = null;
        _spineBodies.Clear();
        _activeSpineSpec = null;
        _spineReady = false;
        base._ExitTree();
    }

    public bool TryPlayTrigger(string triggerName)
    {
        if (string.IsNullOrWhiteSpace(triggerName))
        {
            return false;
        }

        string normalizedTrigger = NormalizeTriggerName(triggerName);
        if (string.IsNullOrWhiteSpace(normalizedTrigger))
        {
            return false;
        }

        SyncSpineBody();
        if (_spine != null)
        {
            // Spine 不认识的触发也算处理过：否则触发桥会退回 AnimationPlayer，把藏起来的贴图又显示出来
            _spine.Play(normalizedTrigger, MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this));
            return true;
        }

        if (normalizedTrigger is IdleTrigger or "Hit"
            && TryHoldChaosHitPose())
        {
            return true;
        }

        string animationName = ResolveQualifiedAnimationName(
            normalizedTrigger);
        return PlayAnimation(animationName);
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
    }

    internal string ResolveQualifiedAnimationName(string triggerName) =>
        $"{ResolveCurrentAnimationLibrary()}/{triggerName}";

    private bool PlayCurrentIdle() =>
        TryHoldChaosHitPose()
        || PlayAnimation(ResolveQualifiedAnimationName(IdleTrigger));

    public void RefreshChaosIdle()
    {
        if (_animationPlayer == null || !IsInstanceValid(_animationPlayer))
        {
            return;
        }

        if (_spine != null)
        {
            bool chaoed = MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this);
            if (_wasChaoed != chaoed)
            {
                _wasChaoed = chaoed;
                _spine.SyncHoldHurt(chaoed);
            }

            return;
        }

        bool isChaoed = MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this);
        bool formChanged = _heldChaosAnimation != null
            && _heldChaosAnimation != ResolveQualifiedAnimationName("Hit");
        if (_wasChaoed == isChaoed && !formChanged)
        {
            return;
        }

        _wasChaoed = isChaoed;
        if (GetParent() is NCreature { Entity.IsAlive: true })
        {
            PlayCurrentIdle();
        }
    }

    private bool TryHoldChaosHitPose()
    {
        if (!MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this))
        {
            return false;
        }

        string hitAnimation = ResolveQualifiedAnimationName("Hit");
        if (_heldChaosAnimation == hitAnimation)
        {
            return true;
        }

        if (!PlayAnimation(hitAnimation))
        {
            return false;
        }

        // 受击首帧沿用场景的贴图、位置和缩放，暂停后不会回到普通 Idle。
        _animationPlayer!.Pause();
        _heldChaosAnimation = hitAnimation;
        return true;
    }

    private void ApplyFallbackIdlePose()
    {
        _animationPlayer?.Stop();
        if (_idleVisuals != null
            && IsInstanceValid(_idleVisuals))
        {
            _idleVisuals.Visible = true;
        }

        if (_attackVisuals != null
            && IsInstanceValid(_attackVisuals))
        {
            _attackVisuals.Visible = false;
        }
    }

    private bool PlayAnimation(string animationName)
    {
        if (_animationPlayer == null
            || !IsInstanceValid(_animationPlayer)
            || !_animationPlayer.HasAnimation(animationName))
        {
            return false;
        }

        // Stop first so a new combat trigger cleanly interrupts the old track.
        // Every action owns its time-zero state and its completion callback
        // always returns to the phase selected by the model at that moment.
        _animationPlayer.Stop();
        _heldChaosAnimation = null;
        _animationPlayer.Play(animationName);
        _animationPlayer.Advance(0d);
        return true;
    }

    private void OnAnimationFinished(StringName animationName)
    {
        string idleAnimation = ResolveQualifiedAnimationName(IdleTrigger);
        if (!string.Equals(
                animationName.ToString(),
                idleAnimation,
                StringComparison.Ordinal))
        {
            // Every action is transient, including one segment of a
            // continuous attack.  A following trigger may interrupt it at
            // any point; natural completion always resolves the model's
            // current phase/stance again and returns to that Idle.
            PlayCurrentIdle();
        }
    }
}

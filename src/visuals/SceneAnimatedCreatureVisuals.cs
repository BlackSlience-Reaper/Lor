using System;
using Godot;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals;

/// <summary>
/// C# runtime host for a scriptless creature-visual scene. The scene owns
/// editable nodes and AnimationPlayer tracks; this class only routes combat
/// triggers and restores the current phase's idle animation.
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

    protected abstract string ResolveCurrentAnimationLibrary();

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
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
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

using System;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.PhilosophyFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.PhilosophyFloorLiberation;

/// <summary>
/// Non-Spine recreation of the original Apocalypse Bird E.G.O. appearance.
/// Every state was composited from the same Unity prefab root. The per-state
/// root anchors below preserve that shared origin despite the PNGs having
/// different cropped canvas sizes.
/// </summary>
public sealed partial class PhilosophyFloorTwilightCreatureVisuals
    : SpriteAttackCreatureVisuals, INonSpineVisualTriggerHandler
{
    private const string AnimationLibraryPath =
        "res://scenes/creature_visuals/philosophy_floor_twilight_animations.tres";

    internal const string DefaultTexturePath =
        "res://images/monsters/philosophy_floor_twilight/default.png";
    internal const string GuardTexturePath =
        "res://images/monsters/philosophy_floor_twilight/guard.png";
    internal const string HitTexturePath =
        "res://images/monsters/philosophy_floor_twilight/hit.png";
    internal const string SlashTexturePath =
        "res://images/monsters/philosophy_floor_twilight/j.png";
    internal const string PenetrateTexturePath =
        "res://images/monsters/philosophy_floor_twilight/z.png";
    internal const string ForestLightTexturePath =
        "res://images/monsters/philosophy_floor_twilight/f.png";
    internal const string BigEyeTexturePath =
        "res://images/monsters/philosophy_floor_twilight/s1.png";
    internal const string PunishmentTexturePath =
        "res://images/monsters/philosophy_floor_twilight/s2.png";
    internal const string PunishmentFollowupTexturePath =
        "res://images/monsters/philosophy_floor_twilight/s3.png";
    internal const string JudgementTexturePath =
        "res://images/monsters/philosophy_floor_twilight/s4.png";
    internal const string PeaceTexturePath =
        "res://images/monsters/philosophy_floor_twilight/s5.png";

    // Scene scale used by the catalog/static QA scene. Unity-root Y alignment
    // is represented as a state-specific scene-space offset.
    private const float VisualScale = 0.42f;
    private const float DefaultTextureWidth = 1513f;
    private const float DefaultRootAnchorX = 465f;
    private const float DefaultRootFromCenterY = 745f;
    internal const float HalfPixelsPerUnitFrameNormalization = 2f;
    private static readonly Vector2 AnimationBaselinePosition =
        new(0f, -316f);
    private static readonly Vector2 AnimationBaselineScale =
        new(VisualScale, VisualScale);
    internal static readonly Vector2 AnimationRootPivot =
        AnimationBaselinePosition + new Vector2(
            (DefaultRootAnchorX - DefaultTextureWidth * 0.5f) * VisualScale,
            DefaultRootFromCenterY * VisualScale);

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    [MonsterVisual(typeof(PhilosophyFloorTwilight))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -216f), new(0.45f, 0.45f), -315f, -624f, 315f, 12f, new(0f, -202f), new(36f, -508f))
    {
        TalkPos = new Vector2(0f, -540f),
        StateDisplayLiftY = 52f,
    };

    private AnimationPlayer? _animationPlayer;
    private Node2D? _attackFrameNormalizer;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        // The AnimationPlayer owns absolute child transforms and resets both
        // sprites to the Unity-composite baseline on every animation. Preserve
        // the position/scale requested by MonsterVisualCatalog on MotionRoot so
        // its SpritePos/SpriteScale remain the actual tuning controls.
        Node2D motionRoot = GetNode<Node2D>("%MotionRoot");
        Sprite2D idleVisuals = GetNode<Sprite2D>("%Visuals");
        Sprite2D attackVisuals = GetNode<Sprite2D>("%AttackVisuals");
        _attackFrameNormalizer = EnsureAttackFrameNormalizer(
            motionRoot,
            attackVisuals);
        Vector2 requestedPosition = idleVisuals.Position;
        Vector2 requestedScale = idleVisuals.Scale;
        Vector2 rootScale = new(
            requestedScale.X / AnimationBaselineScale.X,
            requestedScale.Y / AnimationBaselineScale.Y);
        motionRoot.Scale = rootScale;
        motionRoot.Position = requestedPosition - new Vector2(
            AnimationBaselinePosition.X * rootScale.X,
            AnimationBaselinePosition.Y * rootScale.Y);
        idleVisuals.Position = AnimationBaselinePosition;
        idleVisuals.Scale = AnimationBaselineScale;
        attackVisuals.Position = AnimationBaselinePosition;
        attackVisuals.Scale = AnimationBaselineScale;

        base._Ready();

        _animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (_animationPlayer == null)
        {
            AnimationLibrary? library =
                ResourceLoader.Load<AnimationLibrary>(AnimationLibraryPath);
            if (library == null)
            {
                throw new InvalidOperationException(
                    $"Unable to load Twilight animation library: {AnimationLibraryPath}");
            }

            _animationPlayer = new AnimationPlayer
            {
                Name = "AnimationPlayer"
            };
            _animationPlayer.AddAnimationLibrary(string.Empty, library);
            AddChild(_animationPlayer);
            _animationPlayer.Owner = this;
        }

        _animationPlayer.AnimationFinished += OnAnimationFinished;
        PlayAnimation("Default");
    }

    public override void _ExitTree()
    {
        if (_animationPlayer != null
            && IsInstanceValid(_animationPlayer))
        {
            _animationPlayer.AnimationFinished -= OnAnimationFinished;
            _animationPlayer.Stop();
        }
        _animationPlayer = null;
        _attackFrameNormalizer = null;
        base._ExitTree();
    }

    /// <summary>
    /// Reimplements the non-Spine trigger bridge for this visual only. The
    /// profile remains the resource/anchor catalog, while AnimationPlayer is
    /// the sole owner of action timing so the base Tween path cannot compete
    /// with scene tracks or leave a repeated action offset behind.
    /// </summary>
    public new bool TryPlayTrigger(string triggerName)
    {
        string? animationName = triggerName switch
        {
            "Idle" or "Default" or "Standing" => "Default",
            "G" or "Guard" or "Block" or "Defend" => "G",
            "Hit" or "Hurt" or "Damaged" => "Hit",
            "J" or "Slash" or "AttackSlash" => "J",
            "Z" or "Penetrate" or "AttackStrike" or "Attack" => "Z",
            "F" or "ForestLight" or "Cast" => "F",
            "S1" or "BrilliantEyes" or "EyeLaser" or "Watch" => "S1",
            "S2" or "Punishment" => "S2",
            "S3" or "PunishmentFollowup" => "S3",
            "S4" or "Judgement" or "Judgment" => "S4",
            "S5" or "Peace" or "PeaceSlam" => "S5",
            _ => null
        };

        return animationName != null
            ? PlayAnimation(animationName)
            : base.TryPlayTrigger(triggerName);
    }

    private bool PlayAnimation(string animationName)
    {
        bool holdChaosPose = animationName is "Default" or "Hit"
            && MonsterChaosIdleVisualPatch.ShouldHoldHitPose(this);
        if (holdChaosPose)
        {
            animationName = "Hit";
        }

        if (_animationPlayer == null
            || !IsInstanceValid(_animationPlayer)
            || !_animationPlayer.HasAnimation(animationName))
        {
            return false;
        }

        ApplyAttackFrameNormalization(animationName);
        _animationPlayer.Stop();
        _animationPlayer.Play(animationName);
        _animationPlayer.Advance(0d);
        if (holdChaosPose)
        {
            _animationPlayer.Pause();
        }

        return true;
    }

    protected override void RestoreIdleState()
    {
        // 薄暝由独立 AnimationPlayer 管理布局，混乱恢复也经由同一入口。
        if (!PlayAnimation("Default"))
        {
            base.RestoreIdleState();
        }
    }

    internal static float ResolveAttackFrameNormalization(
        string animationName) => animationName is "F" or "S3"
        ? HalfPixelsPerUnitFrameNormalization
        : 1f;

    internal static Vector2 ResolveAttackFrameNormalizerPosition(
        float normalization) =>
        AnimationRootPivot * (1f - normalization);

    private void ApplyAttackFrameNormalization(string animationName)
    {
        if (_attackFrameNormalizer == null
            || !IsInstanceValid(_attackFrameNormalizer))
        {
            return;
        }

        // Unity imports F and Small_S3 at 50 PPU; every other Apocalypse Bird
        // state uses 100 PPU. Scale only those two composites by the exact PPU
        // ratio and compensate around the shared Unity root so attacks do not
        // jump away from the monster's start position.
        float normalization = ResolveAttackFrameNormalization(animationName);
        _attackFrameNormalizer.Position =
            ResolveAttackFrameNormalizerPosition(normalization);
        _attackFrameNormalizer.Scale =
            Vector2.One * normalization;
    }

    private static Node2D EnsureAttackFrameNormalizer(
        Node2D motionRoot,
        Sprite2D attackVisuals)
    {
        Node2D? normalizer = motionRoot.GetNodeOrNull<Node2D>(
            "AttackFrameNormalizer");
        if (normalizer == null)
        {
            normalizer = new Node2D
            {
                Name = "AttackFrameNormalizer",
                UniqueNameInOwner = true
            };
            motionRoot.AddChild(normalizer);
            normalizer.Owner = motionRoot.Owner;
        }

        normalizer.Position = Vector2.Zero;
        normalizer.Scale = Vector2.One;
        if (attackVisuals.GetParent().GetInstanceId()
            != normalizer.GetInstanceId())
        {
            attackVisuals.Reparent(normalizer, keepGlobalTransform: false);
        }

        return normalizer;
    }

    private void OnAnimationFinished(StringName animationName)
    {
        if (animationName != "Default")
        {
            PlayAnimation("Default");
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                DefaultTexturePath)
            .AnchorX(DefaultRootAnchorX);

        AddRootAlignedFrame(profile, "guard", GuardTexturePath, 306f, 751.5f);
        AddRootAlignedFrame(profile, "hit", HitTexturePath, 277f, 786f);
        AddRootAlignedFrame(profile, "j", SlashTexturePath, 666f, 905.5f);
        AddRootAlignedFrame(profile, "z", PenetrateTexturePath, 604f, 931f);
        AddRootAlignedFrame(profile, "f", ForestLightTexturePath, 442f, 452.5f);
        AddRootAlignedFrame(profile, "s1", BigEyeTexturePath, 336f, 883.5f);
        AddRootAlignedFrame(profile, "s2", PunishmentTexturePath, 540f, 958f);
        AddRootAlignedFrame(profile, "s3", PunishmentFollowupTexturePath, 222f, 434f);
        AddRootAlignedFrame(profile, "s4", JudgementTexturePath, 402f, 750f);
        AddRootAlignedFrame(profile, "s5", PeaceTexturePath, 462f, 883.5f);

        profile.Swap("@idle:default", 0.01f, "Idle", "Default", "Standing");
        profile.Swap("guard", 0.46f, "G", "Guard", "Block", "Defend");
        profile.Swap("hit", 0.32f, "Hit", "Hurt", "Damaged");
        profile.Swap("j", 0.48f, "J", "Slash", "AttackSlash");
        profile.Swap("z", 0.48f, "Z", "Penetrate", "AttackStrike", "Attack");
        profile.Swap("f", 0.72f, "F", "ForestLight", "Cast");
        profile.Swap("s1", 0.82f, "S1", "BrilliantEyes", "EyeLaser", "Watch");
        profile.Swap("s2", 0.58f, "S2", "Punishment");
        profile.Swap("s3", 0.54f, "S3", "PunishmentFollowup");
        profile.Swap("s4", 0.88f, "S4", "Judgement", "Judgment");
        profile.Swap("s5", 1.06f, "S5", "Peace", "PeaceSlam");
        profile.Centered();
        return profile;
    }

    private static void AddRootAlignedFrame(
        SpriteVisualProfile profile,
        string key,
        string texturePath,
        float rootAnchorX,
        float rootFromCenterY)
    {
        profile.Frame(key, texturePath)
            .AnchorX(rootAnchorX)
            .Nudge(
                0f,
                (DefaultRootFromCenterY - rootFromCenterY) * VisualScale);
    }
}

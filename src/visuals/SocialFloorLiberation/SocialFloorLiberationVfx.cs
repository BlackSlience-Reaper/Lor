using Godot;
using LibraryOfRuina.audio;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.visuals.SocialFloorLiberation;

/// <summary>
/// Lightweight Godot playback for the original Wizard attack and
/// transformation textures. Source effects face left; every spawned sprite
/// is mirrored from the live attacker transform when it faces right.
/// </summary>
internal static class SocialFloorLiberationVfx
{
    internal const string FarPenetrateTexturePath =
        "res://images/vfx/social_floor_liberation/far_penetrate_hit.png";
    internal const string FarCrystalRiseTexturePath =
        "res://images/vfx/social_floor_liberation/far_hit_crystal_rise.png";
    internal const string AreaEffectTexturePath =
        "res://images/vfx/social_floor_liberation/crystal_area_effect.png";
    internal const string AreaFallTexturePath =
        "res://images/vfx/social_floor_liberation/crystal_area_fall.png";
    internal const string AreaEmbeddedTexturePath =
        "res://images/vfx/social_floor_liberation/crystal_area_embedded.png";
    internal const string AreaShockwaveTexturePath =
        "res://images/vfx/social_floor_liberation/crystal_area_shockwave.png";
    internal const string TransformationTexturePath =
        "res://images/vfx/social_floor_liberation/transformation.png";
    internal const string TransformationMaskTexturePath =
        "res://images/vfx/social_floor_liberation/transformation_mask.png";
    internal const string TransformationMask2TexturePath =
        "res://images/vfx/social_floor_liberation/transformation_mask2.png";

    internal const string AttackBoomSfxPath =
        "res://audio/sfx/social_floor_liberation/attack_boom.ogg";
    internal const string AttackUpSfxPath =
        "res://audio/sfx/social_floor_liberation/attack_up.ogg";
    internal const string CardMagicSfxPath =
        "res://audio/sfx/social_floor_liberation/card_magic.ogg";
    internal const string ChangeMagicSfxPath =
        "res://audio/sfx/social_floor_liberation/change_magic.ogg";
    internal const string StrongAttackStartSfxPath =
        "res://audio/sfx/social_floor_liberation/strong_attack_start.ogg";
    internal const string StrongAttackDownSfxPath =
        "res://audio/sfx/social_floor_liberation/strong_attack_down.ogg";
    internal const string StrongAttackFinishSfxPath =
        "res://audio/sfx/social_floor_liberation/strong_attack_finish.ogg";

    internal static readonly string[] AssetPaths =
    [
        FarPenetrateTexturePath,
        FarCrystalRiseTexturePath,
        AreaEffectTexturePath,
        AreaFallTexturePath,
        AreaEmbeddedTexturePath,
        AreaShockwaveTexturePath,
        TransformationTexturePath,
        TransformationMaskTexturePath,
        TransformationMask2TexturePath,
        AttackBoomSfxPath,
        AttackUpSfxPath,
        CardMagicSfxPath,
        ChangeMagicSfxPath,
        StrongAttackStartSfxPath,
        StrongAttackDownSfxPath,
        StrongAttackFinishSfxPath
    ];

    internal static void PlayFarAttack(
        FalseThroneCreatureVisuals attackerVisuals,
        bool usePenetrateEffect)
    {
        string texturePath = usePenetrateEffect
            ? FarPenetrateTexturePath
            : FarCrystalRiseTexturePath;
        Texture2D? texture = LoadTexture(texturePath);
        Texture2D? flashTexture = LoadTexture(AreaEffectTexturePath);
        if (texture == null
            || flashTexture == null
            || !TryCreateRoot(
                "FalseThroneFarAttackVfx",
                attackerVisuals,
                out Node2D root,
                out float direction))
        {
            return;
        }

        LocalOggOneShotPlayer.Play(
            usePenetrateEffect ? AttackBoomSfxPath : AttackUpSfxPath,
            -2f);
        var additive = CreateAdditiveMaterial();
        bool flipH = direction > 0f;
        var hit = new Sprite2D
        {
            Name = "Hit",
            Texture = texture,
            Position = new Vector2(direction * 235f, 34f),
            Scale = new Vector2(0.18f, 0.18f),
            FlipH = flipH,
            Modulate = new Color(1f, 1f, 1f, 0f),
            Material = additive,
            ZIndex = 4
        };
        var flash = new Sprite2D
        {
            Name = "Flash",
            Texture = flashTexture,
            Position = hit.Position,
            Scale = Vector2.One * 0.25f,
            FlipH = flipH,
            Modulate = new Color(0.45f, 1f, 0.70f, 0f),
            Material = additive,
            ZIndex = 5
        };
        root.AddChildSafely(hit);
        root.AddChildSafely(flash);

        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(hit, "modulate:a", 1f, 0.06f);
        tween.TweenProperty(
                hit,
                "scale",
                Vector2.One * 0.62f,
                0.18f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(
                hit,
                "position",
                hit.Position + new Vector2(direction * 54f, -8f),
                0.28f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(hit, "modulate:a", 0f, 0.24f)
            .SetDelay(0.24f);
        tween.TweenProperty(flash, "modulate:a", 0.85f, 0.04f)
            .SetDelay(0.08f);
        tween.TweenProperty(
                flash,
                "scale",
                Vector2.One * 1.22f,
                0.22f)
            .SetDelay(0.08f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(flash, "modulate:a", 0f, 0.18f)
            .SetDelay(0.20f);
        QueueFreeAfter(root, 0.58f);
    }

    internal static void PlayCrystalArea(
        FalseThroneCreatureVisuals attackerVisuals)
    {
        Texture2D? fallTexture = LoadTexture(AreaFallTexturePath);
        Texture2D? embeddedTexture = LoadTexture(AreaEmbeddedTexturePath);
        Texture2D? shockwaveTexture = LoadTexture(AreaShockwaveTexturePath);
        Texture2D? flashTexture = LoadTexture(AreaEffectTexturePath);
        if (fallTexture == null
            || embeddedTexture == null
            || shockwaveTexture == null
            || flashTexture == null
            || !TryCreateRoot(
                "FalseThroneCrystalAreaVfx",
                attackerVisuals,
                out Node2D root,
                out float direction))
        {
            return;
        }

        LocalOggOneShotPlayer.Play(StrongAttackStartSfxPath, -2f);
        var additive = CreateAdditiveMaterial();
        bool flipH = direction > 0f;
        Vector2 impact = new(direction * 280f, 118f);
        var falling = CreateEffectSprite(
            "FallingCrystal",
            fallTexture,
            impact + new Vector2(0f, -490f),
            0.48f,
            flipH,
            additive,
            5);
        var embedded = CreateEffectSprite(
            "EmbeddedCrystal",
            embeddedTexture,
            impact,
            0.48f,
            flipH,
            additive,
            4);
        var shockwave = CreateEffectSprite(
            "Shockwave",
            shockwaveTexture,
            impact + new Vector2(0f, 36f),
            0.34f,
            flipH,
            additive,
            3);
        var flash = CreateEffectSprite(
            "ImpactFlash",
            flashTexture,
            impact,
            0.38f,
            flipH,
            additive,
            6);
        root.AddChildSafely(falling);
        root.AddChildSafely(embedded);
        root.AddChildSafely(shockwave);
        root.AddChildSafely(flash);

        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(falling, "modulate:a", 1f, 0.08f);
        tween.TweenProperty(falling, "position", impact, 0.42f)
            .SetTrans(Tween.TransitionType.Quart)
            .SetEase(Tween.EaseType.In);
        tween.TweenProperty(falling, "modulate:a", 0f, 0.08f)
            .SetDelay(0.42f);
        tween.TweenProperty(embedded, "modulate:a", 1f, 0.04f)
            .SetDelay(0.42f);
        tween.TweenProperty(embedded, "modulate:a", 0f, 0.30f)
            .SetDelay(0.88f);
        tween.TweenProperty(shockwave, "modulate:a", 0.92f, 0.05f)
            .SetDelay(0.42f);
        tween.TweenProperty(
                shockwave,
                "scale",
                Vector2.One * 1.08f,
                0.30f)
            .SetDelay(0.42f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(shockwave, "modulate:a", 0f, 0.22f)
            .SetDelay(0.66f);
        tween.TweenProperty(flash, "modulate:a", 1f, 0.03f)
            .SetDelay(0.42f);
        tween.TweenProperty(
                flash,
                "scale",
                Vector2.One * 1.36f,
                0.22f)
            .SetDelay(0.42f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(flash, "modulate:a", 0f, 0.20f)
            .SetDelay(0.57f);
        tween.TweenCallback(Callable.From(
                () => LocalOggOneShotPlayer.Play(
                    StrongAttackDownSfxPath,
                    -1.5f)))
            .SetDelay(0.42f);
        tween.TweenCallback(Callable.From(
                () => LocalOggOneShotPlayer.Play(
                    StrongAttackFinishSfxPath,
                    -1.5f)))
            .SetDelay(0.74f);
        QueueFreeAfter(root, 1.24f);
    }

    internal static void PlayTransformation(
        FalseThroneCreatureVisuals attackerVisuals)
    {
        Texture2D? bodyTexture = LoadTexture(TransformationTexturePath);
        Texture2D? maskTexture = LoadTexture(TransformationMaskTexturePath);
        Texture2D? silhouetteTexture = LoadTexture(
            TransformationMask2TexturePath);
        if (bodyTexture == null
            || maskTexture == null
            || silhouetteTexture == null
            || !TryCreateRoot(
                "FalseThroneTransformationVfx",
                attackerVisuals,
                out Node2D root,
                out float direction))
        {
            return;
        }

        LocalOggOneShotPlayer.Play(ChangeMagicSfxPath, -1f);
        var additive = CreateAdditiveMaterial();
        bool flipH = direction > 0f;
        var body = CreateEffectSprite(
            "Body",
            bodyTexture,
            new Vector2(0f, 80f),
            0.34f,
            flipH,
            additive,
            3);
        var mask = CreateEffectSprite(
            "Mask",
            maskTexture,
            body.Position,
            0.34f,
            flipH,
            additive,
            4);
        var silhouette = CreateEffectSprite(
            "Silhouette",
            silhouetteTexture,
            body.Position,
            0.34f,
            flipH,
            additive,
            5);
        root.AddChildSafely(body);
        root.AddChildSafely(mask);
        root.AddChildSafely(silhouette);

        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(body, "modulate:a", 0.92f, 0.12f);
        tween.TweenProperty(
                body,
                "scale",
                Vector2.One * 0.40f,
                0.58f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(body, "modulate:a", 0f, 0.36f)
            .SetDelay(0.78f);
        tween.TweenProperty(mask, "modulate:a", 0.72f, 0.08f)
            .SetDelay(0.10f);
        tween.TweenProperty(mask, "modulate:a", 0f, 0.36f)
            .SetDelay(0.54f);
        tween.TweenProperty(silhouette, "modulate:a", 1f, 0.04f)
            .SetDelay(0.42f);
        tween.TweenProperty(
                silhouette,
                "scale",
                Vector2.One * 0.46f,
                0.34f)
            .SetDelay(0.42f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(silhouette, "modulate:a", 0f, 0.24f)
            .SetDelay(0.66f);
        tween.TweenCallback(Callable.From(
                () => LocalOggOneShotPlayer.Play(CardMagicSfxPath, -1f)))
            .SetDelay(0.42f);
        QueueFreeAfter(root, 1.20f);
    }

    private static bool TryCreateRoot(
        string name,
        FalseThroneCreatureVisuals attackerVisuals,
        out Node2D root,
        out float direction)
    {
        Control? host = NCombatRoom.Instance?.CombatVfxContainer;
        if (host == null
            || !GodotObject.IsInstanceValid(attackerVisuals)
            || !attackerVisuals.IsInsideTree())
        {
            root = null!;
            direction = -1f;
            return false;
        }

        direction = ResolveFacing(attackerVisuals);
        root = new Node2D
        {
            Name = name,
            ZIndex = 180
        };
        host.AddChildSafely(root);
        Marker2D? center = attackerVisuals.GetNodeOrNull<Marker2D>(
            "%CenterPos");
        root.GlobalPosition = center?.GlobalPosition
            ?? attackerVisuals.GlobalPosition;
        return true;
    }

    private static float ResolveFacing(
        FalseThroneCreatureVisuals attackerVisuals)
    {
        float direction = attackerVisuals.GlobalTransform.X.X < 0f
            ? -1f
            : 1f;
        Sprite2D? idle = attackerVisuals.GetNodeOrNull<Sprite2D>("%Visuals");
        if (idle?.FlipH == true)
        {
            direction *= -1f;
        }
        return direction;
    }

    private static Sprite2D CreateEffectSprite(
        string name,
        Texture2D texture,
        Vector2 position,
        float scale,
        bool flipH,
        Material material,
        int zIndex) => new()
    {
        Name = name,
        Texture = texture,
        Position = position,
        Scale = Vector2.One * scale,
        FlipH = flipH,
        Modulate = new Color(1f, 1f, 1f, 0f),
        Material = material,
        ZIndex = zIndex
    };

    private static CanvasItemMaterial CreateAdditiveMaterial() => new()
    {
        BlendMode = CanvasItemMaterial.BlendModeEnum.Add
    };

    private static Texture2D? LoadTexture(string path)
    {
        try
        {
            return ResourceLoader.Load<Texture2D>(
                path);
        }
        catch
        {
            return null;
        }
    }

    private static void QueueFreeAfter(Node2D root, float seconds)
    {
        Tween lifetime = root.CreateTween();
        lifetime.TweenInterval(seconds);
        lifetime.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(root))
            {
                root.QueueFree();
            }
        }));
    }
}

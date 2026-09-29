using System;
using System.IO;
using System.Linq;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.visuals;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Environment = System.Environment;

namespace LibraryOfRuina.patches;

internal static class WrappedMonsterVisualFactory
{
    private static readonly Vector2 DefaultStolenCardScale = new(0.55f, 0.55f);

    public static bool ShouldWrap(MonsterModel monster)
    {
        return MonsterVisualCatalog.Contains(monster.Id.Entry);
    }

    public static NCreatureVisuals Create(MonsterModel monster) =>
        MonsterVisualCatalog.Create(monster);

    internal static NCreatureVisuals CreateStaticSpriteVisuals(
        string id,
        CreatureVisualLayout layout,
        string texturePath)
    {
        var visuals = new NCreatureVisuals { Name = id };
        var sprite = CreateSpriteNode("Visuals", layout, visible: true);

        Texture2D? texture = TryLoadTexture(id, texturePath);
        GodotTextureSafety.TrySetTexture(sprite, texture);

        AddLayoutNodes(visuals, layout, sprite, attackSprite: null);
        return visuals;
    }

    internal static NCreatureVisuals CreateScriptedSpriteVisuals<TVisuals>(
        string id,
        Action<TVisuals>? configure = null)
        where TVisuals : SpriteAttackCreatureVisuals, new()
    {
        SpriteVisualProfile profile =
            MonsterVisualCatalog.GetRequiredProfile(id);
        return CreateScriptedSpriteVisuals(
            id,
            profile.DefaultIdleTexturePath,
            configure);
    }

    internal static NCreatureVisuals CreateScriptedSpriteVisuals<TVisuals>(
        string id,
        string idleTexturePath,
        Action<TVisuals>? configure = null)
        where TVisuals : SpriteAttackCreatureVisuals, new()
    {
        CreatureVisualLayout layout = MonsterVisualCatalog.GetLayout(id);

        var visuals = new TVisuals { Name = id };
        configure?.Invoke(visuals);
        SpriteVisualProfile profile = visuals.SpriteProfile
            ?? throw new InvalidOperationException(
                $"{typeof(TVisuals).Name} has no sprite visual profile.");
        SpriteVisualProfile catalogProfile =
            MonsterVisualCatalog.GetRequiredProfile(id);
        if (!ReferenceEquals(profile, catalogProfile))
        {
            throw new InvalidOperationException(
                $"Catalog profile for '{id}' does not match "
                + $"{typeof(TVisuals).Name}.SpriteProfile.");
        }
        if (!profile.AssetPaths.Contains(
                idleTexturePath,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Initial idle texture '{idleTexturePath}' for '{id}' is not "
                + "declared by its sprite visual profile.");
        }

        MonsterVisualDebug.Trace(
            $"Create id={id} visualClass={typeof(TVisuals).Name} "
            + $"texturePath={idleTexturePath}");
        var idleSprite = CreateSpriteNode("Visuals", layout, visible: true);
        var attackSprite = CreateSpriteNode("AttackVisuals", layout, visible: false);
        var motionRoot = new Node2D
        {
            Name = "MotionRoot",
            UniqueNameInOwner = true,
            Position = Vector2.Zero,
        };
        motionRoot.AddChild(idleSprite);
        motionRoot.AddChild(attackSprite);

        Texture2D? texture = TryLoadTexture(id, idleTexturePath);
        GodotTextureSafety.TrySetTexture(idleSprite, texture);

        AddLayoutNodes(visuals, layout, idleSprite, attackSprite, motionRoot);

        MonsterVisualDebug.Trace(
            $"Created id={id} visualClass={typeof(TVisuals).Name} children={visuals.GetChildCount()} " +
            $"hasMotionRoot={visuals.HasNode("MotionRoot")} hasVisuals={visuals.HasNode("Visuals")} hasAttackVisuals={visuals.HasNode("AttackVisuals")} " +
            $"hasBounds={visuals.HasNode("Bounds")} hasCenter={visuals.HasNode("CenterPos")} hasIntent={visuals.HasNode("IntentPos")}");
        return visuals;
    }

    private static Sprite2D CreateSpriteNode(string nodeName, CreatureVisualLayout layout, bool visible)
    {
        return new Sprite2D
        {
            Name = nodeName,
            UniqueNameInOwner = true,
            Position = layout.SpritePos,
            Scale = layout.SpriteScale,
            Visible = visible,
        };
    }

    private static Texture2D? TryLoadTexture(string id, string texturePath)
    {
        Texture2D? texture = null;
        try
        {
            texture = ResourceLoader.Load<Texture2D>(texturePath);
        }
        catch (Exception ex)
        {
            MonsterVisualDebug.Write($"Texture load error id={id}: {ex.Message}");
        }

        if (texture != null)
        {
            MonsterVisualDebug.Trace($"Texture loaded id={id} size={texture.GetSize()}");
        }
        else
        {
            MonsterVisualDebug.Write($"Texture NULL id={id} path={texturePath} exists={ResourceLoader.Exists(texturePath)}");
        }

        return texture;
    }

    private static void AddLayoutNodes(
        NCreatureVisuals visuals,
        CreatureVisualLayout layout,
        Sprite2D visualSprite,
        Sprite2D? attackSprite,
        Node2D? motionRoot = null)
    {
        var bounds = new Control
        {
            Name = "Bounds",
            UniqueNameInOwner = true,
            LayoutMode = 3,
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = layout.BoundsLeft,
            OffsetTop = layout.BoundsTop,
            OffsetRight = layout.BoundsRight,
            OffsetBottom = layout.BoundsBottom,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var center = new Marker2D
        {
            Name = "CenterPos",
            UniqueNameInOwner = true,
            Position = layout.CenterPos,
        };

        var intent = new Marker2D
        {
            Name = "IntentPos",
            UniqueNameInOwner = true,
            Position = layout.IntentPos,
        };

        Marker2D? talk = null;
        if (layout.TalkPos.HasValue)
        {
            talk = new Marker2D
            {
                Name = "TalkPos",
                UniqueNameInOwner = true,
                Position = layout.TalkPos.Value,
            };
        }

        Marker2D? stolenCard = null;
        if (layout.StolenCardPos.HasValue)
        {
            stolenCard = new Marker2D
            {
                Name = "StolenCardPos",
                UniqueNameInOwner = true,
                Position = layout.StolenCardPos.Value,
                Scale = layout.StolenCardScale ?? DefaultStolenCardScale,
            };
        }

        if (motionRoot != null)
        {
            visuals.AddChild(motionRoot);
        }
        else
        {
            visuals.AddChild(visualSprite);
            if (attackSprite != null)
            {
                visuals.AddChild(attackSprite);
            }
        }
        visuals.AddChild(bounds);
        visuals.AddChild(center);
        visuals.AddChild(intent);
        if (talk != null)
        {
            visuals.AddChild(talk);
        }
        if (stolenCard != null)
        {
            visuals.AddChild(stolenCard);
        }
        if (!Mathf.IsZeroApprox(layout.StateDisplayLiftY))
        {
            var stateDisplayOffset = new CreatureStateDisplayOffset
            {
                Name = "StateDisplayOffset",
                LiftY = layout.StateDisplayLiftY,
            };
            visuals.AddChild(stateDisplayOffset);
            AssignOwnerRecursive(stateDisplayOffset, visuals);
        }

        if (motionRoot != null)
        {
            AssignOwnerRecursive(motionRoot, visuals);
        }
        else
        {
            AssignOwnerRecursive(visualSprite, visuals);
            if (attackSprite != null)
            {
                AssignOwnerRecursive(attackSprite, visuals);
            }
        }
        AssignOwnerRecursive(bounds, visuals);
        AssignOwnerRecursive(center, visuals);
        AssignOwnerRecursive(intent, visuals);
        if (talk != null)
        {
            AssignOwnerRecursive(talk, visuals);
        }
        if (stolenCard != null)
        {
            AssignOwnerRecursive(stolenCard, visuals);
        }
    }

    internal static NCreatureVisuals CreateSceneBackedVisuals<TVisuals>(
        string id,
        string scenePath)
        where TVisuals : SceneAnimatedCreatureVisuals, new()
    {
        PackedScene packed = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException(
                $"Cannot load scene-backed monster visual: {scenePath}");
        Node2D templateRoot = packed.Instantiate<Node2D>();

        try
        {
            if (templateRoot.GetScript().VariantType != Variant.Type.Nil)
            {
                throw new InvalidOperationException(
                    $"Scene-backed monster visual '{id}' must use a "
                    + "scriptless template root.");
            }
            if (!templateRoot.Position.IsEqualApprox(Vector2.Zero)
                || !templateRoot.Scale.IsEqualApprox(Vector2.One)
                || !Mathf.IsZeroApprox(templateRoot.Rotation)
                || !Mathf.IsZeroApprox(templateRoot.Skew))
            {
                throw new InvalidOperationException(
                    $"Scene-backed monster visual '{id}' requires an identity "
                    + "template root; move MotionRoot instead.");
            }

            var visuals = new TVisuals { Name = id };
            while (templateRoot.GetChildCount() > 0)
            {
                Node child = templateRoot.GetChild(0);
                templateRoot.RemoveChild(child);
                visuals.AddChild(child);
                AssignOwnerRecursive(child, visuals);
            }

            ValidateSceneBackedNodes(id, visuals);
            MonsterVisualDebug.Trace(
                $"Create id={id} sceneVisualClass={typeof(TVisuals).Name} "
                + $"scenePath={scenePath} children={visuals.GetChildCount()}");
            return visuals;
        }
        finally
        {
            templateRoot.Free();
        }
    }

    private static void ValidateSceneBackedNodes(
        string id,
        NCreatureVisuals visuals)
    {
        Node2D motionRoot = visuals.GetNodeOrNull<Node2D>("MotionRoot")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing MotionRoot.");
        Sprite2D idle = visuals.GetNodeOrNull<Sprite2D>(
                "MotionRoot/Visuals")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing Visuals.");
        Sprite2D attack = visuals.GetNodeOrNull<Sprite2D>(
                "MotionRoot/AttackVisuals")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing AttackVisuals.");
        AnimationPlayer animationPlayer =
            visuals.GetNodeOrNull<AnimationPlayer>("AnimationPlayer")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing AnimationPlayer.");
        Control bounds = visuals.GetNodeOrNull<Control>("Bounds")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing Bounds.");
        Marker2D center = visuals.GetNodeOrNull<Marker2D>("CenterPos")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing CenterPos.");
        Marker2D intent = visuals.GetNodeOrNull<Marker2D>("IntentPos")
            ?? throw new InvalidOperationException(
                $"Scene-backed monster visual '{id}' is missing IntentPos.");

        foreach (Node node in new Node[]
                 {
                     motionRoot,
                     idle,
                     attack,
                     animationPlayer,
                     bounds,
                     center,
                     intent
                 })
        {
            if (!node.UniqueNameInOwner)
            {
                throw new InvalidOperationException(
                    $"Scene-backed monster visual '{id}' node '{node.Name}' "
                    + "must keep unique_name_in_owner=true.");
            }
        }
    }

    public static NCreatureVisuals CreateFromScene(string scenePath)
    {
        PackedScene? packed = null;
        try { packed = ResourceLoader.Load<PackedScene>(scenePath); }
        catch (Exception ex) { MonsterVisualDebug.Write($"Scene load error path={scenePath}: {ex.Message}"); }

        if (packed == null)
            throw new InvalidOperationException($"Cannot load scene: {scenePath}");

        Node2D templateRoot = packed.Instantiate<Node2D>();
        MonsterVisualDebug.Trace($"Scene instantiated path={scenePath} rootType={templateRoot.GetType().FullName} rootClass={templateRoot.GetClass()}");

        if (templateRoot is NCreatureVisuals cv)
        {
            MonsterVisualDebug.Trace($"Scene returned direct NCreatureVisuals path={scenePath} name={cv.Name}");
            return cv;
        }

        var visuals = new NCreatureVisuals { Name = templateRoot.Name };
        while (templateRoot.GetChildCount() > 0)
        {
            Node child = templateRoot.GetChild(0);
            templateRoot.RemoveChild(child);
            visuals.AddChild(child);
            AssignOwnerRecursive(child, visuals);
        }
        templateRoot.Free();
        MonsterVisualDebug.Trace($"Scene wrapped into NCreatureVisuals path={scenePath} name={visuals.Name} children={visuals.GetChildCount()}");
        return visuals;
    }

    private static void AssignOwnerRecursive(Node node, Node owner)
    {
        node.Owner = owner;
        foreach (Node child in node.GetChildren())
            AssignOwnerRecursive(child, owner);
    }
}

internal static class MonsterVisualDebug
{
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly string LogPath = ProjectSettings.GlobalizePath("user://mods/LibraryOfRuina/monster_visuals_debug.log");

    /// <summary>成功路径的诊断，每只怪生成外观都会走到，只进 Debug 级日志。</summary>
    public static void Trace(string message)
    {
        Log.Debug($"[MonsterVisualDebug] {message}");
    }

    /// <summary>失败路径：进游戏日志，同时追加到单独文件方便玩家反馈；文件超过 1 MB 时轮换为 .old。</summary>
    public static void Write(string message)
    {
        string text = $"[MonsterVisualDebug {DateTime.Now:HH:mm:ss.fff}] {message}";
        Log.Warn(text);
        try
        {
            string? dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var info = new FileInfo(LogPath);
            if (info.Exists && info.Length > MaxLogBytes)
                File.Move(LogPath, LogPath + ".old", overwrite: true);
            File.AppendAllText(LogPath, text + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 日志文件只是反馈辅助，写不进去时游戏日志里已有同一条。
        }
    }
}

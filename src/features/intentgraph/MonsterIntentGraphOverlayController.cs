using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Godot;
using LibraryOfRuina.framework.assets;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.features.intentgraph;

internal static class MonsterIntentGraphOverlayController
{
    
    
    private const bool UseSceneBackedPanel = false;
    private const string IntentGraphPanelScenePath = "res://LibraryOfRuina/intentgraph/scenes/intent_graph_panel.tscn";
    private const string HoverTipTexturePath = "res://images/ui/hover_tip.png";
    private const float SideSpacing = 12f;
    private const float ScreenPadding = 8f;


    private static readonly Dictionary<NCreature, NMonsterIntentGraphPanel> ActivePanels =
        new Dictionary<NCreature, NMonsterIntentGraphPanel>();

    private static bool _panelSceneLoadAttempted;
    private static bool _fallbackThemeLoadAttempted;
    private static PackedScene? _panelScene;
    private static Texture2D? _fallbackHoverTipTexture;
    private static Font? _fallbackHoverTitleFont;

    public static void ShowFor(NCreature creature)
    {
        if (!IntentGraphDisplayConfigRepository.IsEnabled)
        {
            HideFor(creature);
            return;
        }

        LorLog.InfoOnce("IntentGraph.ShowFor", "[LibraryOfRuina.IntentGraph] ShowFor hook triggered.");

        if (creature.Entity?.Monster == null)
        {
            LogOverlayDiagnosticOnce("hidden-no-bound-monster", "Overlay hidden: no bound monster for hovered creature.");
            HideFor(creature);
            return;
        }

        Node? hoverRoot = NGame.Instance?.HoverTipsContainer;
        if (hoverRoot == null)
        {
            LogOverlayDiagnosticOnce("hidden-no-hover-root", "Overlay hidden: hover tips root is unavailable.");
            HideFor(creature);
            return;
        }

        TryGetHoverTipSet(creature, out NHoverTipSet? tipSet);

        NMonsterIntentGraphPanel panel = GetOrCreatePanel(creature, hoverRoot);
        panel.BindCreature(creature);
        panel.RefreshNow();
        if (!panel.HasRenderableGraph)
        {
            string reason = panel.LastHiddenReason;
            if (string.Equals(reason, "no-bound-creature", StringComparison.Ordinal)
                || string.Equals(reason, "no-bound-creature-entity", StringComparison.Ordinal)
                || string.Equals(reason, "no-bound-monster", StringComparison.Ordinal))
            {
                LogOverlayDiagnosticOnce("hidden-" + reason, "Overlay hidden: " + reason + ".");
            }

            if (reason.StartsWith("state-machine-trybuild-failed-and-current-move-fallback-failed", StringComparison.Ordinal))
            {
                LogOverlayDiagnosticOnce(
                    "hidden-trybuild-and-fallback-failed",
                    "Overlay hidden: TryBuild failed and current-move fallback also failed.");
            }

            LogOverlayDiagnosticOnce(
                "hidden-panel-created-no-graph:" + reason,
                "Overlay hidden: panel created but HasRenderableGraph is false. reason=" + reason + ".");
            panel.Visible = false;
            return;
        }

        if (tipSet != null
            && VanillaPrivate.HoverTipSetTextHoverTipContainer.Get(tipSet) is Control textC
            && textC.Size.X > 1f)
        {
            Reposition(panel, tipSet);
        }
        else
        {
            RepositionBesideHitbox(panel, creature.Hitbox);
        }

        panel.Visible = panel.HasRenderableGraph;
    }

    public static void HideFor(NCreature creature)
    {
        if (!ActivePanels.TryGetValue(creature, out NMonsterIntentGraphPanel? panel))
        {
            return;
        }

        ActivePanels.Remove(creature);
        if (GodotObject.IsInstanceValid(panel))
        {
            panel.QueueFree();
        }
    }

    private static NMonsterIntentGraphPanel GetOrCreatePanel(NCreature creature, Node parent)
    {
        if (ActivePanels.TryGetValue(creature, out NMonsterIntentGraphPanel? panel)
            && GodotObject.IsInstanceValid(panel)
            && panel.GetParent() == parent)
        {
            return panel;
        }

        if (panel != null && GodotObject.IsInstanceValid(panel))
        {
            panel.QueueFree();
        }

        NMonsterIntentGraphPanel created = CreatePanel();
        parent.AddChild(created);
        ActivePanels[creature] = created;
        return created;
    }

    private static NMonsterIntentGraphPanel CreatePanel()
    {
        if (UseSceneBackedPanel && TryCreatePanelFromScene(out NMonsterIntentGraphPanel panelFromScene))
        {
            return panelFromScene;
        }

        return CreatePanelFallback();
    }

    private static bool TryCreatePanelFromScene(out NMonsterIntentGraphPanel panel)
    {
        panel = null!;

        PackedScene? panelScene = GetPanelScene();
        if (panelScene == null)
        {
            return false;
        }

        Node? instantiated;
        try
        {
            instantiated = panelScene.Instantiate();
        }
        catch (Exception exception)
        {
            LogOverlayDiagnosticOnce(
                "panel-scene-instantiate-failed",
                "Failed to instantiate intent graph panel scene; using fallback. " + exception.Message);
            return false;
        }

        if (instantiated is NMonsterIntentGraphPanel panelInstance)
        {
            panel = panelInstance;
            return true;
        }

        string nodeType = instantiated?.GetType().FullName ?? "null";
        LogOverlayDiagnosticOnce(
            "panel-scene-type-mismatch",
            "Intent graph panel scene root is not NMonsterIntentGraphPanel (actual=" + nodeType + "); using fallback.");

        if (instantiated != null && GodotObject.IsInstanceValid(instantiated))
        {
            instantiated.QueueFree();
        }

        return false;
    }

    private static PackedScene? GetPanelScene()
    {
        if (_panelSceneLoadAttempted)
        {
            return _panelScene;
        }

        _panelSceneLoadAttempted = true;
        try
        {
            _panelScene = GD.Load<PackedScene>(IntentGraphPanelScenePath);
        }
        catch (Exception exception)
        {
            LogOverlayDiagnosticOnce(
                "panel-scene-load-exception",
                "Failed to load intent graph panel scene; using fallback. " + exception.Message);
            _panelScene = null;
        }

        if (_panelScene == null)
        {
            LogOverlayDiagnosticOnce(
                "panel-scene-load-null",
                "Intent graph panel scene missing at " + IntentGraphPanelScenePath + "; using fallback.");
        }

        return _panelScene;
    }

    private static NMonsterIntentGraphPanel CreatePanelFallback()
    {
        EnsureFallbackThemeAssetsLoaded();

        var panel = new NMonsterIntentGraphPanel
        {
            Name = "IntentGraphPanel",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        if (_fallbackHoverTipTexture != null)
        {
            var shadow = new NinePatchRect
            {
                Name = "Shadow",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                LayoutMode = 2,
                Texture = _fallbackHoverTipTexture,
                RegionRect = new Rect2(-8, -8, 328, 104),
                PatchMarginLeft = 55,
                PatchMarginTop = 43,
                PatchMarginRight = 55,
                PatchMarginBottom = 32,
                Modulate = new Color(0f, 0f, 0f, 0.25098f),
            };
            panel.AddChild(shadow);

            var background = new NinePatchRect
            {
                Name = "Background",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                LayoutMode = 2,
                Texture = _fallbackHoverTipTexture,
                RegionRect = new Rect2(0, 0, 328, 104),
                PatchMarginLeft = 55,
                PatchMarginTop = 43,
                PatchMarginRight = 55,
                PatchMarginBottom = 32,
            };
            panel.AddChild(background);
        }

        var outerMargin = new MarginContainer
        {
            Name = "OuterMargin",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            LayoutMode = 2,
        };
        outerMargin.AddThemeConstantOverride("margin_left", 20);
        outerMargin.AddThemeConstantOverride("margin_top", 14);
        outerMargin.AddThemeConstantOverride("margin_right", 28);
        outerMargin.AddThemeConstantOverride("margin_bottom", 24);

        var vbox = new VBoxContainer
        {
            Name = "VBoxContainer",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        // 标题是 Kreon 粗体。裸 Label 拿不到原版的按语言换字体，中文会落到没有中文字形的 Kreon 上；
        // 这里与 Intent Graph 的面板标题一样，设好 font 覆盖后按当前语言换成粗体字体（MegaLabel 只会换成常规体）。
        var monsterName = new Label
        {
            Name = "MonsterName",
            Text = "Monster",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            UniqueNameInOwner = true,
        };
        monsterName.AddThemeFontOverride("font", _fallbackHoverTitleFont ?? monsterName.GetThemeDefaultFont());
        monsterName.ApplyLocaleFontSubstitution(FontType.Bold, "font");
        monsterName.AddThemeFontSizeOverride("font_size", 22);
        monsterName.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.25098f));
        monsterName.AddThemeConstantOverride("shadow_offset_x", 3);
        monsterName.AddThemeConstantOverride("shadow_offset_y", 2);

        var graphMargin = new MarginContainer
        {
            Name = "GraphMargin",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        graphMargin.AddThemeConstantOverride("margin_left", 12);
        graphMargin.AddThemeConstantOverride("margin_top", 12);
        graphMargin.AddThemeConstantOverride("margin_right", 12);
        graphMargin.AddThemeConstantOverride("margin_bottom", 16);

        var graph = new NIntentGraph
        {
            Name = "IntentGraph",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            UniqueNameInOwner = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        panel.AddChild(outerMargin);
        outerMargin.AddChild(vbox);
        vbox.AddChild(monsterName);
        vbox.AddChild(graphMargin);
        graphMargin.AddChild(graph);

        return panel;
    }

    private static void EnsureFallbackThemeAssetsLoaded()
    {
        // 缓存的是资源对象本身；游戏在局与局之间会释放没有其他引用的缓存资源，之后这里拿到的是已释放的对象，
        // 再用来设主题覆盖会抛 ObjectDisposedException、面板建不起来。失效时重新加载。
        bool cachedValid = (_fallbackHoverTipTexture == null || GodotObject.IsInstanceValid(_fallbackHoverTipTexture))
                           && (_fallbackHoverTitleFont == null || GodotObject.IsInstanceValid(_fallbackHoverTitleFont));
        if (_fallbackThemeLoadAttempted && cachedValid)
        {
            return;
        }

        _fallbackThemeLoadAttempted = true;
        try
        {
            _fallbackHoverTipTexture = GD.Load<Texture2D>(HoverTipTexturePath);
            _fallbackHoverTitleFont = GD.Load<Font>(SharedAssets.KreonBoldGlyphSpaceOneResource);
        }
        catch (Exception exception)
        {
            LogOverlayDiagnosticOnce(
                "fallback-theme-load-failed",
                "Failed to load fallback panel theme resources. " + exception.Message);
        }
    }

    private static bool TryGetHoverTipSet(NCreature creature, out NHoverTipSet? hoverTipSet)
    {
        hoverTipSet = null;

        if (!VanillaPrivate.HoverTipSetActiveHoverTips.IsAvailable && !VanillaPrivate.HoverTipSetOwner.IsAvailable)
        {
            LogReflectionFailure("Cannot resolve NHoverTipSet _activeHoverTips or _owner field.");
            return false;
        }

        if (VanillaPrivate.HoverTipSetActiveHoverTips.IsAvailable)
        {
            object? activeObj = VanillaPrivate.HoverTipSetActiveHoverTips.Get();
            if (activeObj is IDictionary<Control, NHoverTipSet> activeTyped
                && activeTyped.TryGetValue(creature.Hitbox, out hoverTipSet))
            {
                return true;
            }

            if (activeObj is IDictionary active && active.Contains(creature.Hitbox))
            {
                if (active[creature.Hitbox] is NHoverTipSet set)
                {
                    hoverTipSet = set;
                    return true;
                }
            }
        }

        if (VanillaPrivate.HoverTipSetOwner.IsAvailable && NGame.Instance?.HoverTipsContainer != null)
        {
            foreach (Node child in NGame.Instance.HoverTipsContainer.GetChildren())
            {
                if (child is NHoverTipSet set
                    && ReferenceEquals(VanillaPrivate.HoverTipSetOwner.Get(set), creature.Hitbox))
                {
                    hoverTipSet = set;
                    return true;
                }
            }
        }

        return false;
    }

    private static void Reposition(NMonsterIntentGraphPanel panel, NHoverTipSet tipSet)
    {
        if (VanillaPrivate.HoverTipSetTextHoverTipContainer.Get(tipSet) is not Control textContainer)
        {
            LogReflectionFailure("Cannot resolve NHoverTipSet text container field.");
            return;
        }

        panel.ResetSize();
        panel.GlobalPosition = textContainer.GlobalPosition + new Vector2(textContainer.Size.X + SideSpacing, 0f);

        if (NGame.Instance == null)
        {
            return;
        }

        Rect2 viewport = NGame.Instance.GetViewportRect();
        if (panel.GlobalPosition.X + panel.Size.X > viewport.Size.X - ScreenPadding)
        {
            panel.GlobalPosition = new Vector2(
                textContainer.GlobalPosition.X - panel.Size.X - SideSpacing,
                panel.GlobalPosition.Y);
        }

        if (panel.GlobalPosition.X < ScreenPadding)
        {
            panel.GlobalPosition = new Vector2(ScreenPadding, panel.GlobalPosition.Y);
        }

        float clampedY = Mathf.Clamp(
            panel.GlobalPosition.Y,
            ScreenPadding,
            Math.Max(ScreenPadding, viewport.Size.Y - panel.Size.Y - ScreenPadding));

        panel.GlobalPosition = new Vector2(panel.GlobalPosition.X, clampedY);
    }

    
    
    
    
    private static void RepositionBesideHitbox(NMonsterIntentGraphPanel panel, Control hitbox)
    {
        panel.ResetSize();
        panel.GlobalPosition = hitbox.GlobalPosition + new Vector2(hitbox.Size.X + SideSpacing, 0f);

        if (NGame.Instance == null)
        {
            return;
        }

        Rect2 viewport = NGame.Instance.GetViewportRect();
        if (panel.GlobalPosition.X + panel.Size.X > viewport.Size.X - ScreenPadding)
        {
            panel.GlobalPosition = new Vector2(
                hitbox.GlobalPosition.X - panel.Size.X - SideSpacing,
                panel.GlobalPosition.Y);
        }

        if (panel.GlobalPosition.X < ScreenPadding)
        {
            panel.GlobalPosition = new Vector2(ScreenPadding, panel.GlobalPosition.Y);
        }

        float clampedY = Mathf.Clamp(
            panel.GlobalPosition.Y,
            ScreenPadding,
            Mathf.Max(ScreenPadding, viewport.Size.Y - panel.Size.Y - ScreenPadding));

        panel.GlobalPosition = new Vector2(panel.GlobalPosition.X, clampedY);
    }

    private static void LogReflectionFailure(string message)
    {
        LorLog.ErrorOnce("IntentGraph.ReflectionFailure", $"[LibraryOfRuina.IntentGraph] {message}");
    }

    private static void LogOverlayDiagnosticOnce(string key, string message)
    {
        LorLog.WarnOnce("IntentGraph.Overlay:" + key, "[LibraryOfRuina.IntentGraph] " + message);
    }
}

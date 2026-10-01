using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.acts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.patches;

/// <summary>
/// 本模组的战斗背景多是整幅 16:9 画面（1920×1080 或 2560×1440 的图层），不像原版那样四周留出大片余量。
/// 这里把每个图层缩放到刚好铺满当前可见区域（再留一圈震屏余量），让 16:9 下能看到几乎整幅画。
/// 需要铺满的范围随三样东西变：视口尺寸（超宽/窄屏）、原版 BgContainer 的缩放（窗口比例变化时原版改成
/// 0.9 或 ≥1.08）、遭遇战镜头缩放（GetCameraScaling，部分解放战会在阶段中途改）。所以每帧比较这些缩放
/// 与视口尺寸，变了才重算；只看缩放不看平移，震屏的平移抖动不会让背景跟着一跳一跳。
/// </summary>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.SetUpBackground))]
internal static class ResponsiveCombatBackgroundPatch
{
    private const float LibraryActBackgroundOffsetY = -15f;
    // 原版重击震屏（ShakeStrength.Strong）的位移是 40。
    private const float ShakeMargin = 40f;
    private const float SixteenByNine = 16f / 9f;
    private const float ScaleEpsilon = 0.0001f;

    [HarmonyPostfix]
    private static void Postfix(
        NCombatRoom __instance,
        ICombatRoomVisuals ____visuals)
    {
        if (__instance.Background is not { } background
            || ____visuals.Encounter.GetType().Assembly != typeof(ResponsiveCombatBackgroundPatch).Assembly)
        {
            return;
        }

        bool shiftForLibraryAct = ____visuals.Act is LibraryOfRuinaActModel;
        // 背景图层可能通过 AddChildSafely 延迟添加；Ready 后才能可靠读取实际尺寸。
        if (background.IsNodeReady())
        {
            new CoverageTracker(background, shiftForLibraryAct).Start();
        }
        else
        {
            background.Connect(Node.SignalName.Ready,
                Callable.From(() => new CoverageTracker(background, shiftForLibraryAct).Start()),
                (uint)GodotObject.ConnectFlags.OneShot);
        }
    }

    private static bool IsLayerSlot(string name) =>
        name.StartsWith("Layer_", StringComparison.Ordinal) || name == "Foreground";

    private sealed class CoverageTracker(NCombatBackground background, bool shiftForLibraryAct)
    {
        private readonly List<(Control Root, Vector2 BaseScale)> _layers = [];
        private SceneTree? _tree;
        private Vector2 _lastSlotScale;
        private Vector2 _lastViewportSize;

        public void Start()
        {
            foreach (Node node in background.GetChildren())
            {
                if (node is not Control layerSlot || !IsLayerSlot(layerSlot.Name.ToString()))
                {
                    continue;
                }

                foreach (Node layerNode in layerSlot.GetChildren())
                {
                    if (layerNode is not Control layerRoot)
                    {
                        continue;
                    }

                    if (shiftForLibraryAct)
                    {
                        layerRoot.Position += Vector2.Down * LibraryActBackgroundOffsetY;
                    }

                    if (layerRoot.Size.X > ScaleEpsilon && layerRoot.Size.Y > ScaleEpsilon)
                    {
                        layerRoot.PivotOffset = layerRoot.Size * 0.5f;
                        _layers.Add((layerRoot, layerRoot.Scale));
                    }
                }
            }

            if (_layers.Count == 0)
            {
                return;
            }

            Refresh();
            _tree = background.GetTree();
            _tree.ProcessFrame += OnProcessFrame;
            background.TreeExiting += Stop;
        }

        private void Stop()
        {
            if (_tree != null)
            {
                _tree.ProcessFrame -= OnProcessFrame;
                _tree = null;
            }

            background.TreeExiting -= Stop;
        }

        private void OnProcessFrame()
        {
            if (!GodotObject.IsInstanceValid(background) || !background.IsInsideTree())
            {
                Stop();
                return;
            }

            Control slot = (Control)_layers[0].Root.GetParent();
            Vector2 slotScale = slot.GetGlobalTransformWithCanvas().Scale;
            Vector2 viewportSize = background.GetViewport().GetVisibleRect().Size;
            if (!slotScale.IsEqualApprox(_lastSlotScale) || !viewportSize.IsEqualApprox(_lastViewportSize))
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            Rect2 visible = background.GetViewport().GetVisibleRect();
            _lastViewportSize = visible.Size;
            Rect2 required = visible.Grow(ShakeMargin);
            foreach ((Control root, Vector2 baseScale) in _layers)
            {
                if (!GodotObject.IsInstanceValid(root) || root.GetParent() is not Control slot)
                {
                    continue;
                }

                Transform2D slotTransform = slot.GetGlobalTransformWithCanvas();
                _lastSlotScale = slotTransform.Scale;
                Transform2D toSlot = slotTransform.AffineInverse();
                Vector2 center = root.Position + root.Size * 0.5f;
                float halfWidth = 0f;
                float halfHeight = 0f;
                foreach (Vector2 corner in Corners(required))
                {
                    Vector2 local = toSlot * corner - center;
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.X));
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.Y));
                }

                Vector2 baseSize = root.Size * baseScale.Abs();
                float coverage = Mathf.Max(2f * halfWidth / baseSize.X, 2f * halfHeight / baseSize.Y);
                // 原版式分层（例如 2764.8×1296 的图层）本来就靠裁边取景，只在不够铺满时放大，不缩小。
                if (Mathf.Abs(baseSize.X / baseSize.Y - SixteenByNine) > 0.01f)
                {
                    coverage = Mathf.Max(coverage, 1f);
                }

                root.Scale = baseScale * coverage;
            }
        }

        private static IEnumerable<Vector2> Corners(Rect2 rect)
        {
            yield return rect.Position;
            yield return new Vector2(rect.End.X, rect.Position.Y);
            yield return new Vector2(rect.Position.X, rect.End.Y);
            yield return rect.End;
        }
    }
}

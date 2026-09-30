using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.acts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.patches;

/// <summary>
/// Keeps LibraryOfRuina combat backgrounds large enough for the complete native
/// auto-aspect canvas. The game scales the background container to 0.9 on wide
/// screens, so a 1920 or 2560 pixel layer cannot cover the 2580 pixel logical
/// width used by ultrawide mobile displays.
/// </summary>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.SetUpBackground))]
internal static class ResponsiveCombatBackgroundPatch
{
    // Matches the safe layer canvas used by current native/BaseLib dynamic
    // combat backgrounds. At the native 0.9 wide-screen scale this still
    // covers 2593.386 logical pixels, including the 2580-pixel mobile canvas.
    private const float SafeCanvasWidth = 2881.54f;
    private const float SafeCanvasHeight = 1350.72f;
    private const float LibraryActBackgroundOffsetY = -15f;
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
            ApplyCanvasCoverage(background, shiftForLibraryAct);
        }
        else
        {
            background.Connect(Node.SignalName.Ready,
                Callable.From(() => ApplyCanvasCoverage(background, shiftForLibraryAct)),
                (uint)GodotObject.ConnectFlags.OneShot);
        }
    }

    private static void ApplyCanvasCoverage(NCombatBackground background, bool shiftForLibraryAct)
    {
        foreach (Node node in background.GetChildren())
        {
            if (node is not Control layerSlot || !IsLayerSlot(layerSlot.Name.ToString()))
            {
                continue;
            }

            foreach (Node layerNode in layerSlot.GetChildren())
            {
                if (layerNode is Control layerRoot)
                {
                    EnsureSafeCanvasCoverage(layerRoot);
                    if (shiftForLibraryAct)
                    {
                        layerRoot.Position +=
                            Vector2.Down * LibraryActBackgroundOffsetY;
                    }
                }
            }
        }
    }

    private static bool IsLayerSlot(string name) =>
        name.StartsWith("Layer_", StringComparison.Ordinal) || name == "Foreground";

    private static void EnsureSafeCanvasCoverage(Control layerRoot)
    {
        Vector2 size = layerRoot.Size;
        Vector2 currentScale = layerRoot.Scale;
        float renderedWidth = size.X * Mathf.Abs(currentScale.X);
        float renderedHeight = size.Y * Mathf.Abs(currentScale.Y);
        if (renderedWidth <= ScaleEpsilon || renderedHeight <= ScaleEpsilon)
        {
            return;
        }

        float coverageScale = Mathf.Max(
            SafeCanvasWidth / renderedWidth,
            SafeCanvasHeight / renderedHeight);
        if (coverageScale <= 1f + ScaleEpsilon)
        {
            return;
        }

        layerRoot.PivotOffset = size * 0.5f;
        layerRoot.Scale = currentScale * coverageScale;
    }
}

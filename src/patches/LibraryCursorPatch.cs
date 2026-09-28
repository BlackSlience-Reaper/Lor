using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.patches;

internal static class LibraryCursorPatch
{
    private const string CursorDefaultPath = "res://images/packed/common_ui/cursor_default.png";
    private const string CursorTiltedPath = "res://images/packed/common_ui/cursor_tilted.png";
    private const string CursorInspectPath = "res://images/packed/common_ui/cursor_inspect.png";

    private static readonly Vector2 CursorHotSpot = new(17f, 17f);


    private static Image? _defaultCursor;
    private static Image? _tiltedCursor;
    private static Image? _inspectCursor;
    private static bool _applied;

    public static void ApplyToCurrentGame()
    {
        var cursorManager = NGame.Instance?.CursorManager;
        if (cursorManager != null)
            Apply(cursorManager);
    }

    public static void Apply(NCursorManager cursorManager)
    {
        try
        {
            var defaultCursor = _defaultCursor ??= LoadImage(CursorDefaultPath);
            var tiltedCursor = _tiltedCursor ??= LoadImage(CursorTiltedPath);
            var inspectCursor = _inspectCursor ??= LoadImage(CursorInspectPath);

            VanillaPrivate.CursorManagerCursorTilted.Set(cursorManager, tiltedCursor);
            VanillaPrivate.CursorManagerCursorNotTilted.Set(cursorManager, defaultCursor);
            VanillaPrivate.CursorManagerCursorInspect.Set(cursorManager, inspectCursor);

            Input.SetCustomMouseCursor(inspectCursor, Input.CursorShape.Help, CursorHotSpot);
            cursorManager.OverrideCursor(tiltedCursor, defaultCursor, CursorHotSpot);

            if (!_applied)
            {
                Log.Info("[LibraryOfRuina] Replaced the game cursor with Library cursor assets.");
                _applied = true;
            }
        }
        catch (Exception e)
        {
            Log.Error("[LibraryOfRuina] Failed to apply custom cursor: " + e);
        }
    }

    private static Image LoadImage(string path)
    {
        return ResourceLoader.Load<Image>(path, null, ResourceLoader.CacheMode.Ignore)
               ?? throw new InvalidOperationException("Failed to load cursor image: " + path);
    }
}

[HarmonyPatch(typeof(NCursorManager), nameof(NCursorManager._EnterTree))]
internal static class LibraryCursorManagerEnterTreePatch
{
    [HarmonyPostfix]
    private static void Postfix(NCursorManager __instance)
    {
        LibraryCursorPatch.Apply(__instance);
    }
}

[HarmonyPatch(typeof(NCursorManager), nameof(NCursorManager.StopOverridingCursor))]
internal static class LibraryCursorManagerStopOverridingCursorPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCursorManager __instance)
    {
        LibraryCursorPatch.Apply(__instance);
    }
}

using Godot;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.features.moontext;

internal static class MoonTextService
{
    private static NMoonTextOverlayController? _currentController;

    public static void ShowLine(Creature speaker, LocString line)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled || speaker.IsDead)
        {
            return;
        }

        NMoonTextOverlayController? controller = GetOrCreateController();
        controller?.ShowLine(speaker, line);
    }

    public static void StartRandomLoop(IReadOnlyList<LocString> lines, float intervalSeconds, Rect2 spawnArea)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled)
        {
            return;
        }

        NMoonTextOverlayController? controller = GetOrCreateController();
        controller?.StartRandomLoop(lines, intervalSeconds, spawnArea);
    }

    public static void StartRandomLoop(object owner, string scope, IReadOnlyList<LocString> lines, float intervalSeconds, Rect2 spawnArea)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled)
        {
            return;
        }

        NMoonTextOverlayController? controller = GetOrCreateController();
        controller?.StartRandomLoop(owner, scope, lines, intervalSeconds, spawnArea);
    }

    public static void StopRandomLoop(object owner, string scope)
    {
        NMoonTextOverlayController? controller = GetExistingController();
        controller?.StopRandomLoop(owner, scope);
    }

    public static void StartSequence(IReadOnlyList<MoonTextSequenceEntry> entries)
    {
        if (!LibraryOfRuinaSettings.MoonTextEnabled)
        {
            return;
        }

        NMoonTextOverlayController? controller = GetOrCreateController();
        controller?.StartSequence(entries);
    }

    private static NMoonTextOverlayController? GetExistingController()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room))
        {
            _currentController = null;
            return null;
        }

        if (_currentController != null
            && GodotObject.IsInstanceValid(_currentController)
            && ReferenceEquals(_currentController.CombatRoom, room))
        {
            return _currentController;
        }

        _currentController = room.CombatVfxContainer.GetNodeOrNull<NMoonTextOverlayController>(NMoonTextOverlayController.ControllerNodeName);
        _currentController?.BindRoom(room);
        return _currentController;
    }

    private static NMoonTextOverlayController? GetOrCreateController()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null || !GodotObject.IsInstanceValid(room))
        {
            _currentController = null;
            return null;
        }

        if (_currentController != null
            && GodotObject.IsInstanceValid(_currentController)
            && ReferenceEquals(_currentController.CombatRoom, room))
        {
            return _currentController;
        }

        _currentController = room.CombatVfxContainer.GetNodeOrNull<NMoonTextOverlayController>(NMoonTextOverlayController.ControllerNodeName);
        if (_currentController == null)
        {
            _currentController = new NMoonTextOverlayController
            {
                Name = NMoonTextOverlayController.ControllerNodeName
            };
            room.CombatVfxContainer.AddChildSafely(_currentController);
        }

        _currentController.BindRoom(room);
        return _currentController;
    }
}

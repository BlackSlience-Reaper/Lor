using Godot;

namespace LibraryOfRuina.backgrounds.ForsakenMurderer;

internal class ForsakenMurdererFearBackgroundOverlay
{
    private const string OverlayNodeName = "FearOverlay";
    private const string FilterNodeName = "Filter";
    private static bool _overlayVisible;

    private ForsakenMurdererFearBackgroundOverlay() { }

    public static void SetOverlayVisible(bool visible)
    {
        _overlayVisible = visible;
        ApplyVisibility();
    }

    private static void ApplyVisibility()
    {
        if (Engine.GetMainLoop() is not SceneTree sceneTree)
        {
            return;
        }

        ApplyVisibilityRecursive(sceneTree.Root);
    }

    private static void ApplyVisibilityRecursive(Node node)
    {
        if (node.Name.ToString() == OverlayNodeName && node is CanvasItem overlay)
        {
            overlay.Visible = _overlayVisible;

            if (FindCanvasItem(node, FilterNodeName) is CanvasItem filter)
            {
                filter.Visible = _overlayVisible;
            }
        }

        foreach (Node child in node.GetChildren())
        {
            ApplyVisibilityRecursive(child);
        }
    }

    private static CanvasItem? FindCanvasItem(Node node, string name)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.Name.ToString() == name && child is CanvasItem canvasItem)
            {
                return canvasItem;
            }

            CanvasItem? nested = FindCanvasItem(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}

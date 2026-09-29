using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace LibraryOfRuina.core.settings;

public static class ExtNodeTransfer
{
    public static T TransferAllNodes<T>(this T target, string sourceScene) where T : Node
    {
        var source = PreloadManager.Cache.GetScene(sourceScene).Instantiate();
        target.Name = source.Name;

        foreach (var child in source.GetChildren())
        {
            source.RemoveChild(child);
            target.AddChild(child);
            child.Owner = target;
            SetChildrenOwner(target, child);
        }

        source.QueueFree();
        return target;
    }

    private static void SetChildrenOwner(Node owner, Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            child.Owner = owner;
            SetChildrenOwner(owner, child);
        }
    }
}

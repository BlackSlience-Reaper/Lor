using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.features.ftue;

internal static class LibraryOfRuinaFtueTargets
{
    public static Control? ResolveResistanceIconsTarget()
    {
        return ResolvePhysicalResistanceIconsTarget()
            ?? ResolveChaosResistanceIconsTarget()
            ?? FindFirstEnemyNode()?.Hitbox;
    }

    public static Control? ResolvePhysicalResistanceIconsTarget()
    {
        NCreature? creature = FindFirstEnemyNode();
        if (creature == null)
            return null;

        return ResolveHitboxGroup(
                creature,
                "LibraryOfRuinaPhysicalResistIcons",
                "PhysicalResistHitbox_Slash",
                "PhysicalResistHitbox_Pierce",
                "PhysicalResistHitbox_Blunt")
            ?? creature.Hitbox;
    }

    public static Control? ResolveChaosResistanceIconsTarget()
    {
        NCreature? creature = FindFirstEnemyNode(static node =>
            FindControl(node, "LibraryOfRuinaChaosResistIcons") != null);
        if (creature == null)
            return null;

        return ResolveHitboxGroup(
                creature,
                "LibraryOfRuinaChaosResistIcons",
                "ChaosResistHitbox_Slash",
                "ChaosResistHitbox_Pierce",
                "ChaosResistHitbox_Blunt")
            ?? creature.Hitbox;
    }

    public static Control? ResolveStaggerBarTarget()
    {
        NCreature? creature = FindFirstEnemyNode(static node =>
            FindControl(node, "LibraryOfRuinaStaggerBarContainer") != null);

        creature ??= FindFirstEnemyNode();
        if (creature == null)
            return null;

        return FindControl(creature, "LibraryOfRuinaStaggerBarContainer")
            ?? FindControl(creature, "StaggerValueLabel")
            ?? ResolveChaosResistanceIconsTarget()
            ?? ResolvePhysicalResistanceIconsTarget()
            ?? creature.Hitbox;
    }

    private static NCreature? FindFirstEnemyNode(Func<NCreature, bool>? predicate = null)
    {
        return NCombatRoom.Instance?.CreatureNodes
            .OfType<NCreature>()
            .Where(static node => node.Entity.IsEnemy && node.Entity.IsAlive)
            .Where(node => predicate?.Invoke(node) ?? true)
            .OrderBy(static node => node.GlobalPosition.X)
            .FirstOrDefault();
    }

    private static Control? ResolveHitboxGroup(Node root, string containerName, params string[] hitboxNames)
    {
        Control? container = FindControl(root, containerName);
        if (container == null)
            return null;

        Control? firstHitbox = null;
        Rect2? combinedRect = null;
        foreach (string hitboxName in hitboxNames)
        {
            Control? hitbox = FindControl(container, hitboxName);
            if (hitbox == null || !hitbox.IsVisibleInTree())
                continue;

            firstHitbox ??= hitbox;
            Rect2 rect = hitbox.GetGlobalRect();
            combinedRect = combinedRect.HasValue ? combinedRect.Value.Merge(rect) : rect;
        }

        if (firstHitbox == null || combinedRect == null)
            return container;

        var target = container.GetNodeOrNull<Control>("FtueTargetGroup");
        if (target == null)
        {
            target = new Control
            {
                Name = "FtueTargetGroup",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 1,
            };
            container.AddChild(target);
        }

        Rect2 localRect = new(
            container.GetGlobalTransformWithCanvas().AffineInverse() * combinedRect.Value.Position,
            combinedRect.Value.Size);
        target.Position = localRect.Position;
        target.Size = localRect.Size;
        return target;
    }

    private static Control? FindControl(Node root, string nodeName)
    {
        if (root is Control control && root.Name == nodeName)
            return control;

        foreach (Node child in root.GetChildren())
        {
            Control? found = FindControl(child, nodeName);
            if (found != null)
                return found;
        }

        return null;
    }
}

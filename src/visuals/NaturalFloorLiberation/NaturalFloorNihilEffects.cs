using System;
using System.Linq;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.visuals.NaturalFloorLiberation;

internal static class NaturalFloorNihilEffects
{
    internal const string ScenePath = "res://scenes/vfx/natural_floor_nihil_attack.tscn";
    private const float FadeSeconds = 0.22f; // 专属攻击特效：结算后淡出的时间。
    private const float SwordScale = 0.42f; // 绝望与正义招式：飞剑特效的场景显示倍率。

    internal static IDisposable? Start(NaturalFloorNihilAction action, Creature owner, IReadOnlyList<Creature> targets)
    {
        bool magic = action is NaturalFloorNihilAction.BossMagic or NaturalFloorNihilAction.LoveMagic;
        bool sword = action is NaturalFloorNihilAction.HeartPierce or NaturalFloorNihilAction.HeartSplit
            or NaturalFloorNihilAction.HeartDestroy or NaturalFloorNihilAction.JusticeProtect
            or NaturalFloorNihilAction.JusticeDefend or NaturalFloorNihilAction.JusticeGuard;
        if ((!magic && !sword) || owner.GetCreatureNode()?.Visuals is not { } visuals
            || NCombatRoom.Instance?.CombatVfxContainer is not { } host)
        {
            return null;
        }

        var root = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Node2D>();
        host.AddChild(root);
        root.GlobalPosition = visuals.GetNode<Node2D>("%CenterPos").GlobalPosition;
        Creature? target = targets.FirstOrDefault(creature => creature.IsAlive);
        Vector2 destination = target?.GetCreatureNode()?.Visuals.GetNode<Node2D>("%CenterPos").GlobalPosition
            ?? root.GlobalPosition;
        Vector2 offset = root.ToLocal(destination);
        var lease = new EffectLease(root);

        if (magic)
        {
            Sprite2D ring = root.GetNode<Sprite2D>("MagicRing");
            Line2D beam = root.GetNode<Line2D>("Beam");
            ring.Visible = true;
            beam.Visible = true;
            beam.Points = [Vector2.Zero, offset];
            root.GetNode<AnimationPlayer>("AnimationPlayer").Play("Magic");
            lease.Loop = LocalOggLoopPlayer.StartLoop(NaturalFloorNihilMonster.SoundPath("MagicalGirl_LaserLoop"));
        }
        else
        {
            Sprite2D projectile = root.GetNode<Sprite2D>("Sword");
            projectile.Visible = true;
            projectile.Scale = Vector2.One * SwordScale;
            projectile.Rotation = offset.Angle() + Mathf.Pi;
            root.CreateTween().TweenProperty(projectile, "position", offset, NaturalFloorNihilVisuals.HitTime);
        }

        return lease;
    }

    private sealed class EffectLease(Node2D root) : IDisposable
    {
        internal LocalOggLoopPlayer.LoopHandle? Loop { get; set; }

        public void Dispose()
        {
            Loop?.Dispose();
            Loop = null;
            if (!GodotObject.IsInstanceValid(root) || !root.IsInsideTree())
            {
                return;
            }

            Tween fade = root.CreateTween();
            fade.TweenProperty(root, "modulate:a", 0f, FadeSeconds);
            fade.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(root))
                {
                    root.QueueFree();
                }
            }));
        }
    }
}

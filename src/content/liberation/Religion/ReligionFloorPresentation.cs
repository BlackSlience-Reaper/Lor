using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.liberation.Religion;

internal static class ReligionFloorPresentation
{
    private const float HeldCardScale = 0.18f; // 敬畏：翼节点上卡牌的显示比例。
    private const float BaseCardFlightSeconds = 1f; // 敬畏：原始飞向翼节点动画的秒数。
    private const float CardFlightSpeedMultiplier = 1.5f; // 敬畏：飞行与缩小速度提高 50%。
    private const float CardFlightSeconds = BaseCardFlightSeconds / CardFlightSpeedMultiplier; // 敬畏：加速后的飞行与缩小时长。
    private const float CrownScale = 0.7f; // 荆棘之冠：每名玩家头顶表徵的比例。
    private const float HeldCardHoverScale = 0.375f; // 敬畏：悬停时原卡牌的放大比例。
    private const float HeldCardHoverSeconds = 0.15f; // 敬畏：悬停放大与移出复原的动画秒数。
    private const int HeldCardHoverZOffset = 10; // 敬畏：悬停卡牌相对原层级的显示提升。
    private static readonly Dictionary<CardModel, HeldCardPresentation> HeldNodes = [];

    private sealed class HeldCardPresentation(NCard node, Tween? tween, Control? hitbox)
    {
        internal NCard Node { get; } = node;

        internal Tween? Tween { get; } = tween;

        internal Control? Hitbox { get; } = hitbox;

        internal Tween? HoverTween { get; set; }

        internal bool IsHeld { get; set; } = true;

        internal int OriginalZIndex { get; } = node.ZIndex;
    }

    internal static void PlayAttackStart(ReligionFloorMonster monster, string animation) =>
        PresentationGuard.Run(() =>
        {
            string? sound = monster switch
            {
                ReligionFloorScytheApostle => animation == "Slash"
                    ? ReligionFloorAssets.ScytheSlashSfx
                    : ReligionFloorAssets.ScytheStrikeSfx,
                ReligionFloorSpearApostle => ReligionFloorAssets.SpearAttackSfx,
                ReligionFloorStaffApostle => ReligionFloorAssets.StaffAttackSfx,
                ReligionFloorLostParadise => ReligionFloorAssets.ParadiseChargeSfx,
                _ => null
            };
            if (sound != null)
            {
                LocalOggOneShotPlayer.Play(sound);
            }
        }, "Religion attack sound");

    internal static Task PlayAttackImpact(ReligionFloorMonster monster)
    {
        if (monster is ReligionFloorLostParadise)
        {
            PresentationGuard.Run(
                () => LocalOggOneShotPlayer.Play(ReligionFloorAssets.ParadiseFireSfx),
                "Religion attack impact sound");
        }
        return Task.CompletedTask;
    }

    internal static void Refresh(ReligionFloorLiberationEncounter encounter) =>
        PresentationGuard.Run(() => RefreshCore(encounter), "Religion background");

    private static void RefreshCore(ReligionFloorLiberationEncounter encounter)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null)
        {
            return;
        }

        if (room.FindChild("ReligionBound", true, false) is CanvasItem bound)
        {
            bound.Visible = encounter.Phase == 1;
        }
        if (room.FindChild("ReligionUnbound", true, false) is CanvasItem unbound)
        {
            unbound.Visible = encounter.Phase == 2;
        }
    }

    internal static void HoldCard(ReligionFloorLiberationEncounter encounter, CardModel card) =>
        PresentationGuard.Run(() => HoldCardCore(encounter, card), "Religion held card");

    private static void HoldCardCore(ReligionFloorLiberationEncounter encounter, CardModel card)
    {
        if (NCombatRoom.Instance is not { } room || encounter.Boss?.Creature.GetCreatureNode()?.Visuals is not { } visuals)
        {
            return;
        }

        NCard? node = NCard.FindOnTable(card);
        if (node == null)
        {
            node = NCard.Create(card);
            if (node == null)
            {
                return;
            }
            room.Ui.AddToPlayContainer(node);
        }

        ReleaseCard(card);
        if (!LocalContext.IsMe(card.Owner))
        {
            node.Visible = false;
            HeldNodes[card] = new HeldCardPresentation(node, null, null);
            return;
        }

        int index = encounter.WingFor(card);
        Marker2D? marker = visuals.GetNodeOrNull<Marker2D>($"Wing{index + 1}");
        if (marker == null)
        {
            return;
        }

        if (node.GetParent() != room.Ui.PlayContainer)
        {
            node.Reparent(room.Ui.PlayContainer, keepGlobalTransform: true);
        }
        Vector2 screenPosition = marker.GetGlobalTransformWithCanvas().Origin;
        Vector2 destination = room.Ui.PlayContainer.GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
        Tween tween = node.CreateTween().SetParallel();
        tween.TweenProperty(node, "position", destination, CardFlightSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(node, "scale", Vector2.One * HeldCardScale, CardFlightSeconds);
        node.MouseFilter = Control.MouseFilterEnum.Ignore;
        var hitbox = new Control
        {
            Name = "HeldCardHoverArea",
            Position = -NCard.defaultSize / 2f,
            Size = NCard.defaultSize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        node.AddChild(hitbox);
        var presentation = new HeldCardPresentation(node, tween, hitbox);
        tween.Finished += () => hitbox.MouseFilter = Control.MouseFilterEnum.Stop;
        hitbox.MouseEntered += () => SetHeldCardHovered(presentation, true);
        hitbox.MouseExited += () => SetHeldCardHovered(presentation, false);
        HeldNodes[card] = presentation;
        node.TreeExiting += () => HeldNodes.Remove(card);
    }

    private static void SetHeldCardHovered(HeldCardPresentation entry, bool hovered)
    {
        if (!entry.IsHeld || !GodotObject.IsInstanceValid(entry.Node) || !entry.Node.IsVisibleInTree())
        {
            return;
        }

        entry.HoverTween?.Kill();
        entry.Node.ZIndex = hovered ? entry.OriginalZIndex + HeldCardHoverZOffset : entry.OriginalZIndex;
        entry.HoverTween = entry.Node.CreateTween();
        entry.HoverTween.TweenProperty(
                entry.Node, "scale", Vector2.One * (hovered ? HeldCardHoverScale : HeldCardScale), HeldCardHoverSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private static void ResetHeldCardScale(HeldCardPresentation entry)
    {
        entry.HoverTween?.Kill();
        entry.HoverTween = null;
        if (GodotObject.IsInstanceValid(entry.Node))
        {
            entry.Node.Scale = Vector2.One * HeldCardScale;
            entry.Node.ZIndex = entry.OriginalZIndex;
        }
    }

    internal static void UpdateWingPositions(ReligionFloorLiberationEncounter encounter)
    {
        if (NCombatRoom.Instance is not { } room || encounter.Boss?.Creature.GetCreatureNode()?.Visuals is not { } visuals)
        {
            return;
        }
        foreach (CardModel card in encounter.HeldCards)
        {
            if (!HeldNodes.TryGetValue(card, out var entry) || !GodotObject.IsInstanceValid(entry.Node)
                || !LocalContext.IsMe(card.Owner) || entry.Tween?.IsRunning() == true)
            {
                continue;
            }
            if (visuals.GetNodeOrNull<Marker2D>($"Wing{encounter.WingFor(card) + 1}") is { } marker)
            {
                entry.Node.Position = room.Ui.PlayContainer.GetGlobalTransformWithCanvas().AffineInverse()
                    * marker.GetGlobalTransformWithCanvas().Origin;
                entry.Node.Visible = !encounter.IsSettling;
                if (encounter.IsSettling)
                {
                    ResetHeldCardScale(entry);
                }
            }
        }
    }

    internal static void ReleaseCard(CardModel card) =>
        PresentationGuard.Run(() => ReleaseCardCore(card), "Religion released card");

    private static void ReleaseCardCore(CardModel card)
    {
        if (!HeldNodes.Remove(card, out var entry))
        {
            return;
        }
        entry.IsHeld = false;
        entry.Tween?.Kill();
        ResetHeldCardScale(entry);
        if (entry.Hitbox is { } hitbox && GodotObject.IsInstanceValid(hitbox))
        {
            hitbox.MouseFilter = Control.MouseFilterEnum.Ignore;
            hitbox.QueueFree();
        }
        if (GodotObject.IsInstanceValid(entry.Node))
        {
            entry.Node.Visible = true;
            entry.Node.MouseFilter = Control.MouseFilterEnum.Stop;
        }
    }

    internal static Task ShowSalvation(ReligionFloorLiberationEncounter encounter) =>
        PresentationGuard.RunAsync(() => ShowSalvationCore(encounter), "Religion salvation");

    private static async Task ShowSalvationCore(ReligionFloorLiberationEncounter encounter)
    {
        if (NCombatRoom.Instance is not { } room)
        {
            return;
        }
        PackedScene scene = ResourceLoader.Load<PackedScene>(ReligionFloorAssets.PresentationScene);
        Node2D effect = scene.Instantiate<Node2D>();
        room.CombatVfxContainer.AddChild(effect);
        Creature[] players = encounter.Boss!.Creature.CombatState!.PlayerCreatures.Where(static player => player.IsAlive).ToArray();
        Vector2[] positions = players.Select(static player => player.GetCreatureNode()?.GlobalPosition ?? Vector2.Zero).ToArray();
        if (positions.Length > 0)
        {
            effect.GlobalPosition = new Vector2(positions.Average(static position => position.X), positions.Min(static position => position.Y) - 280f);
        }
        effect.GetNode<AnimationPlayer>("AnimationPlayer").Play("Salvation");
        await Cmd.Wait(ReligionFloorRules.FrameSeconds);
        foreach (Creature player in players)
        {
            if (player.GetCreatureNode()?.Visuals is not { } visuals)
            {
                continue;
            }
            var crown = new Sprite2D
            {
                Name = "CrownOfThorns",
                Texture = ResourceLoader.Load<Texture2D>("res://images/monsters/religion_floor_liberation/crown_effect.png"),
                Scale = Vector2.One * CrownScale,
                Position = (visuals.GetNodeOrNull<Marker2D>("%IntentPos")?.Position ?? Vector2.Up * 200f) + Vector2.Down * 35f
            };
            visuals.AddChild(crown);
        }
    }

    internal static void ShowRepentance(Creature boss) =>
        PresentationGuard.Run(() => ShowRepentanceCore(boss), "Religion repentance");

    private static void ShowRepentanceCore(Creature boss)
    {
        if (boss.GetCreatureNode() is { Visuals: ReligionFloorLostParadiseVisuals visuals } node)
        {
            // 赎罪时与玩家站在同一地面高度，保留失乐园原有横向位置。
            var groundNode = boss.CombatState?.PlayerCreatures
                .Select(static player => player.GetCreatureNode())
                .FirstOrDefault(static creatureNode => creatureNode != null && GodotObject.IsInstanceValid(creatureNode));
            if (groundNode != null)
            {
                node.GlobalPosition = new Vector2(node.GlobalPosition.X, groundNode.GlobalPosition.Y);
            }

            node.IntentContainer.Visible = false;
            visuals.Modulate = Colors.White;
            visuals.Visible = true;
            visuals.TryPlayTrigger("Idle");
        }
    }
}

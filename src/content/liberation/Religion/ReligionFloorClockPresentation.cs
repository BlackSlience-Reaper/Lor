using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.liberation.Religion;

internal static class ReligionFloorClockPresentation
{
    private const float ClockStepDegrees = 30f; // 原版死亡钟表：每死亡一名使徒顺时针推进一格。
    private const float ClockTickSeconds = 1f; // 原版时针曲线在一秒时完成跳格并播放钟声。
    private const float ReferenceWidth = 1920f; // 原版钟表 UI 的参考画布宽度。
    private const float ReferenceHeight = 1080f; // 原版钟表 UI 的参考画布高度。

    internal static Task Play(int previousKills, int steps) =>
        PresentationGuard.RunAsync(() => PlayCore(previousKills, steps), "Religion apostle clock");

    private static async Task PlayCore(int previousKills, int steps)
    {
        if (steps <= 0 || NCombatRoom.Instance is not { } room)
        {
            return;
        }

        PackedScene scene = ResourceLoader.Load<PackedScene>(ReligionFloorAssets.ClockScene);
        CanvasLayer clock = scene.Instantiate<CanvasLayer>();
        room.AddChild(clock);
        try
        {
            Node2D visuals = clock.GetNode<Node2D>("Visuals");
            Node2D hand = visuals.GetNode<Node2D>("Clock/HandStart");
            AnimationPlayer player = clock.GetNode<AnimationPlayer>("AnimationPlayer");
            float stepSeconds = (float)player.GetAnimation("Step").Length;
            for (int step = 0; step < steps; step++)
            {
                if (!GodotObject.IsInstanceValid(room) || !GodotObject.IsInstanceValid(clock)
                    || !room.IsInsideTree())
                {
                    break;
                }

                Vector2 viewport = room.GetViewportRect().Size;
                visuals.Position = viewport / 2f;
                visuals.Scale = Vector2.One * Mathf.Min(viewport.X / ReferenceWidth, viewport.Y / ReferenceHeight);
                hand.RotationDegrees = (previousKills + step) * ClockStepDegrees;
                player.Stop();
                player.Play("Step");
                player.Advance(0);
                LocalOggOneShotPlayer.Play(ReligionFloorAssets.ClockTickSfx);
                await Cmd.Wait(ClockTickSeconds);
                if (!GodotObject.IsInstanceValid(clock) || !clock.IsInsideTree())
                {
                    break;
                }

                LocalOggOneShotPlayer.Play(ReligionFloorAssets.ClockBellSfx);
                await Cmd.Wait(stepSeconds - ClockTickSeconds);
            }
        }
        finally
        {
            if (GodotObject.IsInstanceValid(clock))
            {
                clock.QueueFree();
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.TestSupport;
using static LibraryOfRuina.framework.visuals.common.VfxPrimitives;

namespace LibraryOfRuina.content.liberation.Philosophy;

// Surveillance：全屏压暗并铺满大鸟眼睛贴图，0.48 秒后淡出；没有伤害回调。
internal static partial class PhilosophyFloorLiberationVfx
{
    internal static async Task PlaySurveillanceDarknessAsync(
        Creature attacker)
    {
        AttackVfxSnapshot snapshot = Capture(attacker, []);
        LocalOggOneShotPlayer.Play(SurveillanceSfxPath, -2f);
        if (TestMode.IsOn
            || !TryCreateRoot(
                "PhilosophyTwilightSurveillance",
                snapshot,
                out Node2D root))
        {
            return;
        }

        Texture2D? eyesTexture = LoadTexture(SurveillanceEyesPath);
        if (eyesTexture == null)
        {
            root.QueueFree();
            return;
        }

        Rect2 viewportRect = root.GetViewportRect();
        Vector2 topLeft = root.ToLocal(viewportRect.Position);
        Vector2 bottomRight = root.ToLocal(viewportRect.End);
        Vector2 overlaySize = new(
            Math.Abs(bottomRight.X - topLeft.X),
            Math.Abs(bottomRight.Y - topLeft.Y));
        var darkness = new ColorRect
        {
            Name = "SurveillanceDarkness",
            Position = topLeft,
            Size = overlaySize,
            Color = new Color(0f, 0f, 0f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1
        };
        var eyes = new TextureRect
        {
            Name = "SurveillanceEyes",
            Position = topLeft,
            Size = overlaySize,
            Texture = eyesTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            FlipH = snapshot.Direction > 0f,
            Modulate = new Color(1f, 0.38f, 0.08f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 2
        };
        root.AddChildSafely(darkness);
        root.AddChildSafely(eyes);

        Tween reveal = root.CreateTween();
        reveal.SetParallel();
        reveal.TweenProperty(
            darkness,
            "color",
            new Color(0f, 0f, 0f, 0.88f),
            0.20f);
        reveal.TweenProperty(eyes, "modulate:a", 0.82f, 0.26f);
        if (!await Wait(root, 0.48))
        {
            QueueFree(root);
            return;
        }
        Tween fade = root.CreateTween();
        fade.SetParallel();
        fade.TweenProperty(darkness, "color:a", 0f, 0.30f);
        fade.TweenProperty(eyes, "modulate:a", 0f, 0.30f);
        await Wait(root, 0.32);
        QueueFree(root);
    }
}

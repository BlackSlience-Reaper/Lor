using Godot;

namespace LibraryOfRuina.framework.visuals;

/// <summary>
/// “动画效果”低档：逐帧换图外观的待机图与动作图之间淡入淡出。
/// <list type="bullet">
/// <item>不改各外观的换图逻辑（换图配置的 Tween、场景动画的 visible 轨道都照旧瞬间切换），只在旁边盯着两张贴图：
/// 某张图出现或换了贴图就从透明淡入；某张图消失或换了贴图，就让预先建好的影子替身显示旧图、旧位置，再淡出。</item>
/// <item>检测放在每帧绘制前（<c>RenderingServer.FramePreDraw</c>）：换图配置的 Tween 回调在节点 _Process 之后才跑，
/// 在 _Process 里检测会先露出一帧不透明的新图。替身在挂上时就建好，绘制前只改属性、不增删节点。</item>
/// <item>只动贴图的 SelfModulate 透明度；Modulate 留给场景动画轨道和受击泛红等其它效果。</item>
/// </list>
/// 只是本机表现，不参与同步。
/// </summary>
internal sealed partial class SpriteCrossfade : Node
{
    internal const float FadeSeconds = 0.12f;

    private sealed class Watch
    {
        internal required Sprite2D Sprite;
        internal required Sprite2D Shadow;
        internal bool Visible;
        internal Texture2D? Texture;
        internal Transform2D Transform;
        internal Vector2 Offset;
        internal bool FlipH;
        internal bool Centered;
        internal Color Modulate;
        internal Tween? FadeIn;
        internal Tween? FadeOut;
    }

    private Watch[] _watches = [];

    /// <summary>给外观挂上淡入淡出；替身放在各自贴图的紧上面，与贴图同父节点、同层级。</summary>
    internal static void Attach(Node owner, params Sprite2D?[] sprites)
    {
        var crossfade = new SpriteCrossfade { Name = "SpriteCrossfade" };
        var watches = new System.Collections.Generic.List<Watch>();
        foreach (Sprite2D? sprite in sprites)
        {
            if (sprite?.GetParent() is not { } parent)
            {
                continue;
            }

            var shadow = new Sprite2D
            {
                Name = sprite.Name + "CrossfadeShadow",
                Visible = false,
                ZIndex = sprite.ZIndex,
                ZAsRelative = sprite.ZAsRelative,
            };
            parent.AddChild(shadow);
            parent.MoveChild(shadow, sprite.GetIndex() + 1);
            var watch = new Watch { Sprite = sprite, Shadow = shadow };
            Snapshot(watch);
            watches.Add(watch);
        }

        crossfade._watches = watches.ToArray();
        owner.AddChild(crossfade);
    }

    public override void _EnterTree() => RenderingServer.FramePreDraw += OnFramePreDraw;

    public override void _ExitTree() => RenderingServer.FramePreDraw -= OnFramePreDraw;

    private void OnFramePreDraw()
    {
        foreach (Watch watch in _watches)
        {
            if (!IsInstanceValid(watch.Sprite) || !IsInstanceValid(watch.Shadow))
            {
                continue;
            }

            Sprite2D sprite = watch.Sprite;
            bool visible = sprite.Visible;
            bool changed = visible != watch.Visible || (visible && sprite.Texture != watch.Texture);
            if (changed)
            {
                if (watch.Visible && watch.Texture != null)
                {
                    FadeOutShadow(watch);
                }

                if (visible)
                {
                    FadeInSprite(watch);
                }
            }

            Snapshot(watch);
        }
    }

    private static void Snapshot(Watch watch)
    {
        Sprite2D sprite = watch.Sprite;
        watch.Visible = sprite.Visible;
        watch.Texture = sprite.Texture;
        watch.Transform = sprite.Transform;
        watch.Offset = sprite.Offset;
        watch.FlipH = sprite.FlipH;
        watch.Centered = sprite.Centered;
        watch.Modulate = sprite.Modulate;
    }

    // 替身显示上一帧的贴图与摆放，从贴图消失前的透明度淡到 0
    private void FadeOutShadow(Watch watch)
    {
        Sprite2D shadow = watch.Shadow;
        watch.FadeOut?.Kill();
        shadow.Texture = watch.Texture;
        shadow.Transform = watch.Transform;
        shadow.Offset = watch.Offset;
        shadow.FlipH = watch.FlipH;
        shadow.Centered = watch.Centered;
        shadow.Modulate = watch.Modulate;
        shadow.Material = watch.Sprite.Material;
        Color start = watch.Sprite.SelfModulate;
        shadow.SelfModulate = start;
        shadow.Visible = true;
        watch.FadeOut = CreateTween();
        watch.FadeOut.TweenProperty(shadow, "self_modulate:a", 0f, FadeSeconds);
        watch.FadeOut.TweenCallback(Callable.From(() => shadow.Visible = false));
    }

    private void FadeInSprite(Watch watch)
    {
        Sprite2D sprite = watch.Sprite;
        watch.FadeIn?.Kill();
        Color color = sprite.SelfModulate;
        sprite.SelfModulate = color with { A = 0f };
        watch.FadeIn = CreateTween();
        watch.FadeIn.TweenProperty(sprite, "self_modulate:a", 1f, FadeSeconds);
    }
}

using System.Collections.Generic;
using Godot;
using HarmonyLib;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.features.creatureglow;

/// <summary>
/// 本模组战斗里给敌人贴图加一圈沿轮廓的微弱外发光，把怪物从细节繁杂的整幅插画背景里分出来。
/// 每张怪物 Sprite2D 下挂一个同贴图、画在父节点身后的子 Sprite2D，着色器把四边外扩后按透明度向外扩散。
/// 颜色沿用怪物身后那块背景的色相，只把明度往反方向推一点；背景越暗、越花，光越实。纯表现层，不参与任何同步状态。
/// Spine 骨骼动画的怪物（原版、玩家）不处理：网格只覆盖贴图本身，外扩不出去。
/// </summary>
internal sealed partial class CreatureOutlineGlow : Node
{
    private const string NodeName = "LorCreatureOutlineGlow";
    private const string GlowName = "LorOutlineGlow";
    // 屏幕上的发光宽度（像素），按贴图实际缩放换算成贴图像素。
    private const float ScreenRadius = 20f;
    private const double RefreshInterval = 0.5;

    private const string ShaderCode = """
        shader_type canvas_item;
        render_mode blend_mix;

        uniform vec4 glow_color : source_color = vec4(1.0);
        uniform float radius = 12.0;
        uniform vec4 quad_rect;
        uniform vec4 uv_rect;
        uniform vec2 uv_flip = vec2(1.0);

        void vertex() {
            vec2 dir = sign(VERTEX - (quad_rect.xy + quad_rect.zw * 0.5));
            VERTEX += dir * radius;
            UV += dir * uv_flip * radius * TEXTURE_PIXEL_SIZE;
        }

        float alpha_at(sampler2D tex, vec2 uv) {
            if (uv.x < uv_rect.x || uv.y < uv_rect.y || uv.x > uv_rect.z || uv.y > uv_rect.w) {
                return 0.0;
            }
            return texture(tex, uv).a;
        }

        void fragment() {
            // 三圈采样按近重远轻加权平均，近似高斯模糊，没有硬边。
            float sum = 0.0;
            for (int i = 0; i < 16; i++) {
                float angle = float(i) * 0.39269908 + 0.19634954;
                vec2 d = vec2(cos(angle), sin(angle)) * TEXTURE_PIXEL_SIZE * radius;
                sum += alpha_at(TEXTURE, UV + d * 0.25) * 1.0;
                sum += alpha_at(TEXTURE, UV + d * 0.55) * 0.6;
                sum += alpha_at(TEXTURE, UV + d) * 0.3;
            }
            float glow = sum / (16.0 * 1.9);
            glow = 1.0 - (1.0 - glow) * (1.0 - glow);
            COLOR = vec4(glow_color.rgb, glow_color.a * glow);
        }
        """;

    private static readonly Dictionary<ulong, Image?> ImageCache = new();
    private static Shader? _shader;

    private readonly List<(Sprite2D Source, Sprite2D Glow, ShaderMaterial Material)> _glows = [];
    private NCreatureVisuals _visuals = null!;
    private double _sinceRefresh = RefreshInterval;
    private Color _color = new(1f, 1f, 1f, 0f);

    internal static void Attach(NCreature creature)
    {
        NCreatureVisuals? visuals = creature.Visuals;
        if (visuals == null || visuals.GetNodeOrNull(NodeName) != null)
        {
            return;
        }

        visuals.AddChild(new CreatureOutlineGlow { Name = NodeName, _visuals = visuals });
    }

    public override void _Process(double delta)
    {
        // 贴图节点（含后来换上的攻击姿势等）和背景颜色隔一会儿重新收集一次；贴图帧每帧同步。
        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshInterval)
        {
            _sinceRefresh = 0;
            CollectSprites(_visuals);
            RefreshColor();
        }

        foreach ((Sprite2D source, Sprite2D glow, ShaderMaterial material) in _glows)
        {
            if (GodotObject.IsInstanceValid(source) && GodotObject.IsInstanceValid(glow))
            {
                Sync(source, glow, material);
            }
        }
    }

    private void CollectSprites(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Sprite2D sprite && sprite.Name != GlowName && sprite.GetNodeOrNull(GlowName) == null)
            {
                var material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode } };
                var glow = new Sprite2D
                {
                    Name = GlowName,
                    ShowBehindParent = true,
                    UseParentMaterial = false,
                    Material = material,
                };
                sprite.AddChild(glow);
                _glows.Add((sprite, glow, material));
                Sync(sprite, glow, material);
                material.SetShaderParameter("glow_color", _color);
            }

            if (child.Name != GlowName)
            {
                CollectSprites(child);
            }
        }
    }

    private static void Sync(Sprite2D source, Sprite2D glow, ShaderMaterial material)
    {
        if (glow.Texture != source.Texture)
        {
            glow.Texture = source.Texture;
        }

        glow.Centered = source.Centered;
        glow.Offset = source.Offset;
        glow.FlipH = source.FlipH;
        glow.FlipV = source.FlipV;
        glow.Hframes = source.Hframes;
        glow.Vframes = source.Vframes;
        glow.Frame = source.Frame;
        glow.RegionEnabled = source.RegionEnabled;
        glow.RegionRect = source.RegionRect;
        if (source.Texture == null)
        {
            return;
        }

        Rect2 quad = source.GetRect();
        Vector2 texSize = source.Texture.GetSize();
        Rect2 uv;
        if (source.RegionEnabled)
        {
            uv = new Rect2(source.RegionRect.Position / texSize, source.RegionRect.Size / texSize);
        }
        else
        {
            Vector2 frameSize = new(1f / source.Hframes, 1f / source.Vframes);
            uv = new Rect2(new Vector2(source.FrameCoords.X, source.FrameCoords.Y) * frameSize, frameSize);
        }

        float scale = Mathf.Max(0.0001f, source.GetGlobalTransformWithCanvas().Scale.Abs().X);
        material.SetShaderParameter("radius", ScreenRadius / scale);
        material.SetShaderParameter("quad_rect", new Vector4(quad.Position.X, quad.Position.Y, quad.Size.X, quad.Size.Y));
        material.SetShaderParameter("uv_rect", new Vector4(uv.Position.X, uv.Position.Y, uv.End.X, uv.End.Y));
        material.SetShaderParameter("uv_flip", new Vector2(source.FlipH ? -1f : 1f, source.FlipV ? -1f : 1f));
    }

    private void RefreshColor()
    {
        if (_visuals.Bounds is not { } bounds
            || FindBackgroundRect() is not { Texture: { } texture } rect
            || Sample(rect, texture, bounds.GetGlobalRect()) is not { } sample)
        {
            return;
        }

        // 沿用背景本身的色相，只把明度往反方向推一点、饱和度略降：暗背景上是同色系的微光，亮背景上是同色系的浅影。
        Color mean = sample.Mean;
        float luminance = mean.Luminance;
        Color tint = luminance < 0.55f
            ? Color.FromHsv(mean.H, mean.S * 0.7f, Mathf.Clamp(mean.V + 0.42f, 0f, 0.95f))
            : Color.FromHsv(mean.H, Mathf.Min(1f, mean.S * 1.1f), mean.V * 0.55f);
        float contrastNeed = Mathf.Abs(luminance - 0.5f) < 0.2f ? 0.08f : 0f;
        float alpha = Mathf.Clamp(0.32f + (1f - luminance) * 0.22f + sample.Busyness * 0.5f + contrastNeed, 0.3f, 0.62f);
        _color = new Color(tint.R, tint.G, tint.B, alpha);
        foreach ((_, _, ShaderMaterial material) in _glows)
        {
            material.SetShaderParameter("glow_color", _color);
        }
    }

    private static TextureRect? FindBackgroundRect()
    {
        NCombatBackground? background = NCombatRoom.Instance?.Background;
        if (background == null)
        {
            return null;
        }

        foreach (Node slot in background.GetChildren())
        {
            if (slot.Name.ToString().StartsWith("Layer_", System.StringComparison.Ordinal)
                && FindTextureRect(slot) is { } rect)
            {
                return rect;
            }
        }

        return null;
    }

    private static TextureRect? FindTextureRect(Node node)
    {
        // 解放战的动态背景节点挂在原图层下面并盖住它，取最深一层可见的贴图。
        TextureRect? found = node is TextureRect { Visible: true, Texture: not null } self ? self : null;
        foreach (Node child in node.GetChildren())
        {
            if (FindTextureRect(child) is { } deeper)
            {
                found = deeper;
            }
        }

        return found;
    }

    private static (Color Mean, float Busyness)? Sample(TextureRect rect, Texture2D texture, Rect2 globalRegion)
    {
        Image? image = CachedImage(texture);
        if (image == null)
        {
            return null;
        }

        // 把怪物范围换算到背景贴图像素坐标，按“铺满并保持比例”的拉伸方式近似。
        Transform2D toRect = rect.GetGlobalTransformWithCanvas().AffineInverse();
        Vector2 p0 = toRect * globalRegion.Position;
        Vector2 p1 = toRect * globalRegion.End;
        Vector2 texSize = new(image.GetWidth(), image.GetHeight());
        float cover = Mathf.Max(rect.Size.X / texSize.X, rect.Size.Y / texSize.Y);
        Vector2 offset = (rect.Size - texSize * cover) * 0.5f;
        Vector2 a = ((p0 - offset) / cover).Clamp(Vector2.Zero, texSize - Vector2.One);
        Vector2 b = ((p1 - offset) / cover).Clamp(Vector2.Zero, texSize - Vector2.One);
        int x0 = (int)Mathf.Min(a.X, b.X), x1 = (int)Mathf.Max(a.X, b.X);
        int y0 = (int)Mathf.Min(a.Y, b.Y), y1 = (int)Mathf.Max(a.Y, b.Y);
        if (x1 - x0 < 4 || y1 - y0 < 4)
        {
            return null;
        }

        int step = Mathf.Max(2, Mathf.Min(x1 - x0, y1 - y0) / 48);
        float r = 0, g = 0, bl = 0, l = 0, l2 = 0;
        int n = 0;
        for (int y = y0; y <= y1; y += step)
        {
            for (int x = x0; x <= x1; x += step)
            {
                Color c = image.GetPixel(x, y);
                r += c.R;
                g += c.G;
                bl += c.B;
                float lum = c.Luminance;
                l += lum;
                l2 += lum * lum;
                n++;
            }
        }

        float mean = l / n;
        float std = Mathf.Sqrt(Mathf.Max(0f, l2 / n - mean * mean));
        return (new Color(r / n, g / n, bl / n), Mathf.Clamp(std - 0.08f, 0f, 0.25f));
    }

    private static Image? CachedImage(Texture2D texture)
    {
        ulong id = texture.GetInstanceId();
        if (ImageCache.TryGetValue(id, out Image? cached))
        {
            return cached;
        }

        Image? image = texture.GetImage();
        if (image != null && image.IsCompressed())
        {
            image.Decompress();
        }

        if (ImageCache.Count >= 2)
        {
            ImageCache.Clear();
        }

        ImageCache[id] = image;
        return image;
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class CreatureOutlineGlowPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity is not { Side: CombatSide.Enemy } entity
            || !ModOwnership.IsOwn(entity.CombatState?.Encounter))
        {
            return;
        }

        CreatureOutlineGlow.Attach(__instance);
    }
}

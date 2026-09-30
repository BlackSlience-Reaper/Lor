using System;
using System.Linq;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.features.intentgraph;

/// <summary>
/// 意图图的绘制，全部在 _Draw 里直接画。尺寸、贴图区域与绘制顺序照 Intent Graph（Chaofan，创意工坊 3747528152）：
/// 1 格 80 像素，每个意图图标占一格、画成 72×72，数值用 Kreon 粗体 22 号加半透明黑描边写在图标左下；
/// 连线宽 10，起点从节点边缘出发，拐角用拐角贴片，末端留出 15 像素画箭头贴图；
/// 分组框是 3 像素的九宫格；标签的坐标是基线，带 12 像素描边。
/// 本模组的箭头与分组贴图（LibraryOfRuina/intentgraph/images/ui/）与它的贴图布局相同。
/// </summary>
public partial class NIntentGraph : Control
{
    internal const float GridSize = 80f;
    private const float IconSize = 72f;
    private const float IconInset = 4f;
    private const int ValueFontSize = 22;
    private const int ValueOutlineSize = 16;
    private const int LabelOutlineSize = 12;
    private const float LineHalfWidth = 5f;
    private const float ArrowHeadLength = 15f;
    private const float GlowReach = 20f;

    private static readonly Color TextOutline = new Color(0f, 0f, 0f, 0.5f);
    private static readonly Color CurrentMoveGlow = new Color(1f, 1f, 1f, 0.3f);

    // 箭头贴图（128×64）：横线、竖线、四种拐角、四个方向的箭头。
    private static readonly Rect2 ArrowHorizontal = new Rect2(1f, 0f, 62f, 10f);
    private static readonly Rect2 ArrowVertical = new Rect2(65f, 1f, 10f, 62f);
    private static readonly Rect2 CornerDownRight = new Rect2(0f, 11f, 10f, 10f);
    private static readonly Rect2 CornerDownLeft = new Rect2(11f, 11f, 10f, 10f);
    private static readonly Rect2 CornerUpRight = new Rect2(0f, 22f, 10f, 10f);
    private static readonly Rect2 CornerUpLeft = new Rect2(11f, 22f, 10f, 10f);
    private static readonly Rect2 HeadUp = new Rect2(91f, 0f, 20f, 15f);
    private static readonly Rect2 HeadDown = new Rect2(91f, 35f, 20f, 15f);
    private static readonly Rect2 HeadRight = new Rect2(111f, 15f, 15f, 20f);
    private static readonly Rect2 HeadLeft = new Rect2(76f, 15f, 15f, 20f);

    private static Texture2D? _groupBorderTexture;
    private static Texture2D? _arrowTexture;

    private IntentGraphRenderModel _render = new IntentGraphRenderModel();
    private Font? _labelFont;
    private Font? _valueFont;
    private GradientTexture2D? _glowTexture;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        LoadSharedTextures();
    }

    internal void SetRenderModel(IntentGraphRenderModel render)
    {
        _render = render;
        Vector2 size = new Vector2(Math.Max(1f, render.WidthUnits), Math.Max(1f, render.HeightUnits)) * GridSize;
        CustomMinimumSize = size;
        Size = size;
        QueueRedraw();
    }

    public override void _Draw()
    {
        _labelFont ??= IntentGraphFonts.CreateLabelFont();
        _valueFont ??= IntentGraphFonts.CreateValueFont();

        foreach (IntentGraphMoveNode move in _render.Moves.Where(static m => m.IsCurrentMove))
        {
            DrawCurrentMoveGlow(move);
        }

        foreach (IntentGraphMoveNode move in _render.Moves)
        {
            for (int i = 0; i < move.Intents.Count; i++)
            {
                DrawIntent(move.Intents[i], move.PositionUnits + new Vector2(i * IntentGraphLayouter.IconStep, 0f));
            }
        }

        foreach (IntentGraphGroupNode group in _render.Groups)
        {
            DrawGroup(group.RectUnits);
        }

        foreach (IntentGraphArrowPath arrow in _render.Arrows)
        {
            DrawArrow(arrow.PointsUnits);
        }

        foreach (IntentGraphLabelNode label in _render.Labels)
        {
            DrawLabel(label);
        }
    }

    private void DrawIntent(IntentGraphIntentIcon icon, Vector2 cell)
    {
        Vector2 origin = cell * GridSize;
        if (icon.Texture != null)
        {
            // 攻击图标下移 4 像素，给左下角的数值留出位置（与 Intent Graph 相同）。
            float top = icon.IntentType == IntentType.Attack ? IconInset : 0f;
            DrawTextureRect(icon.Texture, new Rect2(origin + new Vector2(IconInset, top), new Vector2(IconSize, IconSize)), false);
        }

        string text = FormatValue(icon.ValueText);
        if (!string.IsNullOrEmpty(text))
        {
            Vector2 baseline = origin + new Vector2(12f, 71f);
            DrawStringOutline(_valueFont!, baseline, text, HorizontalAlignment.Left, -1f, ValueFontSize, ValueOutlineSize, TextOutline);
            DrawString(_valueFont!, baseline, text, HorizontalAlignment.Left, -1f, ValueFontSize);
        }
    }

    private void DrawCurrentMoveGlow(IntentGraphMoveNode move)
    {
        _glowTexture ??= CreateGlowTexture();
        for (int i = 0; i < Math.Max(1, move.Intents.Count); i++)
        {
            Vector2 origin = (move.PositionUnits + new Vector2(i * IntentGraphLayouter.IconStep, 0f)) * GridSize;
            DrawTextureRect(_glowTexture, new Rect2(origin - Vector2.One * GlowReach, Vector2.One * (GridSize + GlowReach * 2f)), false, CurrentMoveGlow);
        }
    }

    // Intent Graph 用一张光晕贴图标出当前招式；本模组没有这张贴图，用径向渐变画同样的柔光。
    private static GradientTexture2D CreateGlowTexture()
    {
        Gradient gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 1f, 1f, 0.9f));
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
        gradient.SetOffset(0, 0.35f);
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 64,
            Height = 64
        };
    }

    private void DrawGroup(Rect2 units)
    {
        if (_groupBorderTexture == null)
        {
            return;
        }

        Vector2 p = units.Position * GridSize;
        Vector2 s = units.Size * GridSize;
        const float b = 3f;
        DrawTextureRectRegion(_groupBorderTexture, new Rect2(p, new Vector2(b, b)), new Rect2(0f, 0f, b, b));
        DrawTextureRectRegion(_groupBorderTexture, new Rect2(p + new Vector2(b, 0f), new Vector2(s.X - 2f * b, b)), new Rect2(b, 0f, 26f, b));
        DrawTextureRectRegion(_groupBorderTexture, new Rect2(p + new Vector2(s.X - b, 0f), new Vector2(b, b)), new Rect2(29f, 0f, b, b));
        DrawTextureRectRegion(_groupBorderTexture, new Rect2(p + new Vector2(0f, b), new Vector2(b, s.Y - 2f * b)), new Rect2(0f, b, b, 26f));
        DrawTextureRectRegion(_groupBorderTexture, new Rect2(p + new Vector2(s.X - b, b), new Vector2(b, s.Y - 2f * b)), new Rect2(29f, b, b, 26f));
        DrawTextureRectRegion(_groupBorderTexture, new Rect2(p + new Vector2(0f, s.Y - b), new Vector2(b, b)), new Rect2(0f, 29f, b, b));
        DrawTextureRectRegion(_groupBorderTexture, new Rect2(p + new Vector2(b, s.Y - b), new Vector2(s.X - 2f * b, b)), new Rect2(b, 29f, 26f, b));
        DrawTextureRectRegion(_groupBorderTexture, new Rect2(p + new Vector2(s.X - b, s.Y - b), new Vector2(b, b)), new Rect2(29f, 29f, b, b));
    }

    /// <summary>
    /// 每段线从上一个拐角之后 5 像素开始，到下一个拐角之前 5 像素结束，拐角处贴拐角贴片；最后一段在终点前 15 像素结束，
    /// 箭头贴图的尖正好落在终点（节点边缘）。方向：0 上、1 右、2 下、3 左。
    /// </summary>
    private void DrawArrow(IReadOnlyList<Vector2> pointsUnits)
    {
        if (_arrowTexture == null || pointsUnits.Count < 2)
        {
            return;
        }

        int previous = -1;
        bool horizontal = true;
        for (int i = 0; i + 1 < pointsUnits.Count; i++)
        {
            Vector2 from = pointsUnits[i] * GridSize;
            Vector2 to = pointsUnits[i + 1] * GridSize;
            bool first = i == 0;
            bool last = i + 2 == pointsUnits.Count;
            Vector2 delta = to - from;
            horizontal = Math.Abs(delta.X) > 0.001f || (Math.Abs(delta.Y) <= 0.001f && !horizontal) || (first && Math.Abs(delta.Y) <= 0.001f);
            int direction;
            if (horizontal)
            {
                bool right = delta.X > 0f;
                direction = right ? 1 : 3;
                float a = from.X + (first ? 0f : right ? LineHalfWidth : -LineHalfWidth);
                float b = to.X + (last ? ArrowHeadLength : LineHalfWidth) * (right ? -1f : 1f);
                if (Math.Abs(a - b) > 0.5f && (b - a) * (right ? 1f : -1f) > 0f)
                {
                    DrawTextureRectRegion(_arrowTexture, new Rect2(Math.Min(a, b), from.Y - LineHalfWidth, Math.Abs(a - b), LineHalfWidth * 2f), ArrowHorizontal);
                }
            }
            else
            {
                bool down = delta.Y > 0f;
                direction = down ? 2 : 0;
                float a = from.Y + (first ? 0f : down ? LineHalfWidth : -LineHalfWidth);
                float b = to.Y + (last ? ArrowHeadLength : LineHalfWidth) * (down ? -1f : 1f);
                if (Math.Abs(a - b) > 0.5f && (b - a) * (down ? 1f : -1f) > 0f)
                {
                    DrawTextureRectRegion(_arrowTexture, new Rect2(from.X - LineHalfWidth, Math.Min(a, b), LineHalfWidth * 2f, Math.Abs(a - b)), ArrowVertical);
                }
            }

            if (!first && CornerRegion(previous, direction) is { } corner)
            {
                DrawTextureRectRegion(_arrowTexture, new Rect2(from - Vector2.One * LineHalfWidth, Vector2.One * LineHalfWidth * 2f), corner);
            }

            previous = direction;
        }

        Vector2 tip = pointsUnits[pointsUnits.Count - 1] * GridSize;
        switch (previous)
        {
            case 0:
                DrawTextureRectRegion(_arrowTexture, new Rect2(tip.X - HeadUp.Size.X / 2f, tip.Y, HeadUp.Size.X, HeadUp.Size.Y), HeadUp);
                break;
            case 1:
                DrawTextureRectRegion(_arrowTexture, new Rect2(tip.X - ArrowHeadLength, tip.Y - HeadRight.Size.Y / 2f, HeadRight.Size.X, HeadRight.Size.Y), HeadRight);
                break;
            case 2:
                DrawTextureRectRegion(_arrowTexture, new Rect2(tip.X - HeadDown.Size.X / 2f, tip.Y - ArrowHeadLength, HeadDown.Size.X, HeadDown.Size.Y), HeadDown);
                break;
            case 3:
                DrawTextureRectRegion(_arrowTexture, new Rect2(tip.X, tip.Y - HeadLeft.Size.Y / 2f, HeadLeft.Size.X, HeadLeft.Size.Y), HeadLeft);
                break;
        }
    }

    private static Rect2? CornerRegion(int from, int to) => (from, to) switch
    {
        (2, 1) or (3, 0) => CornerUpRight,
        (2, 3) or (1, 0) => CornerUpLeft,
        (0, 3) or (1, 2) => CornerDownLeft,
        (0, 1) or (3, 2) => CornerDownRight,
        _ => null
    };

    private void DrawLabel(IntentGraphLabelNode label)
    {
        if (string.IsNullOrEmpty(label.Text))
        {
            return;
        }

        Vector2 position = label.PositionUnits * GridSize;
        float width = _labelFont!.GetStringSize(label.Text, HorizontalAlignment.Left, -1f, label.FontSize).X;
        position.X -= width * label.Align;
        foreach (string line in label.Text.Split('\n'))
        {
            DrawStringOutline(_labelFont, position, line, HorizontalAlignment.Left, -1f, label.FontSize, LabelOutlineSize, TextOutline);
            DrawString(_labelFont, position, line, HorizontalAlignment.Left, -1f, label.FontSize);
            position.Y += label.FontSize + 2f;
        }
    }

    /// <summary>去掉 BBCode；原版各语言分隔伤害与次数的 ×（简中）、х（俄语）统一写成 x，与 Intent Graph 一致。</summary>
    private static string FormatValue(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(text.Length);
        bool inTag = false;
        foreach (char c in text)
        {
            if (c == '[')
            {
                inTag = true;
                continue;
            }

            if (inTag)
            {
                inTag = c != ']';
                continue;
            }

            builder.Append(c is '×' or 'х' ? 'x' : c);
        }

        return builder.ToString().Trim();
    }

    private static void LoadSharedTextures()
    {
        _groupBorderTexture ??= LoadTextureResource("res://LibraryOfRuina/intentgraph/images/ui/groupborder.png");
        _arrowTexture ??= LoadTextureResource("res://LibraryOfRuina/intentgraph/images/ui/arrow.png");
    }

    private static Texture2D? LoadTextureResource(string path)
    {
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        return ResourceLoader.Load<Texture2D>(path);
    }
}

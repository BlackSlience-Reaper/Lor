using System;
using System.Text;
using Godot;

namespace LibraryOfRuina.features.intentgraph;

public partial class NIntentGraph : Control
{
    private const float GridSize = 72f;
    private const float Margin = 8f;
    private const float IconSize = 44f;
    private const float IntentSize = 30f;
    private const float IconSpacing = 38f;

    private static readonly Rect2 ArrowHorizontalRegion = new Rect2(0, 0, 64, 10);
    private static readonly Rect2 ArrowVerticalRegion = new Rect2(65, 0, 10, 64);

    private static Texture2D? _iconTexture;
    private static Texture2D? _groupBorderTexture;
    private static Texture2D? _arrowTexture;

    private IntentGraphRenderModel _render = new IntentGraphRenderModel();

    private readonly List<IReadOnlyList<Vector2>> _arrowPoints = new List<IReadOnlyList<Vector2>>();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        LoadSharedTextures();
    }

    internal void SetRenderModel(IntentGraphRenderModel render)
    {
        _render = render;
        RebuildNodes();
        QueueRedraw();
    }

    public override void _Draw()
    {
        base._Draw();
        DrawArrows();
    }

    private void RebuildNodes()
    {
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }

        _arrowPoints.Clear();

        foreach (IntentGraphGroupNode group in _render.Groups)
        {
            NinePatchRect border = new NinePatchRect
            {
                Texture = _groupBorderTexture,
                PatchMarginLeft = 8,
                PatchMarginTop = 8,
                PatchMarginRight = 8,
                PatchMarginBottom = 8,
                MouseFilter = MouseFilterEnum.Ignore
            };

            Rect2 rect = UnitRectToPixels(group.RectUnits);
            border.Position = rect.Position;
            border.Size = rect.Size;
            AddChild(border);
        }

        foreach (IntentGraphMoveNode move in _render.Moves)
        {
            AddChild(BuildMoveNode(move));
        }

        foreach (IntentGraphLabelNode label in _render.Labels)
        {
            Label text = new Label
            {
                Text = StripBbcode(label.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };

            text.Set("theme_override_font_sizes/font_size", 16);
            text.Set("theme_override_colors/font_color", new Color(0.95f, 0.90f, 0.62f));
            text.Set("theme_override_colors/font_shadow_color", new Color(0f, 0f, 0f, 0.45f));
            text.Set("theme_override_constants/shadow_offset_x", 2);
            text.Set("theme_override_constants/shadow_offset_y", 1);
            Vector2 p = UnitToPixels(label.PositionUnits);
            text.Position = p - new Vector2(100f, 12f);
            text.Size = new Vector2(200f, 24f);
            AddChild(text);
        }

        foreach (IntentGraphArrowPath arrow in _render.Arrows)
        {
            _arrowPoints.Add(arrow.PointsUnits);
        }

        Vector2 desiredSize = new Vector2(
            Math.Max(80f, _render.WidthUnits * GridSize + Margin * 2f),
            Math.Max(80f, _render.HeightUnits * GridSize + Margin * 2f));

        CustomMinimumSize = desiredSize;
        Size = desiredSize;
    }

    private Control BuildMoveNode(IntentGraphMoveNode move)
    {
        int count = Math.Max(1, move.Intents.Count);
        float width = IconSize + Math.Max(0, count - 1) * IconSpacing;
        Control root = new Control
        {
            Size = new Vector2(width, IconSize),
            MouseFilter = MouseFilterEnum.Ignore
        };

        Vector2 center = UnitToPixels(move.PositionUnits);
        root.Position = center - root.Size * 0.5f;
        root.Modulate = move.IsCurrentMove ? Colors.White : new Color(0.9f, 0.9f, 0.9f, 0.95f);

        if (move.Intents.Count == 0)
        {
            TextureRect fallback = new TextureRect
            {
                Texture = _iconTexture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Size = new Vector2(IconSize, IconSize),
                MouseFilter = MouseFilterEnum.Ignore
            };
            root.AddChild(fallback);
            return root;
        }

        for (int i = 0; i < move.Intents.Count; i++)
        {
            IntentGraphIntentIcon icon = move.Intents[i];
            Control iconRoot = new Control
            {
                Position = new Vector2(i * IconSpacing, 0f),
                Size = new Vector2(IconSize, IconSize),
                MouseFilter = MouseFilterEnum.Ignore
            };

            TextureRect frame = new TextureRect
            {
                Texture = _iconTexture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Size = iconRoot.Size,
                MouseFilter = MouseFilterEnum.Ignore
            };
            iconRoot.AddChild(frame);

            if (icon.Texture != null)
            {
                TextureRect intent = new TextureRect
                {
                    Texture = icon.Texture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    Position = new Vector2((IconSize - IntentSize) * 0.5f, (IconSize - IntentSize) * 0.5f - 1f),
                    Size = new Vector2(IntentSize, IntentSize),
                    MouseFilter = MouseFilterEnum.Ignore
                };
                iconRoot.AddChild(intent);
            }

            if (!string.IsNullOrWhiteSpace(icon.ValueText))
            {
                Label value = new Label
                {
                    Text = StripBbcode(icon.ValueText),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Position = new Vector2(-4f, IconSize - 20f),
                    Size = new Vector2(IconSize + 8f, 20f),
                    MouseFilter = MouseFilterEnum.Ignore
                };

                value.Set("theme_override_font_sizes/font_size", 16);
                value.Set("theme_override_colors/font_color", new Color(0.95f, 0.95f, 0.95f));
                value.Set("theme_override_colors/font_shadow_color", new Color(0f, 0f, 0f, 0.65f));
                value.Set("theme_override_constants/shadow_offset_x", 2);
                value.Set("theme_override_constants/shadow_offset_y", 1);
                iconRoot.AddChild(value);
            }

            root.AddChild(iconRoot);
        }

        return root;
    }

    private static string StripBbcode(string? text)
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
                if (c == ']')
                {
                    inTag = false;
                }

                continue;
            }

            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    private void DrawArrows()
    {
        if (_arrowTexture == null)
        {
            return;
        }

        Color color = Colors.White;
        foreach (IReadOnlyList<Vector2> path in _arrowPoints)
        {
            if (path.Count < 2)
            {
                continue;
            }

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 from = UnitToPixels(path[i]);
                Vector2 to = UnitToPixels(path[i + 1]);
                Vector2 diff = to - from;

                if (Math.Abs(diff.X) >= Math.Abs(diff.Y))
                {
                    float x = Math.Min(from.X, to.X);
                    Rect2 dest = new Rect2(x, from.Y - 5f, Math.Abs(diff.X), 10f);
                    if (dest.Size.X > 1f)
                    {
                        DrawTextureRectRegion(_arrowTexture, dest, ArrowHorizontalRegion, color);
                    }
                }
                else
                {
                    float y = Math.Min(from.Y, to.Y);
                    Rect2 dest = new Rect2(from.X - 5f, y, 10f, Math.Abs(diff.Y));
                    if (dest.Size.Y > 1f)
                    {
                        DrawTextureRectRegion(_arrowTexture, dest, ArrowVerticalRegion, color);
                    }
                }
            }

            Vector2 end = UnitToPixels(path[path.Count - 1]);
            Vector2 prev = UnitToPixels(path[path.Count - 2]);
            Vector2 dir = (end - prev).Normalized();
            if (dir.LengthSquared() < 0.001f)
            {
                dir = Vector2.Right;
            }

            Vector2 ortho = new Vector2(-dir.Y, dir.X);
            Color headColor = new Color(0.94f, 0.86f, 0.24f);
            Vector2 p1 = end - dir * 12f + ortho * 6f;
            Vector2 p2 = end - dir * 12f - ortho * 6f;
            DrawLine(end, p1, headColor, 3f, true);
            DrawLine(end, p2, headColor, 3f, true);
            DrawLine(p1, p2, headColor, 3f, true);
        }
    }

    private static void LoadSharedTextures()
    {
        if (_iconTexture == null)
        {
            _iconTexture = LoadTextureResource("res://LibraryOfRuina/intentgraph/images/ui/icon.png");
        }

        if (_groupBorderTexture == null)
        {
            _groupBorderTexture = LoadTextureResource("res://LibraryOfRuina/intentgraph/images/ui/groupborder.png");
        }

        if (_arrowTexture == null)
        {
            _arrowTexture = LoadTextureResource("res://LibraryOfRuina/intentgraph/images/ui/arrow.png");
        }
    }

    private static Texture2D? LoadTextureResource(string path)
    {
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }

        return ResourceLoader.Load<Texture2D>(path);
    }

    private static Rect2 UnitRectToPixels(Rect2 rect)
    {
        Vector2 p = UnitToPixels(rect.Position);
        return new Rect2(p, rect.Size * GridSize);
    }

    private static Vector2 UnitToPixels(Vector2 unitPoint)
    {
        return new Vector2(Margin + unitPoint.X * GridSize, Margin + unitPoint.Y * GridSize);
    }
}


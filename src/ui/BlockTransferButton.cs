using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace LibraryOfRuina.ui;

internal partial class BlockTransferButton : NButton
{
    public const float VisualSize = 85.00f;
    public const float HitboxWidth = VisualSize * 1.4f;
    public const float HitboxHeight = VisualSize;
    public const string DefaultTexturePath = "res://images/ui/little_red_block_transfer_button.png";

    public static readonly Vector2 VisualSizeVector = new(VisualSize, VisualSize);
    public static readonly Vector2 HitboxSize = new(HitboxWidth, HitboxHeight);

    private readonly string _texturePath;
    private readonly string _hoverTitleCategory;
    private readonly string _hoverTitleKey;
    private readonly string _hoverDescriptionCategory;
    private readonly string _hoverDescriptionKey;
    private TextureRect? _buttonImage;
    private HoverBorder? _hoverBorder;
    private Texture2D? _buttonTexture;

    protected override bool AllowFocusWhileDisabled => true;

    public BlockTransferButton(
        string texturePath = DefaultTexturePath,
        string hoverTitleCategory = "settings_ui",
        string hoverTitleKey = "LITTLE_RED_BLOCK_TRANSFER.title",
        string hoverDescriptionCategory = "settings_ui",
        string hoverDescriptionKey = "LITTLE_RED_BLOCK_TRANSFER.description")
    {
        _texturePath = texturePath;
        _hoverTitleCategory = hoverTitleCategory;
        _hoverTitleKey = hoverTitleKey;
        _hoverDescriptionCategory = hoverDescriptionCategory;
        _hoverDescriptionKey = hoverDescriptionKey;
        CustomMinimumSize = HitboxSize;
        Size = HitboxSize;
        PivotOffset = Size * 0.5f;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
    }

    public override bool _HasPoint(Vector2 point)
    {
        return point.X >= 0f
            && point.X <= HitboxWidth
            && point.Y >= 0f
            && point.Y <= HitboxHeight;
    }

    public override void _Ready()
    {
        ConnectSignals();
        BuildVisuals();
        Disable();
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        SetHoverBorderVisible(true);
        NHoverTipSet? tipSet = NHoverTipSet.CreateAndShow(this, CreateHoverTips(), HoverTipAlignment.Center);
        if (tipSet == null)
        {
            return;
        }

        
        
        Rect2 rect = GetGlobalRect();
        if (rect.Size.X > 0f && rect.Size.Y > 0f)
        {
            tipSet.GlobalPosition = rect.Position + Vector2.Down * rect.Size.Y * 1.5f;
        }
        else
        {
            tipSet.GlobalPosition = GlobalPosition + Vector2.Down * Size.Y * 1.5f;
        }
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        SetHoverBorderVisible(false);
        NHoverTipSet.Remove(this);
    }

    protected virtual List<IHoverTip> CreateHoverTips()
    {
        var tips = new List<IHoverTip>();

        var titleLoc = LocString.GetIfExists(_hoverTitleCategory, _hoverTitleKey);
        var descLoc = LocString.GetIfExists(_hoverDescriptionCategory, _hoverDescriptionKey);
        if (titleLoc != null && descLoc != null)
        {
            tips.Add(new HoverTip(titleLoc, descLoc));
        }

        tips.Add(HoverTipFactory.Static(StaticHoverTip.Block));
        return tips;
    }

    private void BuildVisuals()
    {
        _buttonTexture = ResourceLoader.Load<Texture2D>(_texturePath);

        _buttonImage = new TextureRect
        {
            Name = "ButtonImage",
            Size = VisualSizeVector,
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = _buttonTexture
        };
        AddChild(_buttonImage);

        _hoverBorder = new HoverBorder
        {
            Name = "HoverBorder",
            Size = VisualSizeVector,
            Visible = false
        };
        AddChild(_hoverBorder);
    }

    private void SetHoverBorderVisible(bool visible)
    {
        if (_hoverBorder == null)
        {
            return;
        }

        _hoverBorder.Visible = visible;
        _hoverBorder.QueueRedraw();
    }

    private sealed partial class HoverBorder : Control
    {
        private const float BorderWidth = 3f;
        private static readonly Color BorderColor = new(1f, 0.84f, 0.08f);

        public HoverBorder()
        {
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            float inset = BorderWidth * 0.5f;
            DrawRect(
                new Rect2(new Vector2(inset, inset), Size - new Vector2(BorderWidth, BorderWidth)),
                BorderColor,
                false,
                BorderWidth);
        }
    }
}

using Godot;
using HarmonyLib;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace LibraryOfRuina.content.abnormalities.Alriune;

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class AlriuneCrownCardPatch
{
    private const string OverlayName = "AlriuneCrownOverlay";

    // 去掉透明边的王冠贴图。所有卡牌（含原版牌）都会走这个补丁，裁剪框要 GetImage 从显存读回整张贴图才能算，
    // 只在第一次算；之后每张新攻击牌共用这一份
    private static AtlasTexture? _croppedCrown;

    private static AtlasTexture? CroppedCrown()
    {
        if (_croppedCrown is { Atlas: { } atlas } && GodotTextureSafety.IsValid(atlas))
        {
            return _croppedCrown;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(AlriuneAssets.CrownOverlay);
        if (!GodotTextureSafety.IsValid(texture))
        {
            return null;
        }

        using Image pixels = texture!.GetImage();
        _croppedCrown = new AtlasTexture { Atlas = texture, Region = pixels.GetUsedRect() };
        return _croppedCrown;
    }

    [HarmonyPostfix]
    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady() || __instance.GetNodeOrNull<Control>("%Frame") is not { } frame)
        {
            return;
        }
        // 当前卡牌内部的 CardContainer；标记与卡框共享层级和遮挡顺序。
        Node parent = frame.GetParent();
        if (parent.GetNodeOrNull<AlriuneCrownCardOverlay>(OverlayName) is { } existing)
        {
            existing.Bind(__instance, frame);
            return;
        }
        if (__instance.Model?.Type != CardType.Attack)
        {
            return;
        }
        if (CroppedCrown() is not { } crown)
        {
            return;
        }
        AlriuneCrownCardOverlay overlay = new()
        {
            Name = OverlayName,
            Texture = crown,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ZIndex = 0,
            ZAsRelative = true
        };
        parent.AddChild(overlay);
        overlay.Bind(__instance, frame);
    }
}

internal sealed partial class AlriuneCrownCardOverlay : TextureRect
{
    private const float CrownRotationDegrees = 45f;
    private static readonly Vector2 CrownSize = new(120f, 50f);
    private static readonly Vector2 CornerInset = new(-10f, 10f);

    private NCard? _card;
    private Control? _frame;

    internal void Bind(NCard card, Control frame)
    {
        _card = card;
        _frame = frame;
        ZIndex = 0;
        ZAsRelative = true;
        RotationDegrees = CrownRotationDegrees;
        Refresh();
    }

    public override void _Process(double delta) => Refresh();

    private void Refresh()
    {
        if (_card == null || !IsInstanceValid(_card) || _frame == null || !IsInstanceValid(_frame))
        {
            Visible = false;
            return;
        }
        var model = _card.Model;
        Visible = model is { IsMutable: true, Type: CardType.Attack }
            && model.Owner?.Creature.GetPower<AlriuneAtonementCrownPower>()?.Amount > 0;
        if (Visible)
        {
            Size = CrownSize;
            PivotOffset = CrownSize * 0.5f;
            Position = _frame.Position
                + new Vector2(_frame.Size.X, 0f)
                + CornerInset
                - PivotOffset;
        }
    }

    // NCard 会进出对象池；同一节点再次入树时继续读取当前 Model。
    // 标记是卡牌的子节点，随父节点释放，无需在临时离树时清空绑定。
}

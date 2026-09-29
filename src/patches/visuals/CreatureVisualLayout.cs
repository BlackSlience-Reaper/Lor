using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

// CreatureVisualLayout 参数顺序：
// 1. SpritePos：怪物立绘位置，改它会移动怪物本体。
// 2. SpriteScale：怪物立绘缩放；X 为负数会左右翻转。
// 3-6. BoundsLeft/BoundsTop/BoundsRight/BoundsBottom：点击框和选择框范围。
//      Left/Right 还会影响血条和 power 栏宽度、X 对齐；Top/Bottom 不控制血条或 power 栏 Y。
// 7. CenterPos：怪物中心点，主要给受击特效/VFX 用。
// 8. IntentPos：敌人意图图标位置。
// 可选项：
// - TalkPos：说话气泡位置。
// - StateDisplayLiftY：血条、power 栏、名字条整体上移量；正数越大越往上。
internal readonly record struct CreatureVisualLayout(
    Vector2 SpritePos,
    Vector2 SpriteScale,
    float BoundsLeft,
    float BoundsTop,
    float BoundsRight,
    float BoundsBottom,
    Vector2 CenterPos,
    Vector2 IntentPos)
{
    public Vector2? TalkPos { get; init; }

    public Vector2? StolenCardPos { get; init; }

    public Vector2? StolenCardScale { get; init; }

    // Positive values move the HP bar, power row, and nameplate upward together.
    public float StateDisplayLiftY { get; init; }

    public static CreatureVisualLayout Default(float halfWidth = 120f) => new(
        SpritePos: new Vector2(0, -150f),
        SpriteScale: new Vector2(0.31f, 0.31f),
        BoundsLeft: -halfWidth,
        BoundsTop: -299.7f,
        BoundsRight: halfWidth,
        BoundsBottom: 5f,
        CenterPos: new Vector2(0, -139.8f),
        IntentPos: new Vector2(0, -333.7f));
}

internal sealed partial class CreatureStateDisplayOffset : Node
{
    public float LiftY { get; init; }

    public Vector2 AdditionalOffset { get; set; }

    private readonly List<(Control Control, Vector2 BasePosition)> _targets = new();

    public override void _Ready()
    {
        SetProcess(false);
        if (Mathf.IsZeroApprox(LiftY) && AdditionalOffset == Vector2.Zero)
        {
            return;
        }

        CallDeferred(nameof(InitializeOffset));
    }

    public override void _Process(double delta)
    {
        ApplyOffset();
    }

    private void InitializeOffset()
    {
        if (GetParent()?.GetParent() is not NCreature creatureNode)
        {
            return;
        }

        NCreatureStateDisplay? stateDisplay = creatureNode.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        if (stateDisplay == null)
        {
            return;
        }

        CaptureTarget(stateDisplay, "%HealthBar");
        CaptureTarget(stateDisplay, "%PowerContainer");
        CaptureTarget(stateDisplay, "%NameplateContainer");
        ApplyOffset();
        SetProcess(_targets.Count > 0);
    }

    private void CaptureTarget(Node parent, NodePath path)
    {
        if (parent.GetNodeOrNull<Control>(path) is { } control)
        {
            _targets.Add((control, control.Position));
        }
    }

    private void ApplyOffset()
    {
        // NPowerContainer rewrites its own Position after power changes, so keep the lifted Y stable.
        foreach ((Control control, Vector2 basePosition) in _targets)
        {
            control.Position = basePosition + new Vector2(
                AdditionalOffset.X,
                AdditionalOffset.Y - LiftY);
        }
    }
}

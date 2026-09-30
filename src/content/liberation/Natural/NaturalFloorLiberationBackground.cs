using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.liberation.Natural;

internal sealed partial class NaturalFloorLiberationBackground : TextureRect
{
    internal const string HumanTexturePath =
        NaturalFloorAssets.LoveAndHatredHumanBackground;
    internal const string SnakeTexturePath =
        NaturalFloorAssets.LoveAndHatredSnakeBackground;
    internal const string WrathTexturePath =
        NaturalFloorAssets.WrathServantStrongBackground;
    internal const string DespairTexturePath =
        NaturalFloorAssets.DespairKnightBackground;

    internal const string GreedTexturePath =
        NaturalFloorAssets.KingOfGreedBackground;

    internal const string NihilTexturePath =
        NaturalFloorAssets.LiberationEncounterNihilBackground;

    internal NaturalFloorLiberationEncounter Encounter { get; init; } = null!;

    private string? _currentTexturePath;
    private bool _phaseRevealActive;

    internal static NaturalFloorLiberationBackground? GetCurrent() =>
        NCombatRoom.Instance?.Background?.GetNodeOrNull<NaturalFloorLiberationBackground>("Layer_00/A/AnimatedBackground");

    internal static string GetPhaseTexturePath(int phase) => phase switch
    {
        5 => NihilTexturePath,
        4 => GreedTexturePath,
        3 => DespairTexturePath,
        2 => WrathTexturePath,
        _ => HumanTexturePath
    };

    public override void _Ready()
    {
        base._Ready();
        _Process(0d);
        SetProcess(!Encounter.SettlementTriggered);
    }

    public override void _Process(double delta)
    {
        if (Encounter.SettlementTriggered)
        {
            SetProcess(false);
            return;
        }
        // The reveal controller owns the texture while its old-background overlay is playing.
        if (_phaseRevealActive)
        {
            return;
        }

        int visiblePhase = Encounter.TransitionPending ? Encounter.CurrentPhase - 1 : Encounter.CurrentPhase;
        SetPhaseBackground(visiblePhase);
    }

    internal void SetPhaseBackground(int phase)
    {
        string path = phase == 1 && Encounter.IsFirstPhaseSnakeForm ? SnakeTexturePath : GetPhaseTexturePath(phase);
        if (_currentTexturePath == path || !IsInsideTree())
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
        if (!GodotObject.IsInstanceValid(texture))
        {
            return;
        }

        Texture = texture;
        _currentTexturePath = path;
    }

    internal void BeginPhaseReveal()
    {
        _phaseRevealActive = true;
        SetProcess(false);
    }

    internal void EndPhaseReveal()
    {
        _phaseRevealActive = false;
        if (IsInsideTree() && !Encounter.SettlementTriggered)
        {
            SetProcess(true);
            _Process(0d);
        }
    }

    public override void _ExitTree()
    {
        SetProcess(false);
        _currentTexturePath = null;
        base._ExitTree();
    }
}

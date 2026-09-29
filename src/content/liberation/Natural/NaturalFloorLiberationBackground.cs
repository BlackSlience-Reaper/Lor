using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.liberation.Natural;

internal sealed partial class NaturalFloorLiberationBackground : TextureRect
{
    internal const string HumanTexturePath =
        "res://images/backgrounds/natural_floor_liberation_encounter/love_and_hatred_human.png";
    internal const string SnakeTexturePath =
        "res://images/backgrounds/natural_floor_liberation_encounter/love_and_hatred_snake.png";
    internal const string WrathTexturePath =
        "res://images/backgrounds/wrath_servant_strong/background.png";
    internal const string DespairTexturePath =
        "res://images/backgrounds/despair_knight_strong/despair_knight_background.png";

    internal const string GreedTexturePath =
        "res://images/backgrounds/king_of_greed/king_of_greed_background.png";

    internal const string NihilTexturePath =
        "res://images/backgrounds/natural_floor_liberation_encounter/nihil.png";

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

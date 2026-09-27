using LibraryOfRuina.monsters.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.NaturalFloorLiberation;

internal sealed partial class NaturalFloorLoveAndHatredVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = "res://scenes/creature_visuals/natural_floor_love_and_hatred_boss.tscn";
    internal const string AnimationRoot = "res://scenes/creature_visuals/natural_floor_love_and_hatred_";
    internal const string ImageRoot = "res://images/monsters/natural_floor_liberation/love_and_hatred/";
    internal const float AttackHitTime = 0.36f;
    private string _lastLibrary = "";

    protected override string ResolveCurrentAnimationLibrary()
    {
        if (GetParent() is NCreature node && node.Entity.Monster is NaturalFloorLoveAndHatredBoss boss)
            return boss.IsSnakeForm ? "snake" : boss.IsSpecialIdle ? "special" : "human";
        return "human";
    }

    protected override string NormalizeTriggerName(string triggerName) => triggerName switch
    {
        "Attack" => "Strike", "Cast" => "Fire", "Dead" => "Hit", _ => triggerName
    };

    public override void _Ready()
    {
        base._Ready();
        _lastLibrary = ResolveCurrentAnimationLibrary();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        string library = ResolveCurrentAnimationLibrary();
        if (_lastLibrary == library) return;
        _lastLibrary = library;
        TryPlayTrigger("Idle");
    }
}

using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorLoveAndHatredBoss), ScenePath = NaturalFloorLoveAndHatredVisuals.ScenePath)]
internal sealed partial class NaturalFloorLoveAndHatredVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = NaturalFloorAssets.LoveAndHatredBossScene;
    internal const string AnimationRoot = NaturalFloorAssets.LoveAndHatredScenePrefix;
    internal const string ImageRoot = NaturalFloorAssets.LoveAndHatredMonsterRoot;
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

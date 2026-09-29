using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

[MonsterVisual(typeof(GreenStemHermit), ScenePath = GreenStemHermitCreatureVisuals.ScenePath)]
internal sealed partial class GreenStemHermitCreatureVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = "res://scenes/creature_visuals/green_stem_hermit.tscn";
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, "res://scenes/creature_visuals/green_stem_hermit_animations.tres",
        "res://images/monsters/green_stem_hermit/idle.png",
        "res://images/monsters/green_stem_hermit/reach.png",
        "res://images/monsters/green_stem_hermit/ground.png",
        "res://images/monsters/green_stem_hermit/thrust.png",
        "res://images/monsters/green_stem_hermit/hit.png"
    ];

    protected override string ResolveCurrentAnimationLibrary() => "main";

    protected override string NormalizeTriggerName(string triggerName) => triggerName switch
    {
        "Attack" => "AttackThrust",
        "Cast" => "AttackReach",
        "Dead" => "Hit",
        _ => triggerName
    };
}

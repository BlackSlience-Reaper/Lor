using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorBlindRageBoss), ScenePath = NaturalFloorBlindRageVisuals.ScenePath)]
internal sealed partial class NaturalFloorBlindRageVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = "res://scenes/creature_visuals/natural_floor_blind_rage_boss.tscn";
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, "res://scenes/creature_visuals/natural_floor_blind_rage_boss_animations.tres",
        "res://images/monsters/natural_floor_liberation/blind_rage/idle.png",
        "res://images/monsters/natural_floor_liberation/blind_rage/strike.png",
        "res://images/monsters/natural_floor_liberation/blind_rage/thrust.png",
        "res://images/monsters/natural_floor_liberation/blind_rage/slash.png",
        "res://images/monsters/natural_floor_liberation/blind_rage/s1.png",
        "res://images/monsters/natural_floor_liberation/blind_rage/s2.png",
        "res://images/monsters/natural_floor_liberation/blind_rage/s3.png",
        "res://images/monsters/natural_floor_liberation/blind_rage/hit.png"
    ];

    protected override string ResolveCurrentAnimationLibrary() => "main";

    protected override string NormalizeTriggerName(string triggerName) => triggerName switch
    {
        "Attack" => "AttackStrike",
        "Cast" => "SpecialS1",
        "Dead" => "Hit",
        "Victory" => "SpecialS3",
        _ => triggerName
    };
}

[MonsterVisual(typeof(NaturalFloorGreenStemHermit), ScenePath = NaturalFloorHermitVisuals.ScenePath)]
internal sealed partial class NaturalFloorHermitVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = "res://scenes/creature_visuals/natural_floor_green_stem_hermit.tscn";
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, "res://scenes/creature_visuals/natural_floor_green_stem_hermit_animations.tres",
        "res://images/monsters/green_stem_hermit/idle.png",
        "res://images/monsters/green_stem_hermit/reach.png",
        "res://images/monsters/green_stem_hermit/ground.png",
        "res://images/monsters/green_stem_hermit/thrust.png",
        "res://images/monsters/natural_floor_liberation/green_stem_hermit/mental.png",
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

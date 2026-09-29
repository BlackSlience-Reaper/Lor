using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

[MonsterVisual(typeof(WrathServant), ScenePath = WrathServantCreatureVisuals.ScenePath)]
internal sealed partial class WrathServantCreatureVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = "res://scenes/creature_visuals/wrath_servant.tscn";
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, "res://scenes/creature_visuals/wrath_servant_animations.tres",
        "res://images/monsters/wrath_servant/idle.png",
        "res://images/monsters/wrath_servant/attack_strike.png",
        "res://images/monsters/wrath_servant/attack_slash.png",
        "res://images/monsters/wrath_servant/attack_slash2.png",
        "res://images/monsters/wrath_servant/s1.png",
        "res://images/monsters/wrath_servant/s2.png",
        "res://images/monsters/wrath_servant/s3.png",
        "res://images/monsters/wrath_servant/hit.png",
        "res://images/monsters/wrath_servant/special.png"
    ];

    protected override string ResolveCurrentAnimationLibrary() => "main";

    protected override string NormalizeTriggerName(string triggerName) => triggerName switch
    {
        "Attack" => "AttackStrike",
        "Cast" => "SpecialS1",
        "Dead" => "Hit",
        _ => triggerName
    };
}

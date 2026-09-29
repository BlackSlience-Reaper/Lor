using LibraryOfRuina.monsters.JudgementBird;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.JudgementBird;

[MonsterVisual(typeof(monsters.JudgementBird.JudgementBird), ScenePath = JudgementBirdCreatureVisuals.ScenePath)]
internal sealed partial class JudgementBirdCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    private const string AnimationLibrary = "default";

    internal const string ScenePath =
        "res://scenes/creature_visuals/judgement_bird.tscn";

    internal static IReadOnlyList<string> AssetPaths { get; } =
    [
        ScenePath,
        monsters.JudgementBird.JudgementBird.IdleTexturePath,
        monsters.JudgementBird.JudgementBird.FireTexturePath,
        monsters.JudgementBird.JudgementBird.GuardTexturePath,
        monsters.JudgementBird.JudgementBird.HitTexturePath
    ];

    protected override string ResolveCurrentAnimationLibrary() =>
        AnimationLibrary;

    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName switch
        {
            "Block" => "Guard",
            "Cast" or "Heal" or "Judgement" => "Attack",
            _ => triggerName
        };
}

[MonsterVisual(typeof(EscapedBird), ScenePath = EscapedBirdCreatureVisuals.ScenePath)]
internal sealed partial class EscapedBirdCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    private const string AnimationLibrary = "default";
    private int _attackCursor;
    private int _screamCursor;

    internal const string ScenePath =
        "res://scenes/creature_visuals/escaped_bird.tscn";

    internal static IReadOnlyList<string> AssetPaths { get; } =
    [
        ScenePath,
        EscapedBird.IdleTexturePath,
        EscapedBird.AttackTexturePath,
        EscapedBird.AttackTwoTexturePath,
        EscapedBird.HitTexturePath
    ];

    protected override string ResolveCurrentAnimationLibrary() =>
        AnimationLibrary;

    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName switch
        {
            "Attack" => _attackCursor++ % 2 == 0
                ? "AttackOne"
                : "AttackTwo",
            "Scream" => _screamCursor++ % 2 == 0
                ? "ScreamTwo"
                : "ScreamOne",
            _ => triggerName
        };
}

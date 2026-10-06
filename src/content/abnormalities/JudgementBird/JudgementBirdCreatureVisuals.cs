using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.JudgementBird;

[MonsterVisual(typeof(JudgementBird), ScenePath = JudgementBirdCreatureVisuals.ScenePath)]
internal sealed partial class JudgementBirdCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    // 整块的 Spine 身体（照场景摆放），加载失败时退回场景动画。触发名先经 NormalizeTriggerName
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "judgement_bird",
        "judgement_bird",
        "attack",
        new Dictionary<string, string>
        {
            ["Guard"] = "guard",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    private const string AnimationLibrary = "default";

    internal const string ScenePath =
        JudgementBirdAssets.JudgementBirdScene;

    internal static IReadOnlyList<string> AssetPaths { get; } =
    [
        ScenePath,
        JudgementBird.IdleTexturePath,
        JudgementBird.FireTexturePath,
        JudgementBird.GuardTexturePath,
        JudgementBird.HitTexturePath
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
    // 整块的 Spine 身体（飘着；各姿势按标注点对齐高度），加载失败时退回场景动画。
    // 攻击、尖叫先经 NormalizeTriggerName 轮换成一、二两种
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "escaped_bird",
        "escaped_bird",
        "attack_one",
        new Dictionary<string, string>
        {
            ["AttackOne"] = "attack_one",
            ["AttackTwo"] = "attack_two",
            ["ScreamOne"] = "scream_one",
            ["ScreamTwo"] = "scream_two",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    private const string AnimationLibrary = "default";
    private int _attackCursor;
    private int _screamCursor;

    internal const string ScenePath =
        JudgementBirdAssets.EscapedBirdScene;

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

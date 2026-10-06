using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

[MonsterVisual(typeof(GreenStemHermit), ScenePath = GreenStemHermitCreatureVisuals.ScenePath)]
internal sealed partial class GreenStemHermitCreatureVisuals : SceneAnimatedCreatureVisuals
{
    // 整块的 Spine 身体（照场景摆放，各姿势按标注点对齐），加载失败时退回场景动画。触发名先经 NormalizeTriggerName
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "green_stem_hermit",
        "green_stem_hermit",
        "attack_thrust",
        new Dictionary<string, string>
        {
            ["AttackReach"] = "attack_reach",
            ["AttackGround"] = "attack_ground",
            ["AttackThrust"] = "attack_thrust",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal const string ScenePath = WrathServantAssets.GreenStemHermitScene;
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, WrathServantAssets.GreenStemHermitAnimationsResource,
        WrathServantAssets.GreenStemHermitIdleTexture,
        WrathServantAssets.GreenStemHermitReachTexture,
        WrathServantAssets.GreenStemHermitGroundTexture,
        WrathServantAssets.GreenStemHermitThrustTexture,
        WrathServantAssets.GreenStemHermitHitTexture
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

using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

[MonsterVisual(typeof(WrathServant), ScenePath = WrathServantCreatureVisuals.ScenePath)]
internal sealed partial class WrathServantCreatureVisuals : SceneAnimatedCreatureVisuals
{
    // 整块的 Spine 身体（照场景摆放，各姿势按标注点对齐），加载失败时退回场景动画。触发名先经 NormalizeTriggerName；
    // 特殊招式三段各自发触发（每段 0.6 秒），各对一张图；胜利演出换成那张人形图，停 2 秒
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "wrath_servant",
        "wrath_servant",
        "attack_strike",
        new Dictionary<string, string>
        {
            ["AttackStrike"] = "attack_strike",
            ["AttackSlash"] = "attack_slash",
            ["AttackSlash2"] = "attack_slash2",
            ["SpecialS1"] = "special_s1",
            ["SpecialS2"] = "special_s2",
            ["SpecialS3"] = "special_s3",
            ["Victory"] = "victory",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal const string ScenePath = WrathServantAssets.WrathServantScene;
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, WrathServantAssets.WrathServantAnimationsResource,
        WrathServantAssets.WrathServantIdleTexture,
        WrathServantAssets.AttackStrikeTexture,
        WrathServantAssets.AttackSlashTexture,
        WrathServantAssets.AttackSlash2Texture,
        WrathServantAssets.WrathServantS1Texture,
        WrathServantAssets.WrathServantS2Texture,
        WrathServantAssets.WrathServantS3Texture,
        WrathServantAssets.WrathServantHitTexture,
        WrathServantAssets.WrathServantSpecialTexture
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

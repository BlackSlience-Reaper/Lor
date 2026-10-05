using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorBlindRageBoss), ScenePath = NaturalFloorBlindRageVisuals.ScenePath)]
internal sealed partial class NaturalFloorBlindRageVisuals : SceneAnimatedCreatureVisuals
{
    // Spine 身体按动画库（形态）各一副，见 tools/spine_from_layers/build_boss_configs.py 的自然层配置
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "blind_rage",
        "attack_strike",
        new Dictionary<string, string>
        {
            ["AttackStrike"] = "attack_strike",
            ["AttackThrust"] = "attack_thrust",
            ["AttackSlash"] = "attack_slash",
            ["SpecialS1"] = "special_s1",
            ["SpecialS2"] = "special_s2",
            ["SpecialS3"] = "special_s3",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal const string ScenePath = NaturalFloorAssets.BlindRageBossScene;
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, NaturalFloorAssets.BlindRageBossAnimationsResource,
        NaturalFloorAssets.BlindRageIdleTexture,
        NaturalFloorAssets.BlindRageStrikeTexture,
        NaturalFloorAssets.BlindRageThrustTexture,
        NaturalFloorAssets.BlindRageSlashTexture,
        NaturalFloorAssets.BlindRageS1Texture,
        NaturalFloorAssets.BlindRageS2Texture,
        NaturalFloorAssets.BlindRageS3Texture,
        NaturalFloorAssets.BlindRageHitTexture
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
    internal const string ScenePath = NaturalFloorAssets.GreenStemHermitScene;
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, NaturalFloorAssets.GreenStemHermitAnimationsResource,
        NaturalFloorAssets.GreenStemHermitIdleTexture,
        NaturalFloorAssets.GreenStemHermitReachTexture,
        NaturalFloorAssets.GreenStemHermitGroundTexture,
        NaturalFloorAssets.GreenStemHermitThrustTexture,
        NaturalFloorAssets.GreenStemHermitMentalTexture,
        NaturalFloorAssets.GreenStemHermitHitTexture
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

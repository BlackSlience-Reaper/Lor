using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorBlindRageBoss), ScenePath = NaturalFloorBlindRageVisuals.ScenePath)]
internal sealed partial class NaturalFloorBlindRageVisuals : SceneAnimatedCreatureVisuals
{
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

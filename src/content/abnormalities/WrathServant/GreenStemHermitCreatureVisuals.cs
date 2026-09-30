using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

[MonsterVisual(typeof(GreenStemHermit), ScenePath = GreenStemHermitCreatureVisuals.ScenePath)]
internal sealed partial class GreenStemHermitCreatureVisuals : SceneAnimatedCreatureVisuals
{
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

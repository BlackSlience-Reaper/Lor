using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

[MonsterVisual(typeof(WrathServant), ScenePath = WrathServantCreatureVisuals.ScenePath)]
internal sealed partial class WrathServantCreatureVisuals : SceneAnimatedCreatureVisuals
{
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

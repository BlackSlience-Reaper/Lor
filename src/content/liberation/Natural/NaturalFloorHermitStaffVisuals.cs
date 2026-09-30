using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorHermitStaff), ScenePath = NaturalFloorHermitStaffVisuals.ScenePath)]
internal sealed partial class NaturalFloorHermitStaffVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = NaturalFloorAssets.HermitStaffScene;
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, NaturalFloorAssets.HermitStaffAnimationsResource,
        NaturalFloorAssets.HermitStaffIdleTexture,
        NaturalFloorAssets.HermitStaffAttackTexture,
        NaturalFloorAssets.HermitStaffHitTexture
    ];

    protected override string ResolveCurrentAnimationLibrary() => "main";

    protected override string NormalizeTriggerName(string triggerName) => triggerName switch
    {
        "Cast" => "Attack",
        "Dead" => "Hit",
        _ => triggerName
    };
}

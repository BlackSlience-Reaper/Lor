using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorHermitStaff), ScenePath = NaturalFloorHermitStaffVisuals.ScenePath)]
internal sealed partial class NaturalFloorHermitStaffVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = NaturalFloorAssets.HermitStaffScene;

    // 整块的 Spine 身体（tools/spine_from_layers/sprite_layers.py 按场景摆放、按标注点对齐），加载失败时退回场景动画
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "natural_floor_liberation", "nf_hermit_staff", "attack", new Dictionary<string, string>());

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;
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

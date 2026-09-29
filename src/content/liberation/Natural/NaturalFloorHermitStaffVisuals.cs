using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorHermitStaff), ScenePath = NaturalFloorHermitStaffVisuals.ScenePath)]
internal sealed partial class NaturalFloorHermitStaffVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = "res://scenes/creature_visuals/natural_floor_hermit_staff.tscn";
    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath, "res://scenes/creature_visuals/natural_floor_hermit_staff_animations.tres",
        "res://images/monsters/hermit_staff/idle.png",
        "res://images/monsters/hermit_staff/attack.png",
        "res://images/monsters/hermit_staff/hit.png"
    ];

    protected override string ResolveCurrentAnimationLibrary() => "main";

    protected override string NormalizeTriggerName(string triggerName) => triggerName switch
    {
        "Cast" => "Attack",
        "Dead" => "Hit",
        _ => triggerName
    };
}

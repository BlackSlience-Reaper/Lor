using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Nodes.Combat;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Religion;

internal abstract partial class ReligionFloorCreatureVisuals : SceneAnimatedCreatureVisuals
{
    // 三位使徒：整块的 Spine 身体（按标注点对齐，普通库一副），假死（dead 库）没有骨架、退回场景动画，复活后换回。
    // 特殊招式照场景 0、1、2 秒换三张图
    protected static RuntimeSpineBody.Spec CreateApostleSpine(string name, string attackAnimation) => LayeredBossSpine.Create(
        "religion_floor_liberation",
        name,
        attackAnimation,
        new Dictionary<string, string>
        {
            ["Slash"] = "slash",
            ["Strike"] = "strike",
            ["Pierce"] = "pierce",
            ["Guard"] = "guard",
            ["Cast"] = "cast",
            ["Wake"] = "wake",
            ["Special"] = "special",
        });

    private string _lastForm = "normal";

    protected override string ResolveCurrentAnimationLibrary()
    {
        if (GetParent() is NCreature { Entity.Monster: ReligionFloorApostle { IsFakeDead: true } })
        {
            return "dead";
        }
        if (GetParent() is NCreature { Entity.Monster: ReligionFloorLostParadise boss }
            && boss.Encounter is { RepentanceVisible: true })
        {
            return "repentance";
        }
        return "normal";
    }

    protected override string NormalizeTriggerName(string triggerName) => triggerName == "Dead" ? "Hit" : triggerName;

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (GetParent() is NCreature { Entity.Monster: ReligionFloorLostParadise { Encounter: { } encounter } })
        {
            ReligionFloorPresentation.UpdateWingPositions(encounter);
            if (encounter.RepentanceVisible && GetParent() is NCreature creature)
            {
                creature.IntentContainer.Visible = false;
            }
        }
        string form = ResolveCurrentAnimationLibrary();
        if (_lastForm != form)
        {
            _lastForm = form;
            TryPlayTrigger("Idle");
        }
    }
}

[MonsterVisual(typeof(ReligionFloorLostParadise), ScenePath = "res://scenes/creature_visuals/religion_floor_lost_paradise.tscn")]
internal sealed partial class ReligionFloorLostParadiseVisuals : ReligionFloorCreatureVisuals
{
    // Spine 身体见 LayeredBossSpine；忏悔期间（repentance 库）所有动作都停在忏悔姿势，另一副骨架只做受击晃动
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "religion_floor_liberation",
        "lost_paradise",
        "attack",
        new Dictionary<string, string>
        {
            ["Guard"] = "guard",
            ["Cast"] = "cast",
            ["Wake"] = "wake",
            ["Special"] = "special",
        });

    internal static readonly RuntimeSpineBody.Spec RepentanceSpine = LayeredBossSpine.Create(
        "religion_floor_liberation",
        "lost_paradise_repentance",
        "hurt",
        new Dictionary<string, string>
        {
            ["Guard"] = "hurt",
            ["Cast"] = "hurt",
            ["Wake"] = "hurt",
            ["Special"] = "hurt",
        });

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        [Spine, RepentanceSpine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) =>
        library == "repentance" ? RepentanceSpine : Spine;
}

[MonsterVisual(typeof(ReligionFloorScytheApostle), ScenePath = "res://scenes/creature_visuals/religion_floor_scythe_apostle.tscn")]
internal sealed partial class ReligionFloorScytheApostleVisuals : ReligionFloorCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = CreateApostleSpine("scythe_apostle", "slash");

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [Spine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) => library == "dead" ? null : Spine;
}

[MonsterVisual(typeof(ReligionFloorSpearApostle), ScenePath = "res://scenes/creature_visuals/religion_floor_spear_apostle.tscn")]
internal sealed partial class ReligionFloorSpearApostleVisuals : ReligionFloorCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = CreateApostleSpine("spear_apostle", "pierce");

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [Spine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) => library == "dead" ? null : Spine;
}

[MonsterVisual(typeof(ReligionFloorStaffApostle), ScenePath = "res://scenes/creature_visuals/religion_floor_staff_apostle.tscn")]
internal sealed partial class ReligionFloorStaffApostleVisuals : ReligionFloorCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = CreateApostleSpine("staff_apostle", "attack");

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [Spine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) => library == "dead" ? null : Spine;
}

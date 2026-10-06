using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.framework.assets;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.reverberation.CryingChildren;

internal static class CryingChildrenAssets
{
    internal const string PhilipScene = "res://scenes/creature_visuals/reverberation_philip.tscn";
    internal const string ChildScene = "res://scenes/creature_visuals/unspeaking_child.tscn";
    internal const string AudioRoot = "res://audio/sfx/reverberation/crying_children/";

    internal static readonly string[] Paths =
    [
        PhilipScene, ChildScene,
        AudioRoot + "philip_slash.ogg", AudioRoot + "philip_pierce.ogg",
        AudioRoot + "philip_strike.ogg", AudioRoot + "philip_ranged.ogg",
        AudioRoot + "child_slash.ogg", AudioRoot + "child_pierce.ogg",
        SharedAssets.LibraryPassiveOrangeIcon,
        SharedAssets.LibraryPassivePurpleIcon
    ];

    internal static string AttackSound(CryingChildMonsterBase monster, string animation)
    {
        if (monster is UnspeakingChild)
        {
            return AudioRoot + (animation == "Pierce" ? "child_pierce.ogg" : "child_slash.ogg");
        }
        return animation switch
        {
            "Pierce" => AudioRoot + "philip_pierce.ogg",
            "Strike" => AudioRoot + "philip_strike.ogg",
            "Ranged" => AudioRoot + "philip_ranged.ogg",
            _ => AudioRoot + "philip_slash.ogg"
        };
    }

    internal static void Speak(ReverberationPhilip monster, string moment) =>
        DawnOfficeDialogueHelper.Speak(monster, "REVERBERATION_PHILIP.banter." + moment);
}

[MonsterVisual(typeof(ReverberationPhilip), ScenePath = CryingChildrenAssets.PhilipScene)]
internal sealed partial class ReverberationPhilipVisuals : SceneAnimatedCreatureVisuals
{
    // 整块的 Spine 身体（照场景摆放，各姿势按标注点对齐）只给普通库，加载失败时退回场景动画；
    // 第三阶段的燃烧库没有骨架，退回场景动画。普通库的特殊招式在场景里就是待机图，不映射
    internal static readonly RuntimeSpineBody.Spec NormalSpine = LayeredBossSpine.Create(
        "crying_children",
        "rev_philip",
        "slash",
        new Dictionary<string, string>
        {
            ["Slash"] = "slash",
            ["Pierce"] = "pierce",
            ["Strike"] = "strike",
            ["Guard"] = "guard",
            ["Ranged"] = "ranged",
            ["Evade"] = "evade",
        });

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [NormalSpine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) =>
        library == "normal" ? NormalSpine : null;

    protected override string ResolveCurrentAnimationLibrary() =>
        (GetParent() as NCreature)?.Entity?.Monster is ReverberationPhilip { Phase: 3 }
            ? "burning" : "normal";
}

[MonsterVisual(typeof(UnspeakingChild), ScenePath = CryingChildrenAssets.ChildScene)]
internal sealed partial class UnspeakingChildVisuals : SceneAnimatedCreatureVisuals
{
    // 整块的 Spine 身体（照场景摆放，各姿势按标注点对齐），加载失败时退回场景动画。
    // 打击、防御、远程、特殊在场景里就是待机图，不映射
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "crying_children",
        "unspeaking_child",
        "slash",
        new Dictionary<string, string>
        {
            ["Slash"] = "slash",
            ["Pierce"] = "pierce",
            ["Evade"] = "evade",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    protected override string ResolveCurrentAnimationLibrary() => "normal";
}

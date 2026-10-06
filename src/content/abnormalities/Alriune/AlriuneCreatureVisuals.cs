using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.Alriune;

internal static class AlriuneAssets
{
    internal const string BossScene = "res://scenes/creature_visuals/alriune.tscn";
    internal const string DustbornScene = "res://scenes/creature_visuals/alriune_dustborn.tscn";
    internal const string BossRoot = "res://images/monsters/alriune/";
    internal const string DustbornRoot = "res://images/monsters/alriune_dustborn/";
    internal const string SfxRoot = "res://audio/sfx/alriune/";
    internal const string RangedSfx = SfxRoot + "ranged.ogg";
    internal const string GuardSfx = SfxRoot + "guard.ogg";
    internal const string DustbornAttackSfx = SfxRoot + "dustborn_attack.ogg";
    internal const string DustbornDodgeSfx = SfxRoot + "dustborn_dodge.ogg";
    internal const string CrownOverlay = "res://images/ui/alriune/crown_overlay.png";

    internal static readonly string[] BossAssets =
    [
        BossScene, BossRoot + "idle.png", BossRoot + "ranged.png", BossRoot + "hit.png",
        BossRoot + "guard.png", RangedSfx, GuardSfx, CrownOverlay,
        "res://images/powers/alriune_atonement_crown_power.png",
        "res://images/powers/art_floor_green_passive_power.png"
    ];

    internal static readonly string[] DustbornAssets =
    [
        DustbornScene, DustbornRoot + "idle.png", DustbornRoot + "pierce.png", DustbornRoot + "slash.png",
        DustbornRoot + "hit.png", DustbornRoot + "dodge.png", DustbornAttackSfx, DustbornDodgeSfx,
        "res://images/powers/art_floor_green_passive_power.png"
    ];
}

[MonsterVisual(typeof(Alriune), ScenePath = AlriuneAssets.BossScene)]
internal sealed partial class AlriuneCreatureVisuals : SceneAnimatedCreatureVisuals
{
    // 整块的 Spine 身体（照场景摆放，各姿势按标注点对齐），加载失败时退回场景动画。施法、格挡先经 NormalizeTriggerName 换成防御
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "alriune",
        "alriune",
        "attack",
        new Dictionary<string, string>
        {
            ["Guard"] = "guard",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal static IReadOnlyList<string> AssetPaths => AlriuneAssets.BossAssets;

    protected override string ResolveCurrentAnimationLibrary() => "default";

    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName is "Cast" or "Block" ? "Guard" : triggerName;
}

[MonsterVisual(typeof(AlriuneDustborn), ScenePath = AlriuneAssets.DustbornScene)]
internal sealed partial class AlriuneDustbornCreatureVisuals : SceneAnimatedCreatureVisuals
{
    // 整块的 Spine 身体（照场景摆放，各姿势按标注点对齐），加载失败时退回场景动画。攻击先经 NormalizeTriggerName 换成突刺，
    // 施法、格挡（以及心灵碎裂、复活）都是闪避姿势
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "alriune_dustborn",
        "alriune_dustborn",
        "pierce",
        new Dictionary<string, string>
        {
            ["Pierce"] = "pierce",
            ["Slash"] = "slash",
            ["Dodge"] = "dodge",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal static IReadOnlyList<string> AssetPaths => AlriuneAssets.DustbornAssets;

    protected override string ResolveCurrentAnimationLibrary() => "default";

    protected override string NormalizeTriggerName(string triggerName) => triggerName switch
    {
        "Attack" => "Pierce",
        "Cast" or "Block" => "Dodge",
        _ => triggerName
    };
}

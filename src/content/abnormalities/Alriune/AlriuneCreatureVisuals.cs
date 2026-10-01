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
    internal static IReadOnlyList<string> AssetPaths => AlriuneAssets.BossAssets;

    protected override string ResolveCurrentAnimationLibrary() => "default";

    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName is "Cast" or "Block" ? "Guard" : triggerName;
}

[MonsterVisual(typeof(AlriuneDustborn), ScenePath = AlriuneAssets.DustbornScene)]
internal sealed partial class AlriuneDustbornCreatureVisuals : SceneAnimatedCreatureVisuals
{
    internal static IReadOnlyList<string> AssetPaths => AlriuneAssets.DustbornAssets;

    protected override string ResolveCurrentAnimationLibrary() => "default";

    protected override string NormalizeTriggerName(string triggerName) => triggerName switch
    {
        "Attack" => "Pierce",
        "Cast" or "Block" => "Dodge",
        _ => triggerName
    };
}

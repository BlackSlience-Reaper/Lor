using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.BigBadWolf;

/// <summary>
/// 大坏狼：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按标注点对齐），加载失败时退回逐帧换图。
/// 普通、吞下两个形态各一副骨架。换形态的触发先切形态再播，所以吐出（切回普通）的动作在普通那副里。
/// </summary>
public sealed partial class BigBadWolfCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec NormalSpine = LayeredBossSpine.Create(
        "big_bad_wolf",
        "big_bad_wolf",
        "strike",
        new Dictionary<string, string>
        {
            ["Strike"] = "strike",
            ["Slash"] = "slash",
            ["Swallow"] = "swallow",
            ["Spit"] = "spit",
            ["Eat"] = "spit",
            ["ClearSwallowed"] = "spit",
        });

    internal static readonly RuntimeSpineBody.Spec SwallowedSpine = LayeredBossSpine.Create(
        "big_bad_wolf",
        "big_bad_wolf_swallowed",
        "strike",
        new Dictionary<string, string>
        {
            ["Strike"] = "strike",
            ["Slash"] = "slash",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => NormalSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [NormalSpine, SwallowedSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) =>
        variantKey == SwallowedVariant ? SwallowedSpine : NormalSpine;

    [MonsterVisual(typeof(BigBadWolf))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -178f), new(0.72f, 0.72f), -150f, -370f, 150f, 10f, new(0f, -178f), new(0f, -405f))
    {
        TalkPos = new Vector2(0f, -315f),
        StolenCardPos = new Vector2(0f, -218f),
        StolenCardScale = new Vector2(0.48f, 0.48f),
    };

    private const string NormalVariant = "normal";
    private const string SwallowedVariant = "swallowed";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            NormalVariant,
            BigBadWolf.IdleTexturePath);
        profile.Variant(
            SwallowedVariant,
            BigBadWolf.SwallowTexturePath);
        profile.InitialVariant(NormalVariant);
        profile.Centered();

        profile.Frame(
            "hit",
            BigBadWolf.HitTexturePath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "strike",
            BigBadWolf.StrikeTexturePath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "slash",
            BigBadWolf.SlashTexturePath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "swallowed_strike",
            BigBadWolf.SwallowedStrikeTexturePath)
            .ForVariant(SwallowedVariant);
        profile.Frame(
            "swallowed_slash",
            BigBadWolf.SwallowedSlashTexturePath)
            .ForVariant(SwallowedVariant);
        profile.Frame(
            "swallowed_hit",
            BigBadWolf.SwallowedHitTexturePath)
            .ForVariant(SwallowedVariant);

        profile.Swap("strike", 0.42f, "Strike", "Attack")
            .ForVariant(NormalVariant);
        profile.Swap("swallowed_strike", 0.42f, "Strike", "Attack")
            .ForVariant(SwallowedVariant);
        profile.Swap("slash", 0.42f, "Slash")
            .ForVariant(NormalVariant);
        profile.Swap("swallowed_slash", 0.42f, "Slash")
            .ForVariant(SwallowedVariant);
        profile.Swap("hit", 0.38f, "Hit")
            .ForVariant(NormalVariant);
        profile.Swap("swallowed_hit", 0.38f, "Hit")
            .ForVariant(SwallowedVariant);
        profile.Swap("@idle:swallowed", 0.48f, "Swallow");
        profile.Swap("@idle:swallowed", 0.12f, "Swallowed")
            .SwitchToVariant(SwallowedVariant);
        profile.Swap("@idle:swallowed", 0.44f, "Spit", "Eat", "ClearSwallowed")
            .SwitchToVariant(NormalVariant);
        return profile;
    }
}

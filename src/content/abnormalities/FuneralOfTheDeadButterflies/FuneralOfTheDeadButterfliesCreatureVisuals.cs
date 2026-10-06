using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;

/// <summary>
/// 送葬的亡蝶：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，整图只平移转动，各姿势按标注点对齐），
/// 加载失败时退回逐帧换图。黑蝶、白蝶两种开火与棺材的蓄势、出击照原来的换图各是一段姿势。
/// </summary>
public partial class FuneralOfTheDeadButterfliesCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "funeral_of_the_dead_butterflies",
        "funeral_butterflies",
        "fire_black",
        new Dictionary<string, string>
        {
            ["AttackBlack"] = "fire_black",
            ["AttackWhite"] = "fire_white",
            ["Cast"] = "coffin_prepare",
            ["CoffinAttack"] = "coffin_attack",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(FuneralOfTheDeadButterflies))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -126f), new(0.58f, 0.58f), -165f, -315f, 165f, 12f, new(0f, -126f), new(0f, -350f))
    {
        TalkPos = new Vector2(0f, -278f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            FuneralOfTheDeadButterflies.IdleTexturePath);
        profile.Frame(
            "coffin_prepare",
            FuneralOfTheDeadButterflies.CoffinPrepareTexturePath);
        profile.Frame(
            "coffin_attack",
            FuneralOfTheDeadButterflies.CoffinAttackTexturePath);
        profile.Frame(
            "fire_black",
            FuneralOfTheDeadButterflies.FireBlackTexturePath);
        profile.Frame(
            "fire_white",
            FuneralOfTheDeadButterflies.FireWhiteTexturePath);
        profile.Frame(
            "hit",
            FuneralOfTheDeadButterflies.HitTexturePath);
        profile.Swap(
            "fire_black",
            0.22f * 2.25f,
            "Attack",
            "AttackBlack");
        profile.Swap("fire_white", 0.26f * 2.25f, "AttackWhite");
        profile.Swap("coffin_prepare", 0.24f * 2.25f, "Cast");
        profile.Swap(
            "coffin_attack",
            0.24f * 2.25f,
            "CoffinAttack");
        profile.Swap("hit", 0.16f * 2.25f, "Hit");
        return profile;
    }
}

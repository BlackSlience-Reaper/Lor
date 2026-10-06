using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using QueenBeeMonster = LibraryOfRuina.content.abnormalities.QueenBee.QueenBee;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

/// <summary>
/// 蜂后的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按标注点对齐；
/// 待机的四片翅膀抠成单独的层在身体后面扇动），加载失败时退回逐帧换图。施法和防御同一张图。
/// </summary>
public sealed partial class QueenBeeCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "queen_bee",
        "queen_bee",
        "cast",
        new Dictionary<string, string>
        {
            ["Defend"] = "defend",
            ["Cast"] = "cast",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(QueenBee))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -200f), new(0.46f, 0.46f), -170f, -374f, 188f, 24f, new(0f, -126f), new(0f, -412f))
    {
        TalkPos = new Vector2(0f, -268f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                QueenBeeMonster.IdleTexturePath)
            .IdleOnly();
        profile.Frame("hit", QueenBeeMonster.HitTexturePath);
        profile.Frame("defend", QueenBeeMonster.DefendTexturePath);
        profile.Frame("cast", QueenBeeMonster.DefendTexturePath);
        profile.Swap("defend", 0.45f, "Defend");
        profile.Swap("cast", 0.45f, "Cast");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

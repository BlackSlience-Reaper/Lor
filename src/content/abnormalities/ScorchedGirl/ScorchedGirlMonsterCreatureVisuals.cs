using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.ScorchedGirl;

/// <summary>
/// 焦化少女：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按标注点对齐），加载失败时退回逐帧换图。
/// </summary>
public partial class ScorchedGirlMonsterCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "scorched_girl", "scorched_girl", "attack", new Dictionary<string, string>());

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(ScorchedGirlMonster))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(10f, -98f), new(0.52f, 0.52f), -124f, -218f, 124f, 8f, new(10f, -98f), new(-20f, -286f))
    {
        TalkPos = new Vector2(-20f, -214f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ScorchedGirlAssets.ScorchedGirlMonsterPrefix + ".png");
        profile.Frame("attack", ScorchedGirlAssets.ScorchedGirlMonsterPrefix + "_attack.webp")
            .Nudge(24f, -98f)
            .Scale(0.56f);
        profile.Frame("hit", ScorchedGirlAssets.ScorchedGirlMonsterPrefix + "_hit.webp");
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.Leticia;

/// <summary>
/// 小魔女的朋友（异想体战）：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，整图只平移转动，各姿势按标注点对齐），
/// 加载失败时退回逐帧换图。
/// </summary>
public partial class LittleWitchFriendCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "leticia",
        "little_witch_friend",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "cast",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(LittleWitchFriend))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -94f), new(0.43f, 0.43f), -126f, -202f, 126f, 8f, new(0f, -98f), new(0f, -242f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            LeticiaAssets.LittleWitchFriendMonsterPrefix + "idle.png");
        profile.Frame("attack", LeticiaAssets.LittleWitchFriendMonsterPrefix + "attack.png")
            .Nudge(20f, -102f)
            .Scale(0.54f);
        profile.Frame("cast", LeticiaAssets.LittleWitchFriendMonsterPrefix + "cast.png");
        profile.Frame("hit", LeticiaAssets.LittleWitchFriendMonsterPrefix + "hit.png");
        profile.Lunge("attack", 0.18f, 0.08f, 0.22f, "Attack");
        profile.Swap("cast", 0.36f, "Cast");
        profile.Swap("hit", 0.36f, "Hit");
        return profile;
    }
}

using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.Leticia;

public partial class LittleWitchFriendCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(LittleWitchFriend))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -94f), new(0.43f, 0.43f), -126f, -202f, 126f, 8f, new(0f, -98f), new(0f, -242f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root =
            "res://images/monsters/leticia/little_witch_friend_";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + "idle.png");
        profile.Frame("attack", root + "attack.png")
            .Nudge(20f, -102f)
            .Scale(0.54f);
        profile.Frame("cast", root + "cast.png");
        profile.Frame("hit", root + "hit.png");
        profile.Lunge("attack", 0.18f, 0.08f, 0.22f, "Attack");
        profile.Swap("cast", 0.36f, "Cast");
        profile.Swap("hit", 0.36f, "Hit");
        return profile;
    }
}

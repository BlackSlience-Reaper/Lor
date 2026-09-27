using LibraryOfRuina.monsters.GalaxyChild;

namespace LibraryOfRuina.visuals.GalaxyChild;

public sealed partial class GalaxyFriendCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            GalaxyFriend.IdleTexturePath);
        profile.Frame("attack", GalaxyFriend.AttackTexturePath);
        profile.Frame("hit", GalaxyFriend.HitTexturePath);
        profile.Frame("parry", GalaxyFriend.ParryTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("parry", 0.42f, "Parry", "Cast");
        return profile;
    }
}

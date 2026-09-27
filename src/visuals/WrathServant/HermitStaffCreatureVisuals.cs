using LibraryOfRuina.monsters.WrathServant;

namespace LibraryOfRuina.visuals.WrathServant;

public sealed partial class HermitStaffCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HermitStaff.IdleTexturePath)
            .Scale(0.515f);
        profile.Frame("attack", HermitStaff.AttackTexturePath);
        profile.Frame("hit", HermitStaff.HitTexturePath);
        profile.Swap("attack", 0.48f, "Attack");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

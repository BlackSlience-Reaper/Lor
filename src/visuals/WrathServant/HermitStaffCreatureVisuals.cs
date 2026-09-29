using LibraryOfRuina.monsters.WrathServant;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.WrathServant;

public sealed partial class HermitStaffCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HermitStaff))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -100f), new(0.46f, 0.46f), -90f, -220f, 90f, 8f, new(0f, -100f), new(0f, -250f));

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

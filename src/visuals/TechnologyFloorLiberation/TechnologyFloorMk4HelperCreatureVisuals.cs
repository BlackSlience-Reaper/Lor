using LibraryOfRuina.monsters.TechnologyFloorLiberation;

namespace LibraryOfRuina.visuals.TechnologyFloorLiberation;

public partial class TechnologyFloorMk4HelperCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                TechnologyFloorMk4Helper.IdleTexturePath)
            .At(0f, -85f)
            .Scale(0.50f)
            .IdleOnly();
        profile.Frame(
            "attack",
            TechnologyFloorMk4Helper.AttackTexturePath);
        profile.Frame("hit", TechnologyFloorMk4Helper.HitTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

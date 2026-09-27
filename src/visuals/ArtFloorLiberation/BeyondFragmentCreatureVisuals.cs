using LibraryOfRuina.monsters.ArtFloorLiberation;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class BeyondFragmentCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorBeyondFragmentBoss.IdleTexturePath);
        profile.Frame(
            "attack",
            ArtFloorBeyondFragmentBoss.AttackTexturePath);
        profile.Frame(
            "attack_2",
            ArtFloorBeyondFragmentBoss.Attack2TexturePath);
        profile.Frame("hit", ArtFloorBeyondFragmentBoss.HitTexturePath);
        profile.Frame("ego", ArtFloorBeyondFragmentBoss.EgoTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("attack_2", 0.42f, "Attack2");
        profile.Swap("ego", 0.60f, "Ego");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

using LibraryOfRuina.monsters.ArtFloorLiberation;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class ArtFloorDaCapoCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorDaCapoBoss.IdleTexturePath);
        profile.Frame("attack", ArtFloorDaCapoBoss.AttackTexturePath);
        profile.Frame("hit", ArtFloorDaCapoBoss.HitTexturePath);
        profile.Frame("guard", ArtFloorDaCapoBoss.GuardTexturePath);
        profile.Frame("special", ArtFloorDaCapoBoss.SpecialTexturePath);
        profile.Swap("attack", 0.55f, "Attack");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("guard", 0.55f, "Guard");
        profile.Swap("special", 0.80f, "Special");
        return profile;
    }
}

public sealed partial class ArtFloorFirstPerformerCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorFirstPerformer.IdleTexturePath);
        return profile;
    }
}

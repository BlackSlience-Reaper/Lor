using LibraryOfRuina.monsters.SmilingBodies;

namespace LibraryOfRuina.visuals.SmilingBodies;

public sealed partial class MeltingCorpseCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            MeltingCorpse.IdleTexturePath);
        profile.Swap("@idle:default", 0.38f, "Moan", "Spawn");
        profile.Swap("@idle:default", 0.28f, "Hit");
        return profile;
    }
}

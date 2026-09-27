namespace LibraryOfRuina.visuals.PriceOfSilence;

public sealed partial class PriceOfSilenceCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            monsters.PriceOfSilence.PriceOfSilence.IdleTexturePath);
        profile.Frame(
                "special",
                monsters.PriceOfSilence.PriceOfSilence.SpecialTexturePath)
            .Nudge(-18f, 10f);
        profile.Swap("special", 0.62f, "Special", "Attack", "Cast");
        profile.Swap("@idle:default", 0.28f, "Hit");
        return profile;
    }
}

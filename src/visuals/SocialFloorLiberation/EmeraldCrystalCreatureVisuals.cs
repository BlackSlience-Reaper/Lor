namespace LibraryOfRuina.visuals.SocialFloorLiberation;

public sealed partial class EmeraldCrystalCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal const string DefaultTexturePath =
        "res://images/monsters/social_floor_liberation/emerald_crystal/default.png";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            DefaultTexturePath);
        string idle = $"@idle:{SpriteVisualProfile.DefaultVariantKey}";
        profile.Swap(
            idle,
            0.22f,
            "Attack",
            "Hit",
            "Damaged",
            "Guard",
            "Block");
        return profile;
    }
}

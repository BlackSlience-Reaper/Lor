using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.SocialFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.SocialFloorLiberation;

public sealed partial class EmeraldCrystalCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(EmeraldCrystal))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.38f, 0.38f), -95f, -210f, 95f, 8f, new(0f, -98f), new(0f, -240f))
    {
        StateDisplayLiftY = 10f,
    };

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

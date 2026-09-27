namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class TheFourthMatchFlameCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root = "res://images/monsters/";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + "the_fourth_match_flame.png");
        profile.Frame(
                "attack",
                root + "the_fourth_match_flame_attack.webp")
            .Nudge(12f, -54f)
            .Scale(0.42f);
        profile.Frame(
            "hit",
            root + "the_fourth_match_flame_hit.webp");
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

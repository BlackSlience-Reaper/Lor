namespace LibraryOfRuina.visuals.ScorchedGirl;

public partial class ScorchedGirlMonsterCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root =
            "res://images/monsters/scorched_girl_monster";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + ".png");
        profile.Frame("attack", root + "_attack.webp")
            .Nudge(24f, -98f)
            .Scale(0.56f);
        profile.Frame("hit", root + "_hit.webp");
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

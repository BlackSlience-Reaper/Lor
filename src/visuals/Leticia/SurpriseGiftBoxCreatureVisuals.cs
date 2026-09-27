namespace LibraryOfRuina.visuals.Leticia;

public partial class SurpriseGiftBoxCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root =
            "res://images/monsters/leticia/surprise_gift_box_";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + "idle.png");
        profile.Frame("attack", root + "attack.png")
            .Nudge(23.4f, -106.6f)
            .Scale(0.65f);
        profile.Frame("cast", root + "cast.png");
        profile.Frame("hit", root + "hit.png");
        profile.Lunge("attack", 0.16f, 0.08f, 0.2f, "Attack");
        profile.Swap("cast", 0.36f, "Cast");
        profile.Swap("hit", 0.36f, "Hit");
        return profile;
    }
}

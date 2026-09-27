namespace LibraryOfRuina.visuals.Leticia;

public partial class LeticiaCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root = "res://images/monsters/leticia/leticia_";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + "idle.png");
        profile.Frame("attack", root + "attack.png")
            .Nudge(24f, -126f)
            .Scale(0.58f);
        profile.Frame("guard", root + "guard.png");
        profile.Frame("hit", root + "hit.png");
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Swap("guard", 0.42f, "Cast");
        profile.Swap("hit", 0.42f, "Hit");
        return profile;
    }
}

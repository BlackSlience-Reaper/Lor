using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.ForsakenMurderer;

public partial class ForsakenMurdererCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.ForsakenMurderer.ForsakenMurderer))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -100f), new(0.31f, 0.31f), -160f, -250f, 160f, 20f, new(0f, -100f), new(0f, -280f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root = "res://images/monsters/forsaken_murderer";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + ".webp");
        profile.Frame("attack", root + "_attack.webp")
            .Nudge(18f, -100f)
            .Scale(0.33f);
        profile.Frame("hit", root + "_hit.webp");
        profile.Lunge("attack", 0.18f, 0.1f, 0.22f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

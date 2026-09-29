using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.Leticia;

public partial class SurpriseGiftBoxCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(SurpriseGiftBox))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -98.8f), new(0.806f, 0.806f), -72.8f, -260f, 72.8f, 10.4f, new(0f, -106.6f), new(0f, -306.8f));

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

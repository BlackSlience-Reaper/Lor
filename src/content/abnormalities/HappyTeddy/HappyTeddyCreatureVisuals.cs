using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.HappyTeddy;

public partial class HappyTeddyCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HappyTeddyMonster))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(6f, -120f), new(0.58f, 0.58f), -145f, -320f, 145f, 10f, new(6f, -120f), new(-4f, -340f))
    {
        TalkPos = new Vector2(-8f, -272f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root = "res://images/monsters/happy_teddy";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + ".webp");
        profile.Frame("attack", root + "_attack_1.webp")
            .Nudge(20f, -120f)
            .Scale(0.60f);
        profile.Frame("nostalgic_embrace", root + "_attack_2.webp")
            .Nudge(30f, -112f)
            .Scale(0.62f);
        profile.Frame("hit", root + "_hit.webp");
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Lunge(
            "nostalgic_embrace",
            0.2f,
            0.1f,
            0.25f,
            "NostalgicEmbrace");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

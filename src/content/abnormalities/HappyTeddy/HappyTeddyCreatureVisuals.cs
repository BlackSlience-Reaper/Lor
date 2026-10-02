using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.HappyTeddy;

public partial class HappyTeddyCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HappyTeddyMonster))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(6f, -120f), new(0.58f, 0.58f), -120f, -272f, 122f, 11f, new(6f, -120f), new(-4f, -340f))
    {
        TalkPos = new Vector2(-8f, -272f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HappyTeddyAssets.HappyTeddyMonsterPrefix + ".webp");
        profile.Frame("attack", HappyTeddyAssets.HappyTeddyMonsterPrefix + "_attack_1.webp")
            .Nudge(20f, -120f)
            .Scale(0.60f);
        profile.Frame("nostalgic_embrace", HappyTeddyAssets.HappyTeddyMonsterPrefix + "_attack_2.webp")
            .Nudge(30f, -112f)
            .Scale(0.62f);
        profile.Frame("hit", HappyTeddyAssets.HappyTeddyMonsterPrefix + "_hit.webp");
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

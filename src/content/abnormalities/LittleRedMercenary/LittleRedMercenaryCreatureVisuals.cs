using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

public partial class LittleRedMercenaryCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(LittleRedRidingHoodedMercenary))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -138f), new(-0.71f, 0.71f), -101f, -356f, 101f, 8f, new(0f, -146f), new(13f, -349f))
    {
        TalkPos = new Vector2(18f, -310f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public void FacePlayers()
    {
        SetSpriteFlipH(true);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        const string root =
            "res://images/monsters/little_red_mercenary/";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                root + "idle.png")
            .At(-8f, -130f)
            .Scale(-0.74f, 0.74f)
            .AnchorX(76f)
            .IdleOnly();

        profile.Frame("attack_1", root + "attack_1.png")
            .Nudge(24f, -140f)
            .Scale(-0.76f, 0.76f)
            .AnchorX(274f);
        profile.Frame("attack_2", root + "attack_2.png")
            .Nudge(24f, -140f)
            .Scale(-0.76f, 0.76f)
            .AnchorX(438f);
        profile.Frame("fire_1", root + "fire_1.png")
            .Nudge(30f, -140f)
            .Scale(-0.58f, 0.58f)
            .AnchorX(570f);
        profile.Frame("fire_2", root + "fire_2.png")
            .Nudge(30f, -140f)
            .Scale(-0.58f, 0.58f)
            .AnchorX(614f);
        profile.Frame("fire_3", root + "fire_3.png")
            .Nudge(30f, -140f)
            .Scale(-0.58f, 0.58f)
            .AnchorX(525f);
        profile.Frame("fire_4", root + "fire_4.png")
            .Nudge(30f, -140f)
            .Scale(-0.58f, 0.58f)
            .AnchorX(1297f);
        profile.Frame("hit", root + "hit.png");

        profile.Lunge(
                ["attack_1", "attack_2"],
                0.18f,
                0.08f,
                0.22f,
                "Attack")
            .Cycle();
        profile.Lunge(
                ["fire_1", "fire_2", "fire_3", "fire_4"],
                0.18f,
                0.08f,
                0.22f,
                "Fire")
            .Cycle();
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

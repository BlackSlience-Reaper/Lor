using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.guests.DawnOffice;

public partial class SayoCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(Sayo))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-30f, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, 5f, new(-30f, -139.8f), new(-30f, -333.7f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            "res://images/monsters/sayo.png");
        profile.Frame(
                "strike",
                "res://images/monsters/sayo_attack_strike.png")
            .Nudge(15f, -145.2f)
            .Scale(0.51f);
        profile.Frame(
                "thrust",
                "res://images/monsters/sayo_attack_thrust.png")
            .Nudge(0f, -145.2f)
            .Scale(0.403f);
        profile.Frame(
                "slash",
                "res://images/monsters/sayo_attack_slash.png")
            .Nudge(20f, -145.2f)
            .Scale(0.482f);
        profile.Frame("hit", "res://images/monsters/sayo_hit.webp");
        profile.Lunge(
            "strike",
            0.2f,
            0.1f,
            0.25f,
            "AttackStrike");
        profile.Lunge(
            "thrust",
            0.2f,
            0.1f,
            0.25f,
            "AttackThrust");
        profile.Lunge(
            "slash",
            0.2f,
            0.1f,
            0.25f,
            "AttackSlash");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

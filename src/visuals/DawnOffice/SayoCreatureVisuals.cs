namespace LibraryOfRuina.visuals.DawnOffice;

public partial class SayoCreatureVisuals : SpriteAttackCreatureVisuals
{
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

namespace LibraryOfRuina.visuals.BrotherhoodOfIron;

public abstract partial class BrotherhoodOfIronCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static SpriteVisualProfile BuildProfile(
        string textureName,
        float attackEndX,
        float attackEndY,
        float attackScale)
    {
        string root =
            $"res://images/monsters/brotherhood_of_iron/{textureName}";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            root + ".webp");
        profile.Frame("thrust", root + "_attack_thrust.webp")
            .Nudge(attackEndX, attackEndY)
            .Scale(attackScale);
        profile.Frame("strike", root + "_attack_strike.webp")
            .Nudge(attackEndX, attackEndY)
            .Scale(attackScale);
        profile.Frame("slash", root + "_attack_slash.webp")
            .Nudge(attackEndX, attackEndY)
            .Scale(attackScale);
        profile.Frame("hit", root + "_hit.webp");
        profile.Lunge(
                ["thrust", "strike", "slash"],
                0.18f,
                0.08f,
                0.22f,
                "Attack")
            .Cycle();
        profile.Swap("hit", 0.12f, "Hit");
        profile.Swap(
            $"@idle:{SpriteVisualProfile.DefaultVariantKey}",
            0.12f,
            "Cast");
        return profile;
    }
}

public partial class MoCreatureVisuals : BrotherhoodOfIronCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildProfile("mo", 26f, -145.2f, 0.54f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}

public partial class ConstaCreatureVisuals : BrotherhoodOfIronCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildProfile("consta", 24f, -145.2f, 0.52f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}

public partial class ArnoldCreatureVisuals : BrotherhoodOfIronCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        BuildProfile("arnold", 24f, -145.2f, 0.52f);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}

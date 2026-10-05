using Godot;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.content.guests.MusiciansOfBremen;
using LibraryOfRuina.content.guests.WedgeOffice;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.guests.BrotherhoodOfIron;

public abstract partial class BrotherhoodOfIronCreatureVisuals : SpineSpriteAttackCreatureVisuals
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
    [MonsterVisual(typeof(Mo))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145.2f), new(0.48f, 0.48f), -120f, -293f, 120f, -1f, new(0f, -139.8f), new(0f, -333.7f))
    {
        TalkPos = new Vector2(0f, -264f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildProfile("mo", 26f, -145.2f, 0.54f);

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine =
        GuestSpine.Create("brotherhood_of_iron/", "mo", GuestSpine.ThrustStrikeSlash);
}

public partial class ConstaCreatureVisuals : BrotherhoodOfIronCreatureVisuals
{
    [MonsterVisual(typeof(Consta))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, -2f, new(0f, -139.8f), new(0f, -333.7f))
    {
        TalkPos = new Vector2(0f, -264f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildProfile("consta", 24f, -145.2f, 0.52f);

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine =
        GuestSpine.Create("brotherhood_of_iron/", "consta", GuestSpine.ThrustStrikeSlash);
}

public partial class ArnoldCreatureVisuals : BrotherhoodOfIronCreatureVisuals
{
    [MonsterVisual(typeof(Arnold))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -145.2f), new(0.48f, 0.48f), -120f, -299.7f, 120f, -4f, new(0f, -139.8f), new(0f, -333.7f))
    {
        TalkPos = new Vector2(0f, -264f),
    };

    internal static readonly SpriteVisualProfile Profile =
        BuildProfile("arnold", 24f, -145.2f, 0.52f);

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    private static readonly RuntimeSpineBody.Spec Spine =
        GuestSpine.Create("brotherhood_of_iron/", "arnold", GuestSpine.ThrustStrikeSlash);
}

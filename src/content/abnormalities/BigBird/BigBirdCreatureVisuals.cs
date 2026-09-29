using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.BigBird;

public sealed partial class BigBirdCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(BigBird))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -12f), new(0.60f, 0.60f), -190f, -420f, 190f, 16f, new(0f, -204f), new(44f, -372f))
    {
        TalkPos = new Vector2(0f, -356f),
        StateDisplayLiftY = 36f,
    };

    private const string NormalVariant = "normal";
    private const string SleepVariant = "sleep";
    private const string RescueVariant = "rescue";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public void UpdateIdleForIntent(string moveId)
    {
        SetSpriteVisualVariant(
            moveId == BigBird.RescueMoveId
                ? RescueVariant
                : NormalVariant);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            NormalVariant,
            BigBird.IdleTexturePath);
        profile.Variant(
            SleepVariant,
            BigBird.SleepTexturePath);
        profile.Variant(
            RescueVariant,
            BigBird.RescueOpenTexturePath);
        profile.InitialVariant(NormalVariant);

        profile.Frame(
            "hit",
            BigBird.HitTexturePath);
        profile.Frame(
            "guard",
            BigBird.GuardTexturePath);
        profile.Frame(
            "charm",
            BigBird.CharmTexturePath);
        profile.Frame(
            "rescue_close",
            BigBird.RescueCloseTexturePath);

        profile.Swap("@idle:normal", 0.01f, "Idle")
            .SwitchToVariant(NormalVariant);
        profile.Swap("guard", 0.36f, "Guard", "Block");
        profile.Swap("charm", 0.72f, "Charm", "Cast");
        profile.Swap("rescue_close", 0.82f, "Rescue", "Attack");
        profile.Swap("@idle:sleep", 0.5f, "Sleep")
            .SwitchToVariant(SleepVariant);
        profile.Swap("hit", 0.32f, "Hit");
        return profile;
    }
}

public sealed partial class EyeballBirdCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(EyeballBird))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 13f), new(0.56f, 0.56f), -104f, -222f, 104f, 10f, new(0f, -96f), new(0f, -238f))
    {
        TalkPos = new Vector2(0f, -202f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            EyeballBird.IdleTexturePath);
        profile.Frame("hit", EyeballBird.HitTexturePath);
        profile.Frame("attack", EyeballBird.AttackTexturePath);
        profile.Frame("evade", EyeballBird.EvadeTexturePath);
        profile.Swap("attack", 0.34f, "Attack", "Thrust");
        profile.Swap("evade", 0.34f, "Evade", "Dodge");
        profile.Swap("hit", 0.26f, "Hit");
        return profile;
    }
}

using LibraryOfRuina.monsters.BigBird;

namespace LibraryOfRuina.visuals.BigBird;

public sealed partial class BigBirdCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    private const string NormalVariant = "normal";
    private const string SleepVariant = "sleep";
    private const string RescueVariant = "rescue";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public void UpdateIdleForIntent(string moveId)
    {
        SetSpriteVisualVariant(
            moveId == monsters.BigBird.BigBird.RescueMoveId
                ? RescueVariant
                : NormalVariant);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            NormalVariant,
            monsters.BigBird.BigBird.IdleTexturePath);
        profile.Variant(
            SleepVariant,
            monsters.BigBird.BigBird.SleepTexturePath);
        profile.Variant(
            RescueVariant,
            monsters.BigBird.BigBird.RescueOpenTexturePath);
        profile.InitialVariant(NormalVariant);

        profile.Frame(
            "hit",
            monsters.BigBird.BigBird.HitTexturePath);
        profile.Frame(
            "guard",
            monsters.BigBird.BigBird.GuardTexturePath);
        profile.Frame(
            "charm",
            monsters.BigBird.BigBird.CharmTexturePath);
        profile.Frame(
            "rescue_close",
            monsters.BigBird.BigBird.RescueCloseTexturePath);

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

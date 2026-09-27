namespace LibraryOfRuina.visuals.BurrowingHeaven;

public sealed partial class BurrowingHeavenCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    private const string AwakeVariant = "awake";
    private const string SleepVariant = "sleep";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private bool _ready;

    public bool StartsAwake { get; set; } = true;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        _ready = true;
        SetInitialState();
    }

    private void SetInitialState()
    {
        if (_ready)
        {
            SetSpriteVisualVariant(
                StartsAwake ? AwakeVariant : SleepVariant);
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            AwakeVariant,
            monsters.BurrowingHeaven.BurrowingHeaven.AwakeTexturePath);
        profile.Variant(
            SleepVariant,
            monsters.BurrowingHeaven.BurrowingHeaven.SleepTexturePath);
        profile.InitialVariant(AwakeVariant);
        profile.Frame(
            "attack",
            monsters.BurrowingHeaven.BurrowingHeaven.AttackTexturePath);
        profile.Frame(
            "hit",
            monsters.BurrowingHeaven.BurrowingHeaven.HitTexturePath);
        profile.Frame(
            "special",
            monsters.BurrowingHeaven.BurrowingHeaven.SpecialTexturePath);
        profile.Frame(
            "guard",
            monsters.BurrowingHeaven.BurrowingHeaven.GuardTexturePath);
        profile.Swap("@idle:awake", 0.18f, "Awake")
            .SwitchToVariant(AwakeVariant);
        profile.Swap("@idle:sleep", 0.18f, "Sleep")
            .SwitchToVariant(SleepVariant);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("special", 0.66f, "Special");
        profile.Swap("guard", 0.34f, "Guard");
        profile.Swap("hit", 0.32f, "Hit");
        return profile;
    }
}

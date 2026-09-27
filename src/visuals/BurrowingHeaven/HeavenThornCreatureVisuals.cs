using LibraryOfRuina.monsters.BurrowingHeaven;

namespace LibraryOfRuina.visuals.BurrowingHeaven;

public sealed partial class HeavenThornCreatureVisuals
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
        profile.Variant(AwakeVariant, HeavenThorn.AwakeTexturePath);
        profile.Variant(SleepVariant, HeavenThorn.SleepTexturePath);
        profile.InitialVariant(AwakeVariant);
        profile.Frame("attack", HeavenThorn.AttackTexturePath);
        profile.Frame("hit", HeavenThorn.HitTexturePath);
        profile.Frame("guard", HeavenThorn.GuardTexturePath);
        profile.Swap("@idle:awake", 0.16f, "Awake")
            .SwitchToVariant(AwakeVariant);
        profile.Swap("@idle:sleep", 0.16f, "Sleep")
            .SwitchToVariant(SleepVariant);
        profile.Swap("attack", 0.36f, "Attack");
        profile.Swap("guard", 0.36f, "Guard");
        profile.Swap("hit", 0.28f, "Hit");
        return profile;
    }
}

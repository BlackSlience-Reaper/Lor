using LibraryOfRuina.monsters.Ozma;

namespace LibraryOfRuina.visuals.Ozma;

public sealed partial class OzmaCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            monsters.Ozma.Ozma.IdleTexturePath);
        profile.Frame("attack", monsters.Ozma.Ozma.AttackTexturePath);
        profile.Frame("hit", monsters.Ozma.Ozma.HitTexturePath);
        profile.Frame("guard", monsters.Ozma.Ozma.GuardTexturePath);
        profile.Frame("pain", monsters.Ozma.Ozma.PainTexturePath);
        profile.Frame("sorrow", monsters.Ozma.Ozma.SorrowTexturePath);
        profile.Swap("attack", 0.48f, "Attack");
        profile.Swap("guard", 0.48f, "Guard", "Block", "Cast");
        profile.Swap(
            "pain",
            0.92f,
            "LifePowder",
            "Pain",
            "SpecialAttack");
        profile.Swap("sorrow", 1.05f, "Sorrow");
        profile.Swap("hit", 0.3f, "Hit");
        return profile;
    }
}

public sealed partial class OzmaJackCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    private const string DormantVariant = "dormant";
    private const string AwakeVariant = "awake";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private bool _ready;

    public bool StartsAwake { get; set; }

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        _ready = true;
        SetAwake(StartsAwake);
    }

    public void SetAwake(bool awake)
    {
        if (_ready)
        {
            SetSpriteVisualVariant(
                awake ? AwakeVariant : DormantVariant);
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            DormantVariant,
            OzmaJack.DormantTexturePath);
        profile.Variant(
            AwakeVariant,
            OzmaJack.AwakeTexturePath);
        profile.InitialVariant(DormantVariant);
        profile.Frame("hit", OzmaJack.HitTexturePath);
        profile.Swap("@idle:awake", 0.5f, "Awake")
            .SwitchToVariant(AwakeVariant);
        profile.Swap("@idle:dormant", 0.01f, "Idle")
            .ForVariant(DormantVariant);
        profile.Swap("@idle:awake", 0.01f, "Idle")
            .ForVariant(AwakeVariant);
        profile.Swap("hit", 0.28f, "Hit");
        return profile;
    }
}

namespace LibraryOfRuina.visuals.Nosferatu;

public partial class NosferatuCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    private const string NormalVariant = "normal";
    private const string BloodfiendVariant = "bloodfiend";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private bool _isBloodfiend;
    private bool _ready;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        _ready = true;
        RefreshVariant();
    }

    public void SetBloodfiendForm(bool isBloodfiend)
    {
        _isBloodfiend = isBloodfiend;
        if (_ready)
        {
            RefreshVariant();
        }
    }

    private void RefreshVariant()
    {
        SetSpriteVisualVariant(
            _isBloodfiend ? BloodfiendVariant : NormalVariant);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        string root = monsters.Nosferatu.Nosferatu.TextureRoot;
        var profile = new SpriteVisualProfile();
        profile.Variant(
                NormalVariant,
                root + "nosferatu_idle.png")
            .Scale(0.72f);
        profile.Variant(
                BloodfiendVariant,
                root + "nosferatu_bloodfiend_idle.png")
            .Scale(0.76f);
        profile.InitialVariant(NormalVariant);

        profile.Frame(
            "normal_attack",
            root + "nosferatu_attack.png")
            .ForVariant(NormalVariant);
        profile.Frame(
            "normal_group_attack",
            root + "nosferatu_group_attack.png")
            .ForVariant(NormalVariant);
        profile.Frame(
            "normal_guard",
            root + "nosferatu_guard.png")
            .ForVariant(NormalVariant);
        profile.Frame(
            "normal_hit",
            root + "nosferatu_hit.png")
            .ForVariant(NormalVariant);

        profile.Frame(
            "bloodfiend_strike",
            root + "nosferatu_bloodfiend_strike.png")
            .ForVariant(BloodfiendVariant);
        profile.Frame(
            "bloodfiend_pierce",
            root + "nosferatu_bloodfiend_pierce.png")
            .ForVariant(BloodfiendVariant);
        profile.Frame(
            "bloodfiend_slash",
            root + "nosferatu_bloodfiend_slash.png")
            .ForVariant(BloodfiendVariant);
        profile.Frame(
            "bloodfiend_group_attack",
            root + "nosferatu_bloodfiend_group_attack.png")
            .ForVariant(BloodfiendVariant);
        profile.Frame(
            "bloodfiend_guard",
            root + "nosferatu_bloodfiend_guard.png")
            .ForVariant(BloodfiendVariant);
        profile.Frame(
            "bloodfiend_hit",
            root + "nosferatu_bloodfiend_hit.png")
            .ForVariant(BloodfiendVariant);

        profile.Swap("normal_attack", 0.48f, "Attack")
            .ForVariant(NormalVariant);
        profile.Swap(
                "normal_group_attack",
                0.72f,
                "Special",
                "SpecialAttack")
            .ForVariant(NormalVariant);
        profile.Swap("normal_guard", 0.48f, "Cast")
            .ForVariant(NormalVariant);
        profile.Swap("normal_hit", 0.18f, "Hit")
            .ForVariant(NormalVariant);

        profile.Swap(
                [
                    "bloodfiend_strike",
                    "bloodfiend_pierce",
                    "bloodfiend_slash"
                ],
                0.48f,
                "Attack")
            .ForVariant(BloodfiendVariant)
            .Cycle();
        profile.Swap(
                "bloodfiend_group_attack",
                0.72f,
                "Special",
                "SpecialAttack")
            .ForVariant(BloodfiendVariant);
        profile.Swap("bloodfiend_guard", 0.48f, "Cast")
            .ForVariant(BloodfiendVariant);
        profile.Swap("bloodfiend_hit", 0.18f, "Hit")
            .ForVariant(BloodfiendVariant);
        return profile;
    }
}

public partial class BloodBatCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        string root = monsters.Nosferatu.Nosferatu.TextureRoot;
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                root + "blood_bat_idle.png")
            .Scale(0.78f);
        profile.Frame("ranged", root + "blood_bat_ranged.png");
        profile.Frame("attack", root + "blood_bat_attack.png");
        profile.Frame("evade", root + "blood_bat_evade.png");
        profile.Swap(["ranged", "attack"], 0.42f, "Attack")
            .Cycle();
        profile.Swap("evade", 0.38f, "Cast");
        profile.Swap("evade", 0.16f, "Hit");
        return profile;
    }
}

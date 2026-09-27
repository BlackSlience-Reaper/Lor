namespace LibraryOfRuina.visuals.QueenOfHatred;

public partial class QueenOfHatredCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    private const string HumanVariant = "human";
    private const string SnakeVariant = "snake";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private bool _isSnakeForm;
    private bool _ready;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        _ready = true;
        RefreshVariant();
    }

    public void SetSnakeForm(bool isSnakeForm)
    {
        _isSnakeForm = isSnakeForm;
        if (_ready)
        {
            RefreshVariant();
        }
    }

    private void RefreshVariant()
    {
        SetSpriteVisualVariant(
            _isSnakeForm ? SnakeVariant : HumanVariant);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        const string root = "res://images/monsters/";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                HumanVariant,
                root + "queen_of_hatred.webp")
            .At(0f, -118f)
            .Scale(0.84f)
            .IdleOnly();
        profile.Variant(
                SnakeVariant,
                root + "queen_of_hatred_snake.webp")
            .At(0f, -118f)
            .Scale(0.54f)
            .IdleOnly();
        profile.InitialVariant(HumanVariant);

        AddForm(
            profile,
            HumanVariant,
            root + "queen_of_hatred_attack_",
            root + "queen_of_hatred_hit.webp",
            attackScale: 0.68f,
            random: false);
        AddForm(
            profile,
            SnakeVariant,
            root + "queen_of_hatred_snake_attack_",
            root + "queen_of_hatred_snake_hit.webp",
            attackScale: 0.58f,
            random: true);
        return profile;
    }

    private static void AddForm(
        SpriteVisualProfile profile,
        string variant,
        string attackPrefix,
        string hitPath,
        float attackScale,
        bool random)
    {
        string[] attacks = new string[3];
        for (int index = 0; index < attacks.Length; index++)
        {
            string key = $"{variant}_attack_{index + 1}";
            attacks[index] = key;
            profile.Frame(key, $"{attackPrefix}{index + 1}.webp")
                .ForVariant(variant)
                .Nudge(24f, -118f)
                .Scale(attackScale);
        }

        string hit = variant + "_hit";
        profile.Frame(hit, hitPath)
            .ForVariant(variant);
        SpriteAnimationDefinition attack = profile.Lunge(
                attacks,
                0.18f,
                0.08f,
                0.22f,
                "Attack")
            .ForVariant(variant);
        if (random)
        {
            attack.Random();
        }
        else
        {
            attack.Cycle();
        }

        profile.Swap(hit, 0.12f, "Hit")
            .ForVariant(variant);
    }
}

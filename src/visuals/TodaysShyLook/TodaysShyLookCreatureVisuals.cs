using Godot;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.TodaysShyLook;

public partial class TodaysShyLookCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.TodaysShyLook.TodaysShyLook))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -142f), new(0.58f, 0.58f), -118f, -330f, 118f, 12f, new(0f, -142f), new(0f, -366f))
    {
        TalkPos = new Vector2(0f, -286f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private int _currentExpression = 5;
    private bool _ready;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();

        if (GetParent() is NCreature creatureNode && creatureNode.Entity.Monster is monsters.TodaysShyLook.TodaysShyLook todaysShyLook)
        {
            _currentExpression = todaysShyLook.CurrentExpression;
        }

        _ready = true;
        ApplyExpression();
    }

    public void SetExpression(int expression)
    {
        _currentExpression = Mathf.Clamp(expression, 1, 5);
        if (_ready)
        {
            ApplyExpression();
        }
    }

    private void ApplyExpression()
    {
        SetSpriteVisualVariant(VariantKey(_currentExpression));
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        for (int expression = 1; expression <= 5; expression++)
        {
            string variant = VariantKey(expression);
            string attack = variant + "_attack";
            string hit = variant + "_hit";
            profile.Variant(
                variant,
                TexturePath(expression, "idle"));
            profile.Frame(attack, TexturePath(expression, "attack"))
                .ForVariant(variant);
            profile.Frame(hit, TexturePath(expression, "hit"))
                .ForVariant(variant);
            profile.Swap(attack, 0.18f, "Attack")
                .ForVariant(variant);
            profile.Swap(hit, 0.12f, "Hit")
                .ForVariant(variant);
            profile.Swap(
                    $"@idle:{variant}",
                    0.18f,
                    "Cast")
                .ForVariant(variant);
        }

        profile.InitialVariant(VariantKey(5));
        return profile;
    }

    private static string VariantKey(int expression) =>
        $"expression_{expression}";

    private static string TexturePath(int expression, string suffix) =>
        "res://images/monsters/todays_shy_look/"
        + $"todays_shy_look_{expression}_{suffix}.webp";
}

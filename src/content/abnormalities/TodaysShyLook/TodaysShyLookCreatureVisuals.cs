using System.Collections.Generic;
using System.Linq;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.TodaysShyLook;

/// <summary>
/// 今天也很害羞的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。
/// 原版每个动作是五种表情的全身整图叠在一起，没有身体部件，所以每种表情一副骨架、只有整体动作，按当前表情切换。
/// </summary>
public partial class TodaysShyLookCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec[] ExpressionSpines =
        Enumerable.Range(1, 5).Select(expression => LayeredBossSpine.Create(
            "todays_shy_look",
            $"shy_look_{expression}",
            "attack",
            new Dictionary<string, string>
            {
                ["Cast"] = "cast",
            })).ToArray();

    internal override RuntimeSpineBody.Spec SpineSpec => ExpressionSpines[4];

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => ExpressionSpines;

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey)
    {
        for (int expression = 1; expression <= 5; expression++)
        {
            if (variantKey == VariantKey(expression))
            {
                return ExpressionSpines[expression - 1];
            }
        }

        return SpineSpec;
    }

    [MonsterVisual(typeof(TodaysShyLook))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -142f), new(0.58f, 0.58f), -126f, -330f, 118f, 12f, new(0f, -142f), new(0f, -366f))
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

        if (GetParent() is NCreature creatureNode && creatureNode.Entity.Monster is TodaysShyLook todaysShyLook)
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

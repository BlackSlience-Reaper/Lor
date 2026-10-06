using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

/// <summary>
/// 憎恶皇后：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，整图只平移转动，各姿势按标注点对齐），
/// 加载失败时退回逐帧换图。人形、蛇形两个形态各一副骨架；攻击轮流用三张攻击图
/// （蛇形原来的换图是随机挑一张，Spine 这里也按顺序轮流）。
/// </summary>
public partial class QueenOfHatredCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec HumanSpine = LayeredBossSpine.Create(
        "queen_of_hatred",
        "queen_of_hatred_human",
        "attack1",
        new Dictionary<string, string>()) with { AttackCycle = ["attack1", "attack2", "attack3"] };

    internal static readonly RuntimeSpineBody.Spec SnakeSpine = LayeredBossSpine.Create(
        "queen_of_hatred",
        "queen_of_hatred_snake",
        "attack1",
        new Dictionary<string, string>()) with { AttackCycle = ["attack1", "attack2", "attack3"] };

    internal override RuntimeSpineBody.Spec SpineSpec => HumanSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [HumanSpine, SnakeSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) =>
        variantKey == SnakeVariant ? SnakeSpine : HumanSpine;

    [MonsterVisual(typeof(QueenOfHatred))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -118f), new(0.84f, 0.84f), -155f, -265f, 120f, 8f, new(0f, -120f), new(0f, -315f));

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
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                HumanVariant,
                QueenOfHatredAssets.ImagesMonstersRoot + "queen_of_hatred.webp")
            .At(0f, -118f)
            .Scale(0.84f)
            .IdleOnly();
        profile.Variant(
                SnakeVariant,
                QueenOfHatredAssets.ImagesMonstersRoot + "queen_of_hatred_snake.webp")
            .At(0f, -118f)
            .Scale(0.54f)
            .IdleOnly();
        profile.InitialVariant(HumanVariant);

        AddForm(
            profile,
            HumanVariant,
            QueenOfHatredAssets.ImagesMonstersRoot + "queen_of_hatred_attack_",
            QueenOfHatredAssets.ImagesMonstersRoot + "queen_of_hatred_hit.webp",
            attackScale: 0.68f,
            random: false);
        AddForm(
            profile,
            SnakeVariant,
            QueenOfHatredAssets.ImagesMonstersRoot + "queen_of_hatred_snake_attack_",
            QueenOfHatredAssets.ImagesMonstersRoot + "queen_of_hatred_snake_hit.webp",
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

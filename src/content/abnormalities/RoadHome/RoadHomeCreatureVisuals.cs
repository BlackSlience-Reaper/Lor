using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.RoadHome;

/// <summary>
/// 归家的路途：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，整图只平移转动，各姿势按标注点对齐），
/// 加载失败时退回逐帧换图。普通、混乱两个形态各一副骨架。
/// </summary>
public sealed partial class RoadHomeCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    private static Dictionary<string, string> SpineTriggers() => new()
    {
        ["Attack2"] = "attack2",
        ["Attack3"] = "attack3",
        ["BadWizard"] = "attack3",
        ["Hide"] = "dodge",
        ["Guard"] = "dodge",
        ["Cast"] = "dodge",
    };

    internal static readonly RuntimeSpineBody.Spec NormalSpine = LayeredBossSpine.Create(
        "road_home",
        "road_home_normal",
        "attack",
        SpineTriggers());

    internal static readonly RuntimeSpineBody.Spec ConfusedSpine = LayeredBossSpine.Create(
        "road_home",
        "road_home_confused",
        "attack",
        SpineTriggers());

    internal override RuntimeSpineBody.Spec SpineSpec => NormalSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [NormalSpine, ConfusedSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) =>
        variantKey == ConfusedVariant ? ConfusedSpine : NormalSpine;

    [MonsterVisual(typeof(RoadHome))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -18f), new(0.60f, 0.60f), -155f, -360f, 155f, 14f, new(0f, -170f), new(0f, -390f))
    {
        TalkPos = new Vector2(0f, -316f),
        StateDisplayLiftY = 28f,
    };

    private const string NormalVariant = "normal";
    private const string ConfusedVariant = "confused";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            NormalVariant,
            RoadHome.IdleTexturePath);
        profile.Variant(
            ConfusedVariant,
            RoadHome.ConfusedTexturePath);
        profile.InitialVariant(NormalVariant);

        profile.Frame(
            "attack",
            RoadHome.AttackTexturePath);
        profile.Frame(
            "attack_2",
            RoadHome.Attack2TexturePath);
        profile.Frame(
            "attack_3",
            RoadHome.Attack3TexturePath);
        profile.Frame(
            "hit",
            RoadHome.HitTexturePath);
        profile.Frame(
            "dodge",
            RoadHome.DodgeTexturePath);

        profile.Swap("@idle:normal", 0.01f, "Idle")
            .SwitchToVariant(NormalVariant);
        profile.Swap("attack", 0.36f, "Attack");
        profile.Swap("attack_2", 0.36f, "Attack2");
        profile.Swap("attack_3", 0.48f, "Attack3", "BadWizard");
        profile.Swap("dodge", 0.42f, "Hide", "Guard", "Cast");
        profile.Swap("hit", 0.28f, "Hit");
        profile.Swap(
                "@idle:confused",
                0.5f,
                "Stunned",
                "Confused")
            .SwitchToVariant(ConfusedVariant);
        return profile;
    }
}

/// <summary>
/// 胆小的猫咪：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按标注点对齐），加载失败时退回逐帧换图。
/// 普通、同伴（归家的路途被打败后）两个形态各一副骨架；同伴那副也给 <see cref="ScaredyCatCompanionCreatureVisuals"/> 用。
/// </summary>
public partial class ScaredyCatCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec NormalSpine = LayeredBossSpine.Create(
        "scaredy_cat",
        "scaredy_cat",
        "strike",
        new Dictionary<string, string>
        {
            ["AttackStrike"] = "strike",
            ["AttackSlash"] = "slash",
            ["Ranged"] = "ranged",
            ["Dodge"] = "guard",
            ["Guard"] = "guard",
            ["Cast"] = "guard",
        });

    internal static readonly RuntimeSpineBody.Spec CompanionSpine = LayeredBossSpine.Create(
        "scaredy_cat",
        "scaredy_cat_companion",
        "attack",
        new Dictionary<string, string>
        {
            ["AttackStrike"] = "attack",
            ["AttackSlash"] = "attack",
            ["Ranged"] = "attack",
            ["Dodge"] = "guard",
            ["Guard"] = "guard",
            ["Cast"] = "guard",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => NormalSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [NormalSpine, CompanionSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) =>
        variantKey == DefeatedVariant ? CompanionSpine : NormalSpine;

    [MonsterVisual(typeof(ScaredyCat))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -12f), new(0.88f, 0.88f), -211f, -304f, 213f, 12f, new(0f, -126f), new(0f, -332f))
    {
        TalkPos = new Vector2(0f, -238f),
        StateDisplayLiftY = 14f,
    };

    private const string NormalVariant = "normal";
    private const string DefeatedVariant = "road_home_defeated";

    internal static readonly SpriteVisualProfile Profile =
        BuildScaredyProfile(
            ScaredyCat.IdleTexturePath,
            ScaredyCat.HitTexturePath,
            ScaredyCat.AttackStrikeTexturePath,
            ScaredyCat.AttackSlashTexturePath,
            ScaredyCat.AttackRangedTexturePath);

    internal override SpriteVisualProfile SpriteProfile => Profile;

    internal static SpriteVisualProfile BuildScaredyProfile(
        string idlePath,
        string hitPath,
        string? attackStrikePath,
        string? attackSlashPath,
        string? attackRangedPath)
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(NormalVariant, idlePath);
        profile.Variant(
            DefeatedVariant,
            ScaredyCat.AfterRoadHomeDefeatedIdleTexturePath);
        profile.InitialVariant(NormalVariant);

        profile.Frame(
            "normal_hit",
            hitPath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "defeated_hit",
            ScaredyCat.AfterRoadHomeDefeatedHitTexturePath)
            .ForVariant(DefeatedVariant);
        profile.Frame(
            "strike",
            attackStrikePath ?? idlePath);
        profile.Frame(
            "slash",
            attackSlashPath ?? idlePath);
        profile.Frame(
            "ranged",
            attackRangedPath ?? idlePath);

        profile.Swap("strike", 0.28f, "AttackStrike", "Attack");
        profile.Swap("slash", 0.28f, "AttackSlash");
        profile.Swap("ranged", 0.24f, "Ranged");
        profile.Swap("normal_hit", 0.24f, "Hit")
            .ForVariant(NormalVariant);
        profile.Swap("defeated_hit", 0.24f, "Hit")
            .ForVariant(DefeatedVariant);
        profile.Swap(
                "@idle:normal",
                0.3f,
                "Dodge",
                "Guard",
                "Cast")
            .ForVariant(NormalVariant);
        profile.Swap(
                "@idle:road_home_defeated",
                0.3f,
                "Dodge",
                "Guard",
                "Cast")
            .ForVariant(DefeatedVariant);
        profile.Swap("@idle:normal", 0.01f, "Idle", "Stunned")
            .ForVariant(NormalVariant);
        profile.Swap(
                "@idle:road_home_defeated",
                0.01f,
                "Idle",
                "Stunned")
            .ForVariant(DefeatedVariant);
        profile.Swap(
                "@idle:road_home_defeated",
                0.01f,
                "AfterRoadHomeDefeated")
            .SwitchToVariant(DefeatedVariant);
        return profile;
    }
}

/// <summary>家：Spine 身体见 <see cref="LayeredBossSpine"/>（整块，待机微微晃），加载失败时退回贴图。</summary>
public sealed partial class RoadHomeHouseCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "road_home",
        "road_home_house",
        "guard",
        new Dictionary<string, string>
        {
            ["Guard"] = "guard",
            ["Cast"] = "guard",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(RoadHomeHouse))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -18f), new(0.62f, 0.62f), -226f, -259f, 220f, 14f, new(0f, -124f), new(0f, -292f))
    {
        TalkPos = new Vector2(0f, -230f),
        StateDisplayLiftY = 18f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            RoadHomeHouse.IdleTexturePath);
        profile.Swap(
            $"@idle:{SpriteVisualProfile.DefaultVariantKey}",
            0.2f,
            "Hit",
            "Guard",
            "Cast");
        return profile;
    }
}

/// <summary>归家的路途里的同伴猫咪：两个形态的贴图都是同伴图，只用同伴那副骨架。</summary>
public sealed partial class ScaredyCatCompanionCreatureVisuals
    : ScaredyCatCreatureVisuals
{
    internal override RuntimeSpineBody.Spec SpineSpec => CompanionSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [CompanionSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) => CompanionSpine;

    [MonsterVisual(typeof(ScaredyCatCompanion))]
    internal new static readonly CreatureVisualLayout Layout = new(
        new(-20f, 12f), new(-0.52f, 0.52f), -118f, -246f, 118f, 12f, new(0f, -112f), new(0f, -170f))
    {
        TalkPos = new Vector2(0f, -216f),
        StateDisplayLiftY = -8f,
    };

    internal new static readonly SpriteVisualProfile Profile =
        BuildScaredyProfile(
            ScaredyCatCompanion.IdleTexturePath,
            ScaredyCatCompanion.HitTexturePath,
            attackStrikePath: null,
            attackSlashPath: null,
            attackRangedPath: null);

    internal override SpriteVisualProfile SpriteProfile => Profile;
}

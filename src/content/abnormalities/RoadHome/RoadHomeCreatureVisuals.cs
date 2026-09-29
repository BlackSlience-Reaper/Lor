using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.RoadHome;

public sealed partial class RoadHomeCreatureVisuals
    : SpriteAttackCreatureVisuals
{
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

public partial class ScaredyCatCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ScaredyCat))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -12f), new(0.88f, 0.88f), -132f, -270f, 132f, 12f, new(0f, -126f), new(0f, -332f))
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

public sealed partial class RoadHomeHouseCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(RoadHomeHouse))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -18f), new(0.62f, 0.62f), -170f, -260f, 170f, 14f, new(0f, -124f), new(0f, -292f))
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

public sealed partial class ScaredyCatCompanionCreatureVisuals
    : ScaredyCatCreatureVisuals
{
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

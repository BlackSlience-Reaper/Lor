using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.liberation.Social;

/// <summary>
/// Original False Throne action sprites flattened from the unpacked Unity
/// prefab as complete in-place composites. Each composite keeps the source
/// character root aligned even when an attack effect expands to the left.
/// </summary>
public sealed partial class FalseThroneCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(FalseThrone))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.504f, 0.504f), -190f, -620f, 190f, 10f, new(0f, -285f), new(20f, -600f))
    {
        TalkPos = new Vector2(0f, -500f),
        StateDisplayLiftY = 20f,
    };

    private const string TransformedVariantKey = "transformed";
    internal const string DefaultTexturePath =
        "res://images/monsters/social_floor_liberation/false_throne/default.png";
    internal const string DamagedTexturePath =
        "res://images/monsters/social_floor_liberation/false_throne/damaged.png";
    internal const string GuardTexturePath =
        "res://images/monsters/social_floor_liberation/false_throne/guard.png";
    internal const string FireTexturePath =
        "res://images/monsters/social_floor_liberation/false_throne/fire.png";
    internal const string OverflowingLightTexturePath =
        "res://images/monsters/social_floor_liberation/false_throne/fire_s1.png";
    internal const string AreaTexturePath =
        "res://images/monsters/social_floor_liberation/false_throne/area_s2.png";
    internal const string PolymorphTexturePath =
        "res://images/monsters/social_floor_liberation/false_throne/polymorph_s4.png";
    internal const string RageTexturePath =
        "res://images/monsters/social_floor_liberation/false_throne/rage_s3.png";

    private const float CharacterAnchorX = 220f;
    private const float DamagedAnchorX = 377f;
    private const float GuardAnchorX = 659f;
    private const float FireAnchorX = 759f;
    private const float OverflowingLightAnchorX = 867f;
    private const float AreaAnchorX = 299f;
    private const float RageAnchorX = 834f;

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    protected override void BeforeResolveSpriteTrigger(string triggerName)
    {
        switch (triggerName)
        {
            case "Attack":
            case "Fire":
            case "Insolence":
            case "Manners":
            case "FriendlyGreeting":
                SocialFloorLiberationVfx.PlayFarAttack(
                    this,
                    usePenetrateEffect: true);
                break;
            case "OverflowingLight":
                SocialFloorLiberationVfx.PlayFarAttack(
                    this,
                    usePenetrateEffect: false);
                break;
            case "AllSilent":
            case "BigMistake":
            case "FunIsOver":
            case "Area":
            case "Rage":
                SocialFloorLiberationVfx.PlayCrystalArea(this);
                break;
            case "Transform":
            case "Polymorph":
            case "MagicalPowder":
                SocialFloorLiberationVfx.PlayTransformation(this);
                break;
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                DefaultTexturePath)
            .AnchorX(CharacterAnchorX);
        profile.Variant(TransformedVariantKey, PolymorphTexturePath)
            .AnchorX(CharacterAnchorX);
        profile.Frame("damaged", DamagedTexturePath)
            .AnchorX(DamagedAnchorX)
            .GroundToIdle();
        profile.Frame("guard", GuardTexturePath)
            .AnchorX(GuardAnchorX)
            .GroundToIdle();
        profile.Frame("fire", FireTexturePath)
            .AnchorX(FireAnchorX)
            .GroundToIdle();
        profile.Frame("overflowing_light", OverflowingLightTexturePath)
            .AnchorX(OverflowingLightAnchorX)
            .GroundToIdle();
        profile.Frame("area", AreaTexturePath)
            .AnchorX(AreaAnchorX)
            .GroundToIdle();
        profile.Frame("polymorph", PolymorphTexturePath)
            .AnchorX(CharacterAnchorX)
            .GroundToIdle();
        profile.Frame("rage", RageTexturePath)
            .AnchorX(RageAnchorX)
            .GroundToIdle();

        profile.Swap("damaged", 0.32f, "Hit", "Damaged", "Hurt");
        profile.Swap("guard", 0.44f, "Guard", "Block");
        profile.Swap(
            "fire",
            0.52f,
            "Attack",
            "Fire",
            "Insolence",
            "Manners",
            "FriendlyGreeting");
        profile.Swap(
            "overflowing_light",
            0.58f,
            "OverflowingLight");
        profile.Swap(
            "area",
            0.78f,
            "AllSilent",
            "BigMistake",
            "Area");
        profile.Swap("rage", 0.92f, "FunIsOver", "Rage");
        profile.Swap(
            "polymorph",
            1.04f,
            "Transform",
            "Polymorph",
            "MagicalPowder")
            .SwitchToVariant(TransformedVariantKey);
        profile.Swap(
                "@idle:transformed",
                0.01f,
                "Transformed")
            .SwitchToVariant(TransformedVariantKey);
        return profile;
    }
}

using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Social;

public sealed partial class ScowlingFaceCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ScowlingFace))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-78f, -6f), new(0.30f, 0.30f), -170f, -180f, 14f, 8f, new(0f, -85f), new(-78f, -215f))
    {
        StateDisplayLiftY = 8f,
    };

    internal const string DefaultTexturePath =
        "res://images/monsters/social_floor_liberation/scowling_face/default.png";
    internal const string MoveTexturePath =
        "res://images/monsters/social_floor_liberation/scowling_face/move.png";
    internal const string DamagedTexturePath =
        "res://images/monsters/social_floor_liberation/scowling_face/damaged.png";
    internal const string HitTexturePath =
        "res://images/monsters/social_floor_liberation/scowling_face/hit.png";

    private const float CharacterAnchorX = 256f;
    private const float MoveAnchorX = 235f;
    private const float DamagedAnchorX = 444f;
    private const float HitAnchorX = 604f;

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                DefaultTexturePath)
            .AnchorX(CharacterAnchorX);
        profile.Frame("move", MoveTexturePath)
            .AnchorX(MoveAnchorX)
            .GroundToIdle();
        profile.Frame("damaged", DamagedTexturePath)
            .AnchorX(DamagedAnchorX)
            .GroundToIdle();
        profile.Frame("hit", HitTexturePath)
            .AnchorX(HitAnchorX)
            .GroundToIdle();
        profile.Swap("move", 0.46f, "Attack", "Move", "Stun");
        profile.Swap("damaged", 0.34f, "Damaged", "Hurt");
        profile.Swap("hit", 0.34f, "Hit");
        return profile;
    }
}

namespace LibraryOfRuina.visuals.SocialFloorLiberation;

public sealed partial class ScowlingFaceCreatureVisuals
    : SpriteAttackCreatureVisuals
{
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

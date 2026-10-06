using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Social;

/// <summary>
/// 怒视的面庞：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，飘着；各姿势按标注点对齐高度），
/// 加载失败时退回逐帧换图。攻击、移动、眩晕都是移动图。
/// </summary>
public sealed partial class ScowlingFaceCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "social_floor_liberation",
        "scowling_face",
        "move",
        new Dictionary<string, string>
        {
            ["Move"] = "move",
            ["Stun"] = "move",
            ["Damaged"] = "damaged",
            ["Hurt"] = "damaged",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(ScowlingFace))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-78f, -6f), new(0.30f, 0.30f), -170f, -180f, 14f, 8f, new(0f, -85f), new(-78f, -215f))
    {
        StateDisplayLiftY = 8f,
    };

    internal const string DefaultTexturePath =
        SocialFloorAssets.ScowlingFaceDefaultTexture;
    internal const string MoveTexturePath =
        SocialFloorAssets.ScowlingFaceMoveTexture;
    internal const string DamagedTexturePath =
        SocialFloorAssets.ScowlingFaceDamagedTexture;
    internal const string HitTexturePath =
        SocialFloorAssets.ScowlingFaceHitTexture;

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

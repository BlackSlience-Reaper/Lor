using Godot;
using LibraryOfRuina.helpers;
using LibraryOfRuina.monsters.ArtFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class ArtFloorLittleGalaxyCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ArtFloorLittleGalaxyBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -28f), new(0.52f, 0.52f), -170f, -330f, 170f, 38f, new(0f, -88f), new(0f, -420f))
    {
        TalkPos = new Vector2(0f, -240f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorLittleGalaxyBoss.IdleTexturePath);
        profile.Frame("hit", ArtFloorLittleGalaxyBoss.HitTexturePath);
        profile.Frame(
            "attack_s4",
            ArtFloorLittleGalaxyBoss.AttackFrameS4TexturePath);
        profile.Frame(
            "attack_s3",
            ArtFloorLittleGalaxyBoss.AttackFrameS3TexturePath);
        profile.Frame(
            "attack_s2",
            ArtFloorLittleGalaxyBoss.AttackFrameS2TexturePath);
        profile.Frame(
            "attack_s1",
            ArtFloorLittleGalaxyBoss.AttackFrameS1TexturePath);
        profile.Sequence(
            ["attack_s4", "attack_s3", "attack_s2", "attack_s1"],
            AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds,
            "Attack",
            "Ego");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

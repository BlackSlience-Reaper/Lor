using LibraryOfRuina.helpers;
using LibraryOfRuina.monsters.ArtFloorLiberation;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class ArtFloorLittleGalaxyCreatureVisuals : SpriteAttackCreatureVisuals
{
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

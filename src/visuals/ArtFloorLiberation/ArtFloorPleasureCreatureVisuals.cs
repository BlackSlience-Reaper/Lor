using LibraryOfRuina.helpers;
using LibraryOfRuina.monsters.ArtFloorLiberation;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class ArtFloorPleasureCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorPleasureBoss.IdleTexturePath);
        profile.Frame("hit", ArtFloorPleasureBoss.HitTexturePath);
        profile.Frame(
            "attack_blunt",
            ArtFloorPleasureBoss.AttackBluntTexturePath);
        profile.Frame(
            "attack_pierce",
            ArtFloorPleasureBoss.AttackPierceTexturePath);
        profile.Frame(
            "attack_slash",
            ArtFloorPleasureBoss.AttackSlashTexturePath);
        profile.Frame("ego_s1", ArtFloorPleasureBoss.EgoS1TexturePath);
        profile.Frame("ego_s2", ArtFloorPleasureBoss.EgoS2TexturePath);

        float segment = AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds;
        profile.Swap("attack_blunt", segment, "Blunt", "AttackBlunt");
        profile.Swap(
            "attack_pierce",
            segment,
            "Pierce",
            "AttackPierce");
        profile.Swap("attack_slash", segment, "Slash", "AttackSlash");
        profile.Swap("attack_pierce", 0.42f, "Dodge");
        profile.Swap("ego_s1", segment, "EgoS1");
        profile.Swap("ego_s2", segment, "EgoS2");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

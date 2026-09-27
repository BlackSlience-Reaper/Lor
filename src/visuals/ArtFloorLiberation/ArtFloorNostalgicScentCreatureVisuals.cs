using LibraryOfRuina.monsters.ArtFloorLiberation;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class ArtFloorNostalgicScentCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorNostalgicScentBoss.IdleTexturePath);
        profile.Frame(
            "ranged",
            ArtFloorNostalgicScentBoss.RangedTexturePath);
        profile.Frame("blunt", ArtFloorNostalgicScentBoss.BluntTexturePath);
        profile.Frame(
            "pierce",
            ArtFloorNostalgicScentBoss.PierceTexturePath);
        profile.Frame("hit", ArtFloorNostalgicScentBoss.HitTexturePath);
        profile.Frame("guard", ArtFloorNostalgicScentBoss.GuardTexturePath);
        profile.Frame("ego_s1", ArtFloorNostalgicScentBoss.EgoS1TexturePath);
        profile.Frame("ego_s2", ArtFloorNostalgicScentBoss.EgoS2TexturePath);
        profile.Frame("ego_s3", ArtFloorNostalgicScentBoss.EgoS3TexturePath);

        float segment = ArtFloorNostalgicScentBoss.AttackSegmentDelaySeconds;
        profile.Swap("ranged", segment, "Ranged");
        profile.Swap("blunt", segment, "Blunt");
        profile.Swap("pierce", segment, "Pierce");
        profile.Swap("hit", 0.5f, "Hit");
        profile.Swap("guard", 0.6f, "Guard");
        profile.Sequence(["ego_s1", "ego_s2", "ego_s3"], segment, "EgoS1");
        profile.Sequence(["ego_s2", "ego_s3", "ego_s1"], segment, "EgoS2");
        profile.Sequence(["ego_s3", "ego_s1", "ego_s2"], segment, "EgoS3");
        return profile;
    }
}

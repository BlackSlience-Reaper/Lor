using LibraryOfRuina.monsters.TechnologyFloorLiberation;

namespace LibraryOfRuina.visuals.TechnologyFloorLiberation;

public partial class TechnologyFloorGrinderMk4BossCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                TechnologyFloorGrinderMk4Boss.IdleTexturePath)
            .At(0f, -120f)
            .Scale(0.50f)
            .IdleOnly();
        profile.Frame(
            "slash",
            TechnologyFloorGrinderMk4Boss.SlashTexturePath);
        profile.Frame(
            "thrust",
            TechnologyFloorGrinderMk4Boss.ThrustTexturePath);
        profile.Frame("hit", TechnologyFloorGrinderMk4Boss.HitTexturePath);
        profile.Frame(
            "dodge",
            TechnologyFloorGrinderMk4Boss.DodgeTexturePath);
        profile.Frame(
            "ego_s1",
            TechnologyFloorGrinderMk4Boss.EgoS1TexturePath);
        profile.Frame(
            "ego_s2",
            TechnologyFloorGrinderMk4Boss.EgoS2TexturePath);
        profile.Frame(
            "ego_s3",
            TechnologyFloorGrinderMk4Boss.EgoS3TexturePath);
        profile.Swap(
            "slash",
            0.5f,
            "Attack",
            "AttackStrike",
            "AttackSlash");
        profile.Swap("thrust", 0.5f, "AttackThrust");
        profile.Swap("hit", 0.28f, "Hit");
        profile.Swap("dodge", 0.6f, "Cast");
        profile.Swap("ego_s1", 0.5f, "EgoS1");
        profile.Swap("ego_s2", 0.5f, "EgoS2");
        profile.Swap("ego_s3", 0.7f, "EgoS3");
        return profile;
    }
}

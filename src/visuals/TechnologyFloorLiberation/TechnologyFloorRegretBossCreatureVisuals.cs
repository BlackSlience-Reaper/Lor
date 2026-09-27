using LibraryOfRuina.monsters.TechnologyFloorLiberation;

namespace LibraryOfRuina.visuals.TechnologyFloorLiberation;

public partial class TechnologyFloorRegretBossCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                TechnologyFloorRegretBoss.IdleTexturePath)
            .At(0f, -117f)
            .Scale(0.50f)
            .IdleOnly();
        profile.Frame(
            "attack_right",
            TechnologyFloorRegretBoss.AttackRightTexturePath);
        profile.Frame(
            "attack_left",
            TechnologyFloorRegretBoss.AttackLeftTexturePath);
        profile.Frame(
            "attack_slash",
            TechnologyFloorRegretBoss.AttackSlashTexturePath);
        profile.Frame("hit", TechnologyFloorRegretBoss.HitTexturePath);
        profile.Frame("parry", TechnologyFloorRegretBoss.ParryTexturePath);
        profile.Frame("ego", TechnologyFloorRegretBoss.EgoTexturePath);
        profile.Swap("attack_right", 0.5f, "AttackStrike", "Attack");
        profile.Swap("attack_left", 0.5f, "AttackThrust");
        profile.Swap("attack_slash", 0.5f, "AttackSlash");
        profile.Swap("ego", 1.1f, "EgoFinal");
        profile.Swap("hit", 0.28f, "Hit");
        profile.Swap("parry", 0.6f, "Cast");
        return profile;
    }
}

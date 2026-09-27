using LibraryOfRuina.monsters.TechnologyFloorLiberation;

namespace LibraryOfRuina.visuals.TechnologyFloorLiberation;

public sealed partial class TechnologyFloorChordBossCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            TechnologyFloorChordBoss.IdleTexturePath);
        profile.Frame(
            "attack_fire",
            TechnologyFloorChordBoss.AttackFireTexturePath);
        profile.Frame(
            "attack_strike",
            TechnologyFloorChordBoss.AttackStrikeTexturePath);
        profile.Frame("hit", TechnologyFloorChordBoss.HitTexturePath);
        profile.Frame("parry", TechnologyFloorChordBoss.ParryTexturePath);
        profile.Frame("ego_s1", TechnologyFloorChordBoss.EgoS1TexturePath);
        profile.Frame("ego_s2", TechnologyFloorChordBoss.EgoS2TexturePath);
        profile.Swap("attack_fire", 0.42f, "Attack");
        profile.Swap("attack_strike", 0.42f, "AttackStrike");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("parry", 0.40f, "Guard", "Cast");
        profile.Swap("ego_s1", 1.0f, "EgoS1");
        profile.Swap("ego_s2", 1.0f, "EgoS2");
        return profile;
    }
}

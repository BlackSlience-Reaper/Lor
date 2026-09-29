using Godot;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.TechnologyFloorLiberation;

public sealed partial class TechnologyFloorMagicBulletBossCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(TechnologyFloorMagicBulletBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-95f, -155f), new(0.576f, 0.576f), -170f, -315f, 170f, 12f, new(0f, -155f), new(0f, -340f))
    {
        TalkPos = new Vector2(0f, -280f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            TechnologyFloorMagicBulletBoss.IdleTexturePath);
        profile.Frame(
            "attack",
            TechnologyFloorMagicBulletBoss.AttackTexturePath);
        profile.Frame("hit", TechnologyFloorMagicBulletBoss.HitTexturePath);
        profile.Frame(
            "special",
            TechnologyFloorMagicBulletBoss.SpecialTexturePath);
        profile.Swap("attack", 0.7f, "Attack");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("special", 3.0f, "Special");
        return profile;
    }
}

using Godot;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.TechnologyFloorLiberation;

public sealed partial class TechnologyFloorSolemnMourningBossCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(TechnologyFloorSolemnMourningBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -155f), new(0.576f, 0.576f), -170f, -315f, 170f, 12f, new(0f, -155f), new(-20f, -340f))
    {
        TalkPos = new Vector2(-20f, -280f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            TechnologyFloorSolemnMourningBoss.IdleTexturePath);
        profile.Frame(
            "attack",
            TechnologyFloorSolemnMourningBoss.AttackTexturePath);
        profile.Frame(
            "hit",
            TechnologyFloorSolemnMourningBoss.HitTexturePath);
        profile.Frame(
            "parry",
            TechnologyFloorSolemnMourningBoss.ParryTexturePath);
        profile.Frame(
                "ego_s1",
                TechnologyFloorSolemnMourningBoss.EgoS1TexturePath)
            .Nudge(-488f, 0f);
        profile.Frame(
                "ego_s2",
                TechnologyFloorSolemnMourningBoss.EgoS2TexturePath)
            .Nudge(-511f, 0f);
        profile.Swap("attack", 0.42f, "Attack", "AttackWhite");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("parry", 0.40f, "Guard", "Cast");
        profile.Swap("ego_s1", 1.0f, "EgoS1");
        profile.Swap("ego_s2", 1.0f, "EgoS2");
        return profile;
    }
}

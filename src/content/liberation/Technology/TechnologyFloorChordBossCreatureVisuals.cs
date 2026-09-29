using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed partial class TechnologyFloorChordBossCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(TechnologyFloorChordBoss))]
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

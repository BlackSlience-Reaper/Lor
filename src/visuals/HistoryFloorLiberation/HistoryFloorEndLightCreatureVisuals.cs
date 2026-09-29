using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorEndLightCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HistoryFloorEndLightBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -98f), new(0.58f, 0.58f), -155f, -290f, 155f, 30f, new(0f, -100f), new(0f, -320f))
    {
        TalkPos = new Vector2(0f, -240f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HistoryFloorEndLightBoss.IdleTexturePath);
        profile.Frame(
                "attack",
                HistoryFloorEndLightBoss.AttackTexturePath)
            .Nudge(26f, -92f)
            .Scale(0.58f);
        profile.Frame(
            "cast",
            HistoryFloorEndLightBoss.CastTexturePath);
        profile.Frame(
            "hit",
            HistoryFloorEndLightBoss.HitTexturePath);
        profile.Lunge("attack", 0.22f, 0.12f, 0.25f, "Attack");
        profile.Swap("cast", 0.45f, "Cast", "Parry");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

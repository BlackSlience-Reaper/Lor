using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.liberation.History;

public partial class HistoryFloorFlutteringBossCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HistoryFloorFlutteringBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -122f), new(0.4756f, 0.4756f), -145f, -260f, 145f, 30f, new(0f, -86f), new(0f, -292f))
    {
        TalkPos = new Vector2(0f, -215f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HistoryFloorFlutteringBoss.IdleTexturePath)
            .At(0f, -130f)
            .Scale(0.4756f)
            .IdleOnly();
        profile.Frame(
                "strike",
                HistoryFloorFlutteringBoss.AttackStrikeTexturePath)
            .Nudge(22f, -130f)
            .Scale(0.4756f);
        profile.Frame(
                "slash",
                HistoryFloorFlutteringBoss.AttackSlashTexturePath)
            .Nudge(22f, -130f)
            .Scale(0.4756f);
        profile.Frame(
                "hunger_2",
                HistoryFloorFlutteringBoss.HungerFrenzyS2TexturePath)
            .Nudge(22f, -130f)
            .Scale(0.4756f);
        profile.Frame(
                "hunger_3",
                HistoryFloorFlutteringBoss.HungerFrenzyS3TexturePath)
            .Nudge(22f, -130f)
            .Scale(0.4756f);
        profile.Frame(
                "hunger_4",
                HistoryFloorFlutteringBoss.HungerFrenzyS4TexturePath)
            .Nudge(22f, -130f)
            .Scale(0.4756f);
        profile.Frame(
            "hit",
            HistoryFloorFlutteringBoss.HitTexturePath);

        profile.Lunge(
            "strike",
            0.75f,
            0.75f,
            0.70f,
            "AttackStrike",
            "Attack");
        profile.Lunge(
            "slash",
            0.75f,
            0.75f,
            0.70f,
            "AttackSlash");
        profile.Lunge(
                ["hunger_2", "hunger_3", "hunger_4"],
                0.75f,
                0.75f,
                0.70f,
                "HungerFrenzy")
            .Cycle();
        profile.Swap(
            $"@idle:{SpriteVisualProfile.DefaultVariantKey}",
            0.70f,
            "Cast",
            "Parry");
        profile.Swap("hit", 0.62f, "Hit");
        return profile;
    }
}

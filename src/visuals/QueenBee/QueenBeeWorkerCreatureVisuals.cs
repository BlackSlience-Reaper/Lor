using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.monsters.QueenBee;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.QueenBee;

public sealed partial class QueenBeeWorkerCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(QueenBeeWorker))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -76f), new(0.54f, 0.54f), -82f, -184f, 82f, 8f, new(0f, -82f), new(0f, -224f))
    {
        TalkPos = new Vector2(0f, -178f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public void FaceQueen()
    {
        SetSpriteFlipH(true);
    }

    public void FacePlayers()
    {
        SetSpriteFlipH(false);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HistoryFloorWorkerBee.IdleTexturePath)
            .At(0f, -76f)
            .Scale(0.44f)
            .IdleOnly();
        AddAttack(
            profile,
            "attack",
            HistoryFloorWorkerBee.AttackTexturePath);
        AddAttack(
            profile,
            "attack_2",
            HistoryFloorWorkerBee.Attack2TexturePath);
        profile.Frame(
            "dodge",
            HistoryFloorWorkerBee.DodgeTexturePath);
        profile.Frame("hit", HistoryFloorWorkerBee.HitTexturePath);
        profile.Lunge("attack", 0.34f, 0.18f, 0.28f, "Attack");
        profile.Lunge("attack_2", 0.34f, 0.18f, 0.28f, "Attack2");
        profile.Swap("dodge", 0.3f, "Defend");
        profile.Swap("hit", 0.14f, "Hit");
        return profile;
    }

    private static void AddAttack(
        SpriteVisualProfile profile,
        string key,
        string path)
    {
        profile.Frame(key, path)
            .Nudge(18f, -76f)
            .Scale(0.50f);
    }
}

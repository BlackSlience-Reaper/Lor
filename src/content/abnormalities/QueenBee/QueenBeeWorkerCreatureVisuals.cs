using Godot;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

/// <summary>
/// 蜂后召来的工蜂：骨架与历史层工蜂共用（<see cref="HistoryFloorWorkerBeeCreatureVisuals.Spine"/>），
/// 加载失败时退回逐帧换图。转身朝向蜂后时骨架要跟着贴图重新对齐，才会一起镜像。
/// </summary>
public sealed partial class QueenBeeWorkerCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal override RuntimeSpineBody.Spec SpineSpec => HistoryFloorWorkerBeeCreatureVisuals.Spine;

    [MonsterVisual(typeof(QueenBeeWorker))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -76f), new(0.54f, 0.54f), -94f, -167f, 63f, 9f, new(0f, -82f), new(0f, -224f))
    {
        TalkPos = new Vector2(0f, -178f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public void FaceQueen()
    {
        SetSpriteFlipH(true);
        RealignSpineBody();
    }

    public void FacePlayers()
    {
        SetSpriteFlipH(false);
        RealignSpineBody();
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

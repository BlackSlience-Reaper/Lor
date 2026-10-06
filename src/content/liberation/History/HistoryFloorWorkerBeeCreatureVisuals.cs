using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.History;

/// <summary>
/// 工蜂的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按标注点对齐），
/// 加载失败时退回逐帧换图。异想体蜂后的工蜂同一套图，共用这副骨架。
/// </summary>
public partial class HistoryFloorWorkerBeeCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "history_floor",
        "worker_bee",
        "attack",
        new Dictionary<string, string>
        {
            ["Attack2"] = "attack2",
            ["Defend"] = "dodge",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(HistoryFloorWorkerBee))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -76f), new(0.44f, 0.44f), -82f, -184f, 82f, 8f, new(0f, -82f), new(0f, -224f))
    {
        TalkPos = new Vector2(0f, -178f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

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

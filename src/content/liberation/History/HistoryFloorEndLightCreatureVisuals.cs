using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.History;

/// <summary>
/// 终末之光的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。
/// 攻击的火环是从模组攻击图抠出的（原版火环比模组图大一倍），见 tools/spine_from_layers/build_boss_configs.py。
/// </summary>
public partial class HistoryFloorEndLightCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "history_floor",
        "end_light",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "cast",
            ["Parry"] = "cast",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(HistoryFloorEndLightBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -98f), new(0.58f, 0.58f), -131f, -290f, 155f, 30f, new(0f, -100f), new(0f, -320f))
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

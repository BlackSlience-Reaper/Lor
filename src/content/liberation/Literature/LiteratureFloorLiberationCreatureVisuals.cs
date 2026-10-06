using System;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Literature;

[MonsterVisual(typeof(LiteratureFloorLaetitiaBoss), ScenePath = LiteratureFloorLaetitiaBossCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorLaetitiaBossCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    // Spine 身体的触发与动画契约同名；多段招式的换姿势时刻照场景动画，见 tools/spine_from_layers/build_boss_configs.py
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "literature_floor_liberation",
        "laetitia",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "cast",
            ["SuperGift"] = "super_gift",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal const string ScenePath =
        LiteratureFloorAssets.LaetitiaBossScene;

    protected override string ResolveCurrentAnimationLibrary() =>
        LiteratureFloorLaetitiaAnimationContract.Library;
}

internal static class LiteratureFloorLaetitiaAnimationContract
{
    internal const string Library = "laetitia";
    internal const float AttackDurationSeconds = 0.55f;
    internal const float CastDurationSeconds = 0.55f;
    internal const float HitDurationSeconds = 0.18f;
    internal const float SuperGiftDurationSeconds = 3.30f;

    internal static IReadOnlyList<string> Animations { get; } =
        ["Idle", "Attack", "Cast", "Hit", "SuperGift"];

    internal static float DurationForTrigger(string triggerName) =>
        triggerName switch
        {
            "Attack" => AttackDurationSeconds,
            "Cast" => CastDurationSeconds,
            "Hit" => HitDurationSeconds,
            "SuperGift" => SuperGiftDurationSeconds,
            _ => throw new ArgumentOutOfRangeException(
                nameof(triggerName),
                triggerName,
                "Only non-idle Laetitia actions have a duration.")
        };
}

[MonsterVisual(typeof(LiteratureFloorSurpriseGiftBox), ScenePath = LiteratureFloorGiftBoxCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorGiftBoxCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        LiteratureFloorAssets.SurpriseGiftBoxScene;

    // 整块的 Spine 身体（按标注点对齐；攻击图在场景里缩小了，骨架里放大回与待机同大）；
    // 自爆照场景动画的换图时刻：施法 → 0.32 秒攻击 → 0.62 秒受击。加载失败时退回场景动画
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "literature_floor_liberation",
        "lf_gift_box",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "cast",
            ["SelfDestruct"] = "self_destruct",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    protected override string ResolveCurrentAnimationLibrary() =>
        LiteratureFloorGiftBoxAnimationContract.Library;
}

internal static class LiteratureFloorGiftBoxAnimationContract
{
    internal const string Library = "gift_box";
    internal const float AttackDurationSeconds = 0.48f;
    internal const float CastDurationSeconds = 0.40f;
    internal const float HitDurationSeconds = 0.16f;
    internal const float SelfDestructDurationSeconds = 0.90f;
    internal const float SelfDestructDamageDelaySeconds = 0.50f;

    internal static IReadOnlyList<string> Animations { get; } =
        ["Idle", "Attack", "Cast", "Hit", "SelfDestruct"];
}

[MonsterVisual(typeof(LiteratureFloorLittleWitchFriend), ScenePath = LiteratureFloorLittleWitchFriendCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorLittleWitchFriendCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        LiteratureFloorAssets.LittleWitchFriendScene;

    protected override string ResolveCurrentAnimationLibrary() =>
        LiteratureFloorLittleWitchFriendAnimationContract.Library;
}

internal static class LiteratureFloorLittleWitchFriendAnimationContract
{
    internal const string Library = "friend";
    internal const float AttackDurationSeconds = 0.46f;
    internal const float CastDurationSeconds = 0.42f;
    internal const float HitDurationSeconds = 0.16f;

    internal static IReadOnlyList<string> Animations { get; } =
        ["Idle", "Attack", "AttackAlt", "Cast", "Hit"];
}

[MonsterVisual(typeof(LiteratureFloorRedEyesBoss), ScenePath = LiteratureFloorRedEyesCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorRedEyesCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    // Spine 身体的触发与动画契约同名；多段招式的换姿势时刻照场景动画，见 tools/spine_from_layers/build_boss_configs.py
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "literature_floor_liberation",
        "red_eyes",
        "screech",
        new Dictionary<string, string>
        {
            ["FlickeringEyes"] = "flickering_eyes",
            ["Unknown"] = "unknown",
            ["Screech"] = "screech",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal const string ScenePath =
        LiteratureFloorAssets.RedEyesBossScene;

    protected override string ResolveCurrentAnimationLibrary() =>
        LiteratureFloorRedEyesAnimationContract.Library;
}

internal static class LiteratureFloorRedEyesAnimationContract
{
    internal const string Library = "red_eyes";
    internal const float FlickeringEyesDurationSeconds = 1.10f;
    internal const float UnknownDurationSeconds = 0.90f;
    internal const float HitDurationSeconds = 0.36f;
    internal const float ScreechDurationSeconds = 3.00f;
    internal const float ScreechFinalHitTimeSeconds = 2.10f;
    internal const float ScreechRecoverySeconds =
        ScreechDurationSeconds - ScreechFinalHitTimeSeconds;

    internal static IReadOnlyList<float> ScreechHitFrameTimesSeconds { get; }
        = [0.50f, 1.10f, ScreechFinalHitTimeSeconds];

    internal static IReadOnlyList<string> Animations { get; } =
        ["Idle", "FlickeringEyes", "Unknown", "Screech", "Hit"];
}

[MonsterVisual(typeof(LiteratureFloorEnhancedSmallSpider), ScenePath = LiteratureFloorEnhancedSmallSpiderCreatureVisuals.ScenePath)]
internal sealed partial class
    LiteratureFloorEnhancedSmallSpiderCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        LiteratureFloorAssets.EnhancedSmallSpiderScene;

    protected override string ResolveCurrentAnimationLibrary() =>
        LiteratureFloorEnhancedSmallSpiderAnimationContract.Library;
}

internal static class LiteratureFloorEnhancedSmallSpiderAnimationContract
{
    internal const string Library = "enhanced_small_spider";
    internal const float AttackDurationSeconds = 0.55f;
    internal const float CastDurationSeconds = 0.42f;
    internal const float HitDurationSeconds = 0.16f;

    internal static IReadOnlyList<string> Animations { get; } =
        ["Idle", "Attack", "Cast", "Hit"];
}

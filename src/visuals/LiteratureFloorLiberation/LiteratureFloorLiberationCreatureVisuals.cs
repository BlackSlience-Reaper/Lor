using System;
using LibraryOfRuina.monsters.LiteratureFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.LiteratureFloorLiberation;

[MonsterVisual(typeof(LiteratureFloorLaetitiaBoss), ScenePath = LiteratureFloorLaetitiaBossCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorLaetitiaBossCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/literature_floor_laetitia_boss.tscn";

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
        "res://scenes/creature_visuals/literature_floor_surprise_gift_box.tscn";

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
        "res://scenes/creature_visuals/literature_floor_little_witch_friend.tscn";

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
    internal const string ScenePath =
        "res://scenes/creature_visuals/literature_floor_red_eyes_boss.tscn";

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
        "res://scenes/creature_visuals/literature_floor_enhanced_small_spider.tscn";

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

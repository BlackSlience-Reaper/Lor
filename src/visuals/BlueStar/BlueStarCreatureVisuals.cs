using LibraryOfRuina.monsters.BlueStar;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.BlueStar;

[MonsterVisual(typeof(BlueStarAltar), ScenePath = BlueStarAltarCreatureVisuals.ScenePath)]
internal sealed partial class BlueStarAltarCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/blue_star_altar.tscn";
    internal const string NormalAnimationsPath =
        "res://scenes/creature_visuals/blue_star_altar_normal_animations.tres";
    internal const string NovaAnimationsPath =
        "res://scenes/creature_visuals/blue_star_altar_nova_animations.tres";

    internal static IReadOnlyList<string> AssetPaths { get; } =
    [
        ScenePath,
        NormalAnimationsPath,
        NovaAnimationsPath,
        BlueStarAltar.IdleTexturePath,
        BlueStarAltar.NovaTexturePath
    ];

    private bool _novaIdle;

    protected override string ResolveCurrentAnimationLibrary() =>
        _novaIdle
            ? BlueStarAltarAnimationContract.NovaLibrary
            : BlueStarAltarAnimationContract.NormalLibrary;

    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName == "Cast"
            ? BlueStarAltarAnimationContract.NovaAnimation
            : triggerName;

    internal void UpdateIdleForMove(string moveId)
    {
        _novaIdle = moveId == BlueStarAltar.NovaVoiceMoveId;
        TryPlayTrigger(BlueStarAltarAnimationContract.IdleAnimation);
    }
}

internal static class BlueStarAltarAnimationContract
{
    internal const string NormalLibrary = "normal";
    internal const string NovaLibrary = "nova";
    internal const string IdleAnimation = "Idle";
    internal const string NovaAnimation = "Nova";
    internal const float IdleDurationSeconds = 2.0f;
    internal const float NovaDurationSeconds = 1.50f;
}

[MonsterVisual(typeof(BlueStarFollower), ScenePath = BlueStarFollowerCreatureVisuals.ScenePath)]
internal sealed partial class BlueStarFollowerCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/blue_star_follower.tscn";
    internal const string AnimationsPath =
        "res://scenes/creature_visuals/blue_star_follower_animations.tres";

    internal static IReadOnlyList<string> AssetPaths { get; } =
    [
        ScenePath,
        AnimationsPath,
        BlueStarFollower.IdleTexturePath,
        BlueStarFollower.BasicAttackTexturePath,
        BlueStarFollower.VoiceAttackOneTexturePath,
        BlueStarFollower.VoiceAttackTwoTexturePath,
        BlueStarFollower.HitTexturePath,
        BlueStarFollower.GuardTexturePath
    ];

    protected override string ResolveCurrentAnimationLibrary() =>
        BlueStarFollowerAnimationContract.Library;

    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName switch
        {
            "Attack" => BlueStarFollowerAnimationContract.BasicAttackAnimation,
            "Cast" or "Block" =>
                BlueStarFollowerAnimationContract.GuardAnimation,
            _ => triggerName
        };
}

internal static class BlueStarFollowerAnimationContract
{
    internal const string Library = "follower";
    internal const string IdleAnimation = "Idle";
    internal const string BasicAttackAnimation = "BasicAttack";
    internal const string VoiceAttackAnimation = "VoiceAttack";
    internal const string GuardAnimation = "Guard";
    internal const string HitAnimation = "Hit";
    internal const string SelfDestructAnimation = "SelfDestruct";
    internal const float IdleDurationSeconds = 2.0f;
    internal const float BasicAttackDurationSeconds = 0.80f;
    internal const float VoiceAttackDurationSeconds = 0.80f;
    internal const float GuardDurationSeconds = 0.76f;
    internal const float HitDurationSeconds = 0.56f;
    internal const float SelfDestructDurationSeconds = 1.20f;
}

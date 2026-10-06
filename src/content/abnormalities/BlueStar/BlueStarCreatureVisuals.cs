using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.BlueStar;

[MonsterVisual(typeof(BlueStarAltar), ScenePath = BlueStarAltarCreatureVisuals.ScenePath)]
internal sealed partial class BlueStarAltarCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        BlueStarAssets.BlueStarAltarScene;
    internal const string NormalAnimationsPath =
        BlueStarAssets.AltarNormalAnimationsResource;
    internal const string NovaAnimationsPath =
        BlueStarAssets.AltarNovaAnimationsResource;

    internal static IReadOnlyList<string> AssetPaths { get; } =
    [
        ScenePath,
        NormalAnimationsPath,
        NovaAnimationsPath,
        BlueStarAltar.IdleTexturePath,
        BlueStarAltar.NovaTexturePath
    ];

    private bool _novaIdle;

    // 普通、新星两个动画库各一副整块的 Spine 身体（石台待机几乎不动），加载失败时退回场景动画
    private static Dictionary<string, string> SpineTriggers() => new()
    {
        [BlueStarAltarAnimationContract.NovaAnimation] = "nova",
    };

    internal static readonly RuntimeSpineBody.Spec NormalSpine = LayeredBossSpine.Create(
        "blue_star_altar", "blue_star_altar", "nova", SpineTriggers());

    internal static readonly RuntimeSpineBody.Spec NovaSpine = LayeredBossSpine.Create(
        "blue_star_altar", "blue_star_altar_nova", "nova", SpineTriggers());

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [NormalSpine, NovaSpine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) =>
        library == BlueStarAltarAnimationContract.NovaLibrary ? NovaSpine : NormalSpine;

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
    // 整块的 Spine 身体（照场景摆放，各姿势按标注点对齐），加载失败时退回场景动画。触发名先经 NormalizeTriggerName；
    // 听见声音照场景 0.4 秒从蓄力图换到释放图；自爆只用释放图（与场景自爆同图）
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "blue_star_follower",
        "blue_star_follower",
        "basic_attack",
        new Dictionary<string, string>
        {
            [BlueStarFollowerAnimationContract.BasicAttackAnimation] = "basic_attack",
            [BlueStarFollowerAnimationContract.VoiceAttackAnimation] = "voice_attack",
            [BlueStarFollowerAnimationContract.SelfDestructAnimation] = "self_destruct",
            [BlueStarFollowerAnimationContract.GuardAnimation] = "guard",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal const string ScenePath =
        BlueStarAssets.BlueStarFollowerScene;
    internal const string AnimationsPath =
        BlueStarAssets.FollowerAnimationsResource;

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

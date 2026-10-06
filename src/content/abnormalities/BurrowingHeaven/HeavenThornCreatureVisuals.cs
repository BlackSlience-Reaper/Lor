using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.BurrowingHeaven;

/// <summary>
/// 天堂之刺的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按根部标注点对齐、动作不转身体），
/// 加载失败时退回下面的逐帧换图。醒着、睡着两张待机各一副骨架。
/// </summary>
public sealed partial class HeavenThornCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    private static Dictionary<string, string> SpineTriggers() => new()
    {
        ["Guard"] = "guard",
    };

    internal static readonly RuntimeSpineBody.Spec AwakeSpine = LayeredBossSpine.Create(
        "heaven_thorn", "heaven_thorn_awake", "attack", SpineTriggers());

    internal static readonly RuntimeSpineBody.Spec SleepSpine = LayeredBossSpine.Create(
        "heaven_thorn", "heaven_thorn_sleep", "attack", SpineTriggers());

    internal override RuntimeSpineBody.Spec SpineSpec => AwakeSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [AwakeSpine, SleepSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) =>
        variantKey == SleepVariant ? SleepSpine : AwakeSpine;

    [MonsterVisual(typeof(HeavenThorn))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.48f, 0.48f), -95f, -291f, 104f, -4f, new(0f, -118f), new(0f, -315f))
    {
        StateDisplayLiftY = 14f,
    };

    // 按荆棘当前是否苏醒选初始立绘与 _Ready 时的变体。
    [MonsterVisualFactory]
    internal static NCreatureVisuals CreateForMonster(MonsterModel monster)
    {
        string id = monster.Id.Entry;
        return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HeavenThornCreatureVisuals>(
            id,
            monster is HeavenThorn { IsAwake: false }
                ? HeavenThorn.SleepTexturePath
                : HeavenThorn.AwakeTexturePath,
            visuals => visuals.StartsAwake = monster is not HeavenThorn { IsAwake: false });
    }

    private const string AwakeVariant = "awake";
    private const string SleepVariant = "sleep";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private bool _ready;

    public bool StartsAwake { get; set; } = true;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        _ready = true;
        SetInitialState();
    }

    private void SetInitialState()
    {
        if (_ready)
        {
            SetSpriteVisualVariant(
                StartsAwake ? AwakeVariant : SleepVariant);
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(AwakeVariant, HeavenThorn.AwakeTexturePath);
        profile.Variant(SleepVariant, HeavenThorn.SleepTexturePath);
        profile.InitialVariant(AwakeVariant);
        profile.Frame("attack", HeavenThorn.AttackTexturePath);
        profile.Frame("hit", HeavenThorn.HitTexturePath);
        profile.Frame("guard", HeavenThorn.GuardTexturePath);
        profile.Swap("@idle:awake", 0.16f, "Awake")
            .SwitchToVariant(AwakeVariant);
        profile.Swap("@idle:sleep", 0.16f, "Sleep")
            .SwitchToVariant(SleepVariant);
        profile.Swap("attack", 0.36f, "Attack");
        profile.Swap("guard", 0.36f, "Guard");
        profile.Swap("hit", 0.28f, "Hit");
        return profile;
    }
}

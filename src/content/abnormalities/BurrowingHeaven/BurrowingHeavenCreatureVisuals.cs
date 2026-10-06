using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.BurrowingHeaven;

/// <summary>
/// 渗透天堂的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按根部标注点对齐、动作不转身体），
/// 加载失败时退回下面的逐帧换图。醒着、睡着两张待机各一副骨架。
/// </summary>
public sealed partial class BurrowingHeavenCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    private static Dictionary<string, string> SpineTriggers() => new()
    {
        ["Special"] = "special",
        ["Guard"] = "guard",
    };

    internal static readonly RuntimeSpineBody.Spec AwakeSpine = LayeredBossSpine.Create(
        "burrowing_heaven", "burrowing_heaven_awake", "attack", SpineTriggers());

    internal static readonly RuntimeSpineBody.Spec SleepSpine = LayeredBossSpine.Create(
        "burrowing_heaven", "burrowing_heaven_sleep", "attack", SpineTriggers());

    internal override RuntimeSpineBody.Spec SpineSpec => AwakeSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [AwakeSpine, SleepSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) =>
        variantKey == SleepVariant ? SleepSpine : AwakeSpine;

    [MonsterVisual(typeof(BurrowingHeaven))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -18f), new(0.58f, 0.58f), -196f, -390f, 207f, 12f, new(0f, -170f), new(0f, -430f))
    {
        TalkPos = new Vector2(0f, -330f),
        StateDisplayLiftY = 34f,
    };

    // 按怪物当前是否苏醒选初始立绘与 _Ready 时的变体。
    [MonsterVisualFactory]
    internal static NCreatureVisuals CreateForMonster(MonsterModel monster)
    {
        string id = monster.Id.Entry;
        return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<BurrowingHeavenCreatureVisuals>(
            id,
            monster is BurrowingHeaven { IsAwake: false }
                ? BurrowingHeaven.SleepTexturePath
                : BurrowingHeaven.AwakeTexturePath,
            visuals => visuals.StartsAwake = monster is not BurrowingHeaven { IsAwake: false });
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
        profile.Variant(
            AwakeVariant,
            BurrowingHeaven.AwakeTexturePath);
        profile.Variant(
            SleepVariant,
            BurrowingHeaven.SleepTexturePath);
        profile.InitialVariant(AwakeVariant);
        profile.Frame(
            "attack",
            BurrowingHeaven.AttackTexturePath);
        profile.Frame(
            "hit",
            BurrowingHeaven.HitTexturePath);
        profile.Frame(
            "special",
            BurrowingHeaven.SpecialTexturePath);
        profile.Frame(
            "guard",
            BurrowingHeaven.GuardTexturePath);
        profile.Swap("@idle:awake", 0.18f, "Awake")
            .SwitchToVariant(AwakeVariant);
        profile.Swap("@idle:sleep", 0.18f, "Sleep")
            .SwitchToVariant(SleepVariant);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("special", 0.66f, "Special");
        profile.Swap("guard", 0.34f, "Guard");
        profile.Swap("hit", 0.32f, "Hit");
        return profile;
    }
}

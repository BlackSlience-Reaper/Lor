using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.BurrowingHeaven;

public sealed partial class HeavenThornCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HeavenThorn))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.48f, 0.48f), -98f, -275f, 98f, 12f, new(0f, -118f), new(0f, -315f))
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

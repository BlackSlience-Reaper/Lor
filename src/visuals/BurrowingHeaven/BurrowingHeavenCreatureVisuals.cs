using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.BurrowingHeaven;

public sealed partial class BurrowingHeavenCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.BurrowingHeaven.BurrowingHeaven))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -18f), new(0.58f, 0.58f), -165f, -390f, 165f, 12f, new(0f, -170f), new(0f, -430f))
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
            monster is monsters.BurrowingHeaven.BurrowingHeaven { IsAwake: false }
                ? monsters.BurrowingHeaven.BurrowingHeaven.SleepTexturePath
                : monsters.BurrowingHeaven.BurrowingHeaven.AwakeTexturePath,
            visuals => visuals.StartsAwake = monster is not monsters.BurrowingHeaven.BurrowingHeaven { IsAwake: false });
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
            monsters.BurrowingHeaven.BurrowingHeaven.AwakeTexturePath);
        profile.Variant(
            SleepVariant,
            monsters.BurrowingHeaven.BurrowingHeaven.SleepTexturePath);
        profile.InitialVariant(AwakeVariant);
        profile.Frame(
            "attack",
            monsters.BurrowingHeaven.BurrowingHeaven.AttackTexturePath);
        profile.Frame(
            "hit",
            monsters.BurrowingHeaven.BurrowingHeaven.HitTexturePath);
        profile.Frame(
            "special",
            monsters.BurrowingHeaven.BurrowingHeaven.SpecialTexturePath);
        profile.Frame(
            "guard",
            monsters.BurrowingHeaven.BurrowingHeaven.GuardTexturePath);
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

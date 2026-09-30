using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.Ozma;

public sealed partial class OzmaCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(Ozma))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.48f, 0.48f), -196f, -382f, 196f, 16f, new(0f, -184f), new(20f, -414f))
    {
        StateDisplayLiftY = 30f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            Ozma.IdleTexturePath);
        profile.Frame("attack", Ozma.AttackTexturePath);
        profile.Frame("hit", Ozma.HitTexturePath);
        profile.Frame("guard", Ozma.GuardTexturePath);
        profile.Frame("pain", Ozma.PainTexturePath);
        profile.Frame("sorrow", Ozma.SorrowTexturePath);
        profile.Swap("attack", 0.48f, "Attack");
        profile.Swap("guard", 0.48f, "Guard", "Block", "Cast");
        profile.Swap(
            "pain",
            0.92f,
            "LifePowder",
            "Pain",
            "SpecialAttack");
        profile.Swap("sorrow", 1.05f, "Sorrow");
        profile.Swap("hit", 0.3f, "Hit");
        return profile;
    }
}

public sealed partial class OzmaJackCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(OzmaJack))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.48f, 0.48f), -98f, -228f, 98f, 12f, new(0f, -108f), new(0f, -264f))
    {
        StateDisplayLiftY = 12f,
    };

    // 按南瓜头当前是否苏醒选初始立绘与 _Ready 时的变体。
    [MonsterVisualFactory]
    internal static NCreatureVisuals CreateForMonster(MonsterModel monster)
    {
        string id = monster.Id.Entry;
        return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<OzmaJackCreatureVisuals>(
            id,
            monster is OzmaJack { IsAwake: true }
                ? OzmaJack.AwakeTexturePath
                : OzmaJack.DormantTexturePath,
            visuals => visuals.StartsAwake = monster is OzmaJack { IsAwake: true });
    }

    private const string DormantVariant = "dormant";
    private const string AwakeVariant = "awake";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private bool _ready;

    public bool StartsAwake { get; set; }

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        _ready = true;
        SetAwake(StartsAwake);
    }

    public void SetAwake(bool awake)
    {
        if (_ready)
        {
            SetSpriteVisualVariant(
                awake ? AwakeVariant : DormantVariant);
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            DormantVariant,
            OzmaJack.DormantTexturePath);
        profile.Variant(
            AwakeVariant,
            OzmaJack.AwakeTexturePath);
        profile.InitialVariant(DormantVariant);
        profile.Frame("hit", OzmaJack.HitTexturePath);
        profile.Swap("@idle:awake", 0.5f, "Awake")
            .SwitchToVariant(AwakeVariant);
        profile.Swap("@idle:dormant", 0.01f, "Idle")
            .ForVariant(DormantVariant);
        profile.Swap("@idle:awake", 0.01f, "Idle")
            .ForVariant(AwakeVariant);
        profile.Swap("hit", 0.28f, "Hit");
        return profile;
    }
}

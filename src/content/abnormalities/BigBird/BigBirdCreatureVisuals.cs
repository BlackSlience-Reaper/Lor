using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.BigBird;

/// <summary>
/// 大鸟的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图按姿势做成整块，tools/spine_from_layers/sprite_layers.py），
/// 加载失败时退回下面的逐帧换图。普通、沉睡、救赎三个形态各一副骨架；普通待机的提灯单独一块，挂在嘴下像钟摆一样摆。
/// 各姿势按爪尖对齐、动作只做水平位移，爪子始终在同一高度。
/// </summary>
public sealed partial class BigBirdCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    private static Dictionary<string, string> SpineTriggers() => new()
    {
        ["Guard"] = "guard",
        ["Block"] = "guard",
        ["Charm"] = "charm",
        ["Cast"] = "charm",
        ["Rescue"] = "rescue",
    };

    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "big_bird", "big_bird", "rescue", SpineTriggers());

    internal static readonly RuntimeSpineBody.Spec SleepSpine = LayeredBossSpine.Create(
        "big_bird", "big_bird_sleep", "rescue", SpineTriggers());

    internal static readonly RuntimeSpineBody.Spec RescueSpine = LayeredBossSpine.Create(
        "big_bird", "big_bird_rescue", "rescue", SpineTriggers());

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [Spine, SleepSpine, RescueSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) => variantKey switch
    {
        SleepVariant => SleepSpine,
        RescueVariant => RescueSpine,
        _ => Spine,
    };

    [MonsterVisual(typeof(BigBird))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -12f), new(0.60f, 0.60f), -151f, -326f, 190f, -3f, new(0f, -204f), new(44f, -372f))
    {
        TalkPos = new Vector2(0f, -356f),
        StateDisplayLiftY = 36f,
    };

    private const string NormalVariant = "normal";
    private const string SleepVariant = "sleep";
    private const string RescueVariant = "rescue";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public void UpdateIdleForIntent(string moveId)
    {
        SetSpriteVisualVariant(
            moveId == BigBird.RescueMoveId
                ? RescueVariant
                : NormalVariant);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            NormalVariant,
            BigBird.IdleTexturePath);
        profile.Variant(
            SleepVariant,
            BigBird.SleepTexturePath);
        profile.Variant(
            RescueVariant,
            BigBird.RescueOpenTexturePath);
        profile.InitialVariant(NormalVariant);

        profile.Frame(
            "hit",
            BigBird.HitTexturePath);
        profile.Frame(
            "guard",
            BigBird.GuardTexturePath);
        profile.Frame(
            "charm",
            BigBird.CharmTexturePath);
        profile.Frame(
            "rescue_close",
            BigBird.RescueCloseTexturePath);

        profile.Swap("@idle:normal", 0.01f, "Idle")
            .SwitchToVariant(NormalVariant);
        profile.Swap("guard", 0.36f, "Guard", "Block");
        profile.Swap("charm", 0.72f, "Charm", "Cast");
        profile.Swap("rescue_close", 0.82f, "Rescue", "Attack");
        profile.Swap("@idle:sleep", 0.5f, "Sleep")
            .SwitchToVariant(SleepVariant);
        profile.Swap("hit", 0.32f, "Hit");
        return profile;
    }
}

public sealed partial class EyeballBirdCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(EyeballBird))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 13f), new(0.56f, 0.56f), -74f, -209f, 118f, 11f, new(0f, -96f), new(0f, -238f))
    {
        TalkPos = new Vector2(0f, -202f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            EyeballBird.IdleTexturePath);
        profile.Frame("hit", EyeballBird.HitTexturePath);
        profile.Frame("attack", EyeballBird.AttackTexturePath);
        profile.Frame("evade", EyeballBird.EvadeTexturePath);
        profile.Swap("attack", 0.34f, "Attack", "Thrust");
        profile.Swap("evade", 0.34f, "Evade", "Dodge");
        profile.Swap("hit", 0.26f, "Hit");
        return profile;
    }
}

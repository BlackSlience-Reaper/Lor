using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.PriceOfSilence;

/// <summary>
/// 时间的痕迹的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，只平移、转动，各姿势按标注点对齐），
/// 加载失败时退回下面的逐帧换图。悬空的影子，待机上下浮。
/// </summary>
public sealed partial class TimeTraceCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "time_trace",
        "time_trace",
        "slash",
        new Dictionary<string, string>
        {
            ["AttackBlunt"] = "blunt",
            ["AttackThrust"] = "thrust",
            ["AttackSlash"] = "slash",
            ["Guard"] = "guard",
            ["Cast"] = "guard",
            ["StunTrigger"] = "stun",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(TimeTrace))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(10f, 20f), new(1.24f, 1.24f), -195f, -346f, 158f, 12f, new(0f, -174f), new(-95f, -360f))
    {
        StateDisplayLiftY = 5f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            TimeTrace.IdleTexturePath);
        profile.Frame("attack_blunt", TimeTrace.AttackBluntTexturePath);
        profile.Frame("attack_thrust", TimeTrace.AttackThrustTexturePath);
        profile.Frame("attack_slash", TimeTrace.AttackSlashTexturePath);
        profile.Frame("hit", TimeTrace.HitTexturePath);
        profile.Frame("guard", TimeTrace.GuardTexturePath);
        profile.Swap("attack_blunt", 0.38f, "AttackBlunt");
        profile.Swap("attack_thrust", 0.38f, "AttackThrust");
        profile.Swap("attack_slash", 0.38f, "AttackSlash", "Attack");
        profile.Swap("hit", 0.26f, "Hit");
        profile.Swap("guard", 0.34f, "Guard", "Cast");
        profile.Swap("hit", 0.34f, "StunTrigger", "Dead");
        profile.Swap("@idle:default", 0.22f, "WakeUpTrigger");
        return profile;
    }
}

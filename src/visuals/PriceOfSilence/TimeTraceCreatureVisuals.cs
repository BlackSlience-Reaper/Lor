using LibraryOfRuina.monsters.PriceOfSilence;

namespace LibraryOfRuina.visuals.PriceOfSilence;

public sealed partial class TimeTraceCreatureVisuals
    : SpriteAttackCreatureVisuals
{
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

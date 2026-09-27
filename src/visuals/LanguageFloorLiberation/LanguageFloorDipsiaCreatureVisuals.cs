using LibraryOfRuina.monsters.LanguageFloorLiberation;

namespace LibraryOfRuina.visuals.LanguageFloorLiberation;

public sealed partial class LanguageFloorDipsiaCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                LanguageFloorDipsia.IdleTexturePath)
            .Scale(0.72f);

        profile.Frame("fire", LanguageFloorDipsia.FireTexturePath)
            .Scale(0.72f);
        profile.Frame("strike", LanguageFloorDipsia.StrikeTexturePath)
            .Scale(0.72f)
            .Nudge(-100f, 30f);
        profile.Frame("slash", LanguageFloorDipsia.SlashTexturePath)
            .Scale(0.84f)
            .Nudge(65f, 250f);
        profile.Frame(
                "group_break",
                LanguageFloorDipsia.GroupBreakTexturePath)
            .Scale(0.60f);
        profile.Frame(
                "group_attack",
                LanguageFloorDipsia.GroupAttackTexturePath)
            .Scale(0.60f)
            .Nudge(-185f, 105f);
        profile.Frame("evade", LanguageFloorDipsia.EvadeTexturePath)
            .Scale(0.58f);
        profile.Frame("hit", LanguageFloorDipsia.HitTexturePath)
            .Scale(0.50f);

        profile.Swap("fire", 0.45f, "AttackFire");
        profile.Swap("strike", 0.45f, "AttackStrike");
        profile.Swap("slash", 0.45f, "AttackSlash");
        profile.Swap(
            "group_break",
            LanguageFloorDipsia.GroupBreakSegmentSeconds,
            "GroupBreak");
        profile.Swap("group_attack", 0.55f, "GroupAttack");
        profile.Swap("evade", 0.45f, "Cast");
        profile.Swap("hit", 0.18f, "Hit");
        return profile;
    }
}

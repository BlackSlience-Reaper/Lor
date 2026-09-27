using LibraryOfRuina.monsters.HistoryFloorLiberation;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorEndLightCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HistoryFloorEndLightBoss.IdleTexturePath);
        profile.Frame(
                "attack",
                HistoryFloorEndLightBoss.AttackTexturePath)
            .Nudge(26f, -92f)
            .Scale(0.58f);
        profile.Frame(
            "cast",
            HistoryFloorEndLightBoss.CastTexturePath);
        profile.Frame(
            "hit",
            HistoryFloorEndLightBoss.HitTexturePath);
        profile.Lunge("attack", 0.22f, 0.12f, 0.25f, "Attack");
        profile.Swap("cast", 0.45f, "Cast", "Parry");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

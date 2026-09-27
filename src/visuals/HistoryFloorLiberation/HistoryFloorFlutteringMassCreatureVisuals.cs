using LibraryOfRuina.monsters.HistoryFloorLiberation;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorFlutteringMassCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HistoryFloorFlutteringMass.IdleTexturePath)
            .At(0f, -92f)
            .Scale(0.3116f)
            .IdleOnly();
        profile.Frame(
                "attack",
                HistoryFloorFlutteringMass.AttackTexturePath)
            .Nudge(20f, -92f)
            .Scale(0.3116f);
        profile.Frame(
            "hit",
            HistoryFloorFlutteringMass.HitTexturePath);
        profile.Lunge("attack", 0.64f, 0.6f, 0.68f, "Attack");
        profile.Swap("hit", 0.52f, "Hit");
        return profile;
    }
}

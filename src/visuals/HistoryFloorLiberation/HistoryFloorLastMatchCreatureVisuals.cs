using LibraryOfRuina.monsters.HistoryFloorLiberation;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorLastMatchCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HistoryFloorLastMatch.IdleTexturePath);
        profile.Frame(
                "attack",
                HistoryFloorLastMatch.AttackTexturePath)
            .Nudge(14f, 0f)
            .Scale(0.40f);
        profile.Frame(
            "cast",
            HistoryFloorLastMatch.CastTexturePath);
        profile.Frame(
            "hit",
            HistoryFloorLastMatch.HitTexturePath);
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Swap("cast", 0.45f, "Cast", "Parry");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

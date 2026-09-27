using LibraryOfRuina.monsters.HistoryFloorLiberation;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorEmeraldBoughCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HistoryFloorEmeraldBoughBoss.IdleTexturePath)
            .At(0f, -128f)
            .Scale(0.5f)
            .IdleOnly();
        profile.Frame(
                "attack",
                HistoryFloorEmeraldBoughBoss.AttackTexturePath)
            .Nudge(26f, -128f)
            .Scale(0.56f);
        profile.Frame(
            "hit",
            HistoryFloorEmeraldBoughBoss.HitTexturePath);
        profile.Frame(
            "parry",
            HistoryFloorEmeraldBoughBoss.ParryTexturePath);
        profile.Frame(
            "ego",
            HistoryFloorEmeraldBoughBoss.EgoTexturePath);
        profile.Lunge("attack", 0.22f, 0.12f, 0.25f, "Attack");
        profile.Swap("parry", 0.55f, "Cast", "Parry");
        profile.Swap("ego", 0.55f, "Ego");
        profile.Swap("hit", 0.14f, "Hit");
        return profile;
    }
}

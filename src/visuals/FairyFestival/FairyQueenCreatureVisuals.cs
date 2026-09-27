using LibraryOfRuina.monsters.FairyFestival;

namespace LibraryOfRuina.visuals.FairyFestival;

public partial class FairyQueenCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            FairyQueen.IdleTexturePath);
        profile.Frame("idle_alt", FairyQueen.IdleAltTexturePath);
        profile.Frame("attack", FairyQueen.AttackTexturePath)
            .Nudge(34f, -112f)
            .Scale(0.56f);
        profile.Frame("cast", FairyQueen.CastTexturePath);
        profile.Frame("hit", FairyQueen.HitTexturePath);
        profile.Lunge("attack", 0.25f, 0.25f, 0.28f, "Attack");
        profile.Swap("cast", 0.7f, "Cast", "Parry");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

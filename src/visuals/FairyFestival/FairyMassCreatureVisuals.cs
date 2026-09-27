using LibraryOfRuina.monsters.FairyFestival;

namespace LibraryOfRuina.visuals.FairyFestival;

public partial class FairyMassCreatureVisuals : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            FairyMass.IdleTexturePath);
        profile.Frame("idle_alt", FairyMass.IdleAltTexturePath);
        profile.Frame("attack", FairyMass.AttackTexturePath)
            .Nudge(24f, -104f)
            .Scale(0.50f);
        profile.Frame("hit", FairyMass.HitTexturePath);
        profile.Lunge("attack", 0.24f, 0.2f, 0.28f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

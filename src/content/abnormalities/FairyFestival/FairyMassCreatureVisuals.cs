using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

public partial class FairyMassCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(FairyMass))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -108f), new(0.46f, 0.46f), -116f, -250f, 110f, 8f, new(0f, -108f), new(0f, -284f))
    {
        TalkPos = new Vector2(0f, -220f),
    };

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

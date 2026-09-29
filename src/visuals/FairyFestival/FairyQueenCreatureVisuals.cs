using Godot;
using LibraryOfRuina.monsters.FairyFestival;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.FairyFestival;

public partial class FairyQueenCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(FairyQueen))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -114f), new(0.58f, 0.58f), -152f, -284f, 152f, 8f, new(0f, -118f), new(0f, -318f))
    {
        TalkPos = new Vector2(0f, -258f),
    };

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

using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.PriceOfSilence;

public sealed partial class PriceOfSilenceCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.PriceOfSilence.PriceOfSilence))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -10f), new(0.56f, 0.56f), -168f, -360f, 168f, 16f, new(0f, -166f), new(70f, -470f))
    {
        TalkPos = new Vector2(0f, -300f),
        StateDisplayLiftY = 26f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            monsters.PriceOfSilence.PriceOfSilence.IdleTexturePath);
        profile.Frame(
                "special",
                monsters.PriceOfSilence.PriceOfSilence.SpecialTexturePath)
            .Nudge(-18f, 10f);
        profile.Swap("special", 0.62f, "Special", "Attack", "Cast");
        profile.Swap("@idle:default", 0.28f, "Hit");
        return profile;
    }
}

using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.BigBadWolf;

public sealed partial class BigBadWolfCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(BigBadWolf))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -178f), new(0.72f, 0.72f), -150f, -370f, 150f, 10f, new(0f, -178f), new(0f, -405f))
    {
        TalkPos = new Vector2(0f, -315f),
        StolenCardPos = new Vector2(0f, -218f),
        StolenCardScale = new Vector2(0.48f, 0.48f),
    };

    private const string NormalVariant = "normal";
    private const string SwallowedVariant = "swallowed";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            NormalVariant,
            BigBadWolf.IdleTexturePath);
        profile.Variant(
            SwallowedVariant,
            BigBadWolf.SwallowTexturePath);
        profile.InitialVariant(NormalVariant);
        profile.Centered();

        profile.Frame(
            "hit",
            BigBadWolf.HitTexturePath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "strike",
            BigBadWolf.StrikeTexturePath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "slash",
            BigBadWolf.SlashTexturePath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "swallowed_strike",
            BigBadWolf.SwallowedStrikeTexturePath)
            .ForVariant(SwallowedVariant);
        profile.Frame(
            "swallowed_slash",
            BigBadWolf.SwallowedSlashTexturePath)
            .ForVariant(SwallowedVariant);
        profile.Frame(
            "swallowed_hit",
            BigBadWolf.SwallowedHitTexturePath)
            .ForVariant(SwallowedVariant);

        profile.Swap("strike", 0.42f, "Strike", "Attack")
            .ForVariant(NormalVariant);
        profile.Swap("swallowed_strike", 0.42f, "Strike", "Attack")
            .ForVariant(SwallowedVariant);
        profile.Swap("slash", 0.42f, "Slash")
            .ForVariant(NormalVariant);
        profile.Swap("swallowed_slash", 0.42f, "Slash")
            .ForVariant(SwallowedVariant);
        profile.Swap("hit", 0.38f, "Hit")
            .ForVariant(NormalVariant);
        profile.Swap("swallowed_hit", 0.38f, "Hit")
            .ForVariant(SwallowedVariant);
        profile.Swap("@idle:swallowed", 0.48f, "Swallow");
        profile.Swap("@idle:swallowed", 0.12f, "Swallowed")
            .SwitchToVariant(SwallowedVariant);
        profile.Swap("@idle:swallowed", 0.44f, "Spit", "Eat", "ClearSwallowed")
            .SwitchToVariant(NormalVariant);
        return profile;
    }
}

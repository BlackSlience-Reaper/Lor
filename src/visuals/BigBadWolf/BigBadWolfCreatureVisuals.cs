using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.BigBadWolf;

public sealed partial class BigBadWolfCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.BigBadWolf.BigBadWolf))]
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
            monsters.BigBadWolf.BigBadWolf.IdleTexturePath);
        profile.Variant(
            SwallowedVariant,
            monsters.BigBadWolf.BigBadWolf.SwallowTexturePath);
        profile.InitialVariant(NormalVariant);
        profile.Centered();

        profile.Frame(
            "hit",
            monsters.BigBadWolf.BigBadWolf.HitTexturePath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "strike",
            monsters.BigBadWolf.BigBadWolf.StrikeTexturePath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "slash",
            monsters.BigBadWolf.BigBadWolf.SlashTexturePath)
            .ForVariant(NormalVariant);
        profile.Frame(
            "swallowed_strike",
            monsters.BigBadWolf.BigBadWolf.SwallowedStrikeTexturePath)
            .ForVariant(SwallowedVariant);
        profile.Frame(
            "swallowed_slash",
            monsters.BigBadWolf.BigBadWolf.SwallowedSlashTexturePath)
            .ForVariant(SwallowedVariant);
        profile.Frame(
            "swallowed_hit",
            monsters.BigBadWolf.BigBadWolf.SwallowedHitTexturePath)
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

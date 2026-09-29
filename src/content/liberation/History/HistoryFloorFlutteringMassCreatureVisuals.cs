using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.History;

public partial class HistoryFloorFlutteringMassCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HistoryFloorFlutteringMass))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -92f), new(0.3116f, 0.3116f), -94f, -205f, 94f, 8f, new(0f, -96f), new(0f, -250f))
    {
        TalkPos = new Vector2(0f, -198f),
    };

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

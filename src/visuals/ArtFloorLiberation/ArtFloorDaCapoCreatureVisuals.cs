using Godot;
using LibraryOfRuina.monsters.ArtFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class ArtFloorDaCapoCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ArtFloorDaCapoBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -188f), new(0.58f, 0.58f), -215f, -472f, 215f, 36f, new(0f, -205f), new(0f, -452f))
    {
        TalkPos = new Vector2(0f, -392f),
    };

    // 终幕版本只多了状态条上移。
    [MonsterVisual(typeof(ArtFloorFinalDaCapoBoss))]
    internal static readonly CreatureVisualLayout FinalLayout = new(
        new(0f, -188f), new(0.58f, 0.58f), -215f, -472f, 215f, 36f, new(0f, -205f), new(0f, -452f))
    {
        TalkPos = new Vector2(0f, -392f),
        StateDisplayLiftY = 35f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorDaCapoBoss.IdleTexturePath);
        profile.Frame("attack", ArtFloorDaCapoBoss.AttackTexturePath);
        profile.Frame("hit", ArtFloorDaCapoBoss.HitTexturePath);
        profile.Frame("guard", ArtFloorDaCapoBoss.GuardTexturePath);
        profile.Frame("special", ArtFloorDaCapoBoss.SpecialTexturePath);
        profile.Swap("attack", 0.55f, "Attack");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("guard", 0.55f, "Guard");
        profile.Swap("special", 0.80f, "Special");
        return profile;
    }
}

public sealed partial class ArtFloorFirstPerformerCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ArtFloorFirstPerformer))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -188f), new(0.64f, 0.64f), -88f, -345f, 88f, -10f, new(0f, -185f), new(0f, -382f))
    {
        TalkPos = new Vector2(0f, -300f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorFirstPerformer.IdleTexturePath);
        return profile;
    }
}

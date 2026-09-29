using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Art;

public sealed partial class BeyondFragmentCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ArtFloorBeyondFragmentBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-108f, -205f), new(0.58f, 0.58f), -400f, -435f, 190f, 14f, new(0f, -210f), new(0f, -438f))
    {
        TalkPos = new Vector2(0f, -352f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorBeyondFragmentBoss.IdleTexturePath);
        profile.Frame(
            "attack",
            ArtFloorBeyondFragmentBoss.AttackTexturePath);
        profile.Frame(
            "attack_2",
            ArtFloorBeyondFragmentBoss.Attack2TexturePath);
        profile.Frame("hit", ArtFloorBeyondFragmentBoss.HitTexturePath);
        profile.Frame("ego", ArtFloorBeyondFragmentBoss.EgoTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("attack_2", 0.42f, "Attack2");
        profile.Swap("ego", 0.60f, "Ego");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

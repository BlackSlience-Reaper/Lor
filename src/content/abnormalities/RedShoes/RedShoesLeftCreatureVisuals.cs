using Godot;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.RedShoes;

public partial class RedShoesLeftCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(LiteratureFloorEnhancedLeftShoe))]
    [MonsterVisual(typeof(RedShoesLeft))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -98f), new(0.52f, 0.52f), -150f, -255f, 150f, 35f, new(0f, -98f), new(0f, -310f))
    {
        TalkPos = new Vector2(0f, -230f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            RedShoesLeft.IdleTexturePath);
        profile.Frame("attack", RedShoesLeft.AttackTexturePath);
        profile.Frame("hit", RedShoesLeft.HitTexturePath);
        profile.Frame("parry", RedShoesLeft.ParryTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        profile.Swap("parry", 0.18f, "Cast");
        return profile;
    }
}

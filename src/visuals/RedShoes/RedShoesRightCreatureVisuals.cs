using Godot;
using LibraryOfRuina.monsters.RedShoes;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.RedShoes;

public partial class RedShoesRightCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(RedShoesRight))]
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
            RedShoesRight.IdleTexturePath);
        profile.Frame("attack", RedShoesRight.AttackTexturePath);
        profile.Frame("hit", RedShoesRight.HitTexturePath);
        profile.Swap("attack", 0.18f, "Attack");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

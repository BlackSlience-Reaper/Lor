using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.Leticia;

public partial class LeticiaCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(Leticia))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -124f), new(0.63f, 0.63f), -92f, -305f, 92f, 8f, new(0f, -126f), new(0f, -330f))
    {
        TalkPos = new Vector2(0f, -260f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            LeticiaAssets.LeticiaLeticiaMonsterPrefix + "idle.png");
        profile.Frame("attack", LeticiaAssets.LeticiaLeticiaMonsterPrefix + "attack.png")
            .Nudge(24f, -126f)
            .Scale(0.58f);
        profile.Frame("guard", LeticiaAssets.LeticiaLeticiaMonsterPrefix + "guard.png");
        profile.Frame("hit", LeticiaAssets.LeticiaLeticiaMonsterPrefix + "hit.png");
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Swap("guard", 0.42f, "Cast");
        profile.Swap("hit", 0.42f, "Hit");
        return profile;
    }
}

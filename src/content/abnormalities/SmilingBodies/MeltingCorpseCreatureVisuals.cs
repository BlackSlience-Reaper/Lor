using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.content.abnormalities.SmilingBodies;

public sealed partial class MeltingCorpseCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(MeltingCorpse))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -1f), new(0.28f, 0.28f), -145f, -124f, 145f, 36f, new(0f, -14f), new(0f, -151f))
    {
        TalkPos = new Vector2(0f, -81f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            MeltingCorpse.IdleTexturePath);
        profile.Swap("@idle:default", 0.38f, "Moan", "Spawn");
        profile.Swap("@idle:default", 0.28f, "Hit");
        return profile;
    }
}

using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Art;

public sealed partial class ArtFloorDustbornPersonCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ArtFloorDustbornPerson))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -80f), new(0.50f, 0.50f), -116f, -260f, 116f, 10f, new(0f, -112f), new(0f, -375f))
    {
        TalkPos = new Vector2(0f, -220f),
        StateDisplayLiftY = 70f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorDustbornPerson.IdleTexturePath);
        profile.Frame("pierce", ArtFloorDustbornPerson.PierceTexturePath);
        profile.Frame("slash", ArtFloorDustbornPerson.SlashTexturePath);
        profile.Frame("hit", ArtFloorDustbornPerson.HitTexturePath);
        profile.Frame("dodge", ArtFloorDustbornPerson.DodgeTexturePath);
        profile.Swap("pierce", 0.45f, "Pierce");
        profile.Swap("slash", 0.45f, "Slash");
        profile.Swap("hit", 0.45f, "Hit");
        profile.Swap("dodge", 0.45f, "Dodge");
        return profile;
    }
}

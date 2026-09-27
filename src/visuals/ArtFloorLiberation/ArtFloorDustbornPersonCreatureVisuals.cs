using LibraryOfRuina.monsters.ArtFloorLiberation;

namespace LibraryOfRuina.visuals.ArtFloorLiberation;

public sealed partial class ArtFloorDustbornPersonCreatureVisuals : SpriteAttackCreatureVisuals
{
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

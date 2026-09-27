using QueenBeeMonster = LibraryOfRuina.monsters.QueenBee.QueenBee;

namespace LibraryOfRuina.visuals.QueenBee;

public sealed partial class QueenBeeCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                QueenBeeMonster.IdleTexturePath)
            .IdleOnly();
        profile.Frame("hit", QueenBeeMonster.HitTexturePath);
        profile.Frame("defend", QueenBeeMonster.DefendTexturePath);
        profile.Frame("cast", QueenBeeMonster.DefendTexturePath);
        profile.Swap("defend", 0.45f, "Defend");
        profile.Swap("cast", 0.45f, "Cast");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

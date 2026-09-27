using LibraryOfRuina.audio;
using LibraryOfRuina.monsters.HistoryFloorLiberation;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorForgottenCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    protected override void BeforeResolveSpriteTrigger(string triggerName)
    {
        if (triggerName == "Hit")
        {
            LocalOggOneShotPlayer.Play(
                HistoryFloorForgottenBoss.ParrySfxPath,
                -2f);
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HistoryFloorForgottenBoss.IdleTexturePath);
        profile.Frame(
            "strike",
            HistoryFloorForgottenBoss.StrikeTexturePath);
        profile.Frame(
            "slash",
            HistoryFloorForgottenBoss.SlashTexturePath);
        profile.Frame(
            "special",
            HistoryFloorForgottenBoss.SpecialTexturePath);
        profile.Frame(
            "hit",
            HistoryFloorForgottenBoss.HitTexturePath);
        profile.Swap("strike", 0.86f, "Attack", "AttackStrike");
        profile.Swap("slash", 0.86f, "AttackSlash");
        profile.Swap(
            "special",
            0.86f,
            "LongingEmbrace",
            "Cast",
            "Parry");
        profile.Swap("hit", 0.63f, "Hit");
        return profile;
    }
}

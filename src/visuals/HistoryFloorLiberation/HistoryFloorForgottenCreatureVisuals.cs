using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorForgottenCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(HistoryFloorForgottenBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 45f), new(0.50f, 0.50f), -190f, -235f, 190f, 5f, new(0f, -88f), new(0f, -300f))
    {
        TalkPos = new Vector2(0f, -220f),
    };

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

using LibraryOfRuina.monsters.HistoryFloorLiberation;

namespace LibraryOfRuina.visuals.HistoryFloorLiberation;

public partial class HistoryFloorWaspBossCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HistoryFloorWaspBoss.IdleTexturePath)
            .At(0f, -128f)
            .Scale(0.46f)
            .IdleOnly();
        AddAttack(
            profile,
            "strike",
            HistoryFloorWaspBoss.AttackStrikeTexturePath);
        AddAttack(
            profile,
            "pierce",
            HistoryFloorWaspBoss.AttackPierceTexturePath);
        profile.Frame("hit", HistoryFloorWaspBoss.HitTexturePath);
        profile.Frame(
            "effect",
            HistoryFloorWaspBoss.EffectTexturePath);
        profile.Frame(
            "loyalty",
            HistoryFloorWaspBoss.LoyaltyTexturePath);
        profile.Lunge("strike", 0.22f, 0.12f, 0.25f, "Attack");
        profile.Lunge(
            "pierce",
            0.22f,
            0.12f,
            0.25f,
            "AttackPierce");
        profile.Swap("effect", 0.45f, "Cast");
        profile.Swap("loyalty", 0.45f, "Loyalty");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }

    private static void AddAttack(
        SpriteVisualProfile profile,
        string key,
        string path)
    {
        profile.Frame(key, path)
            .Nudge(26f, -128f)
            .Scale(0.52f);
    }
}

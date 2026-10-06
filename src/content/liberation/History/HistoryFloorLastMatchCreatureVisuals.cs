using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.History;

/// <summary>
/// 最后的火柴的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按标注点对齐、动作不转身体），
/// 加载失败时退回下面的逐帧换图。
/// </summary>
public partial class HistoryFloorLastMatchCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "history_floor",
        "last_match",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "cast",
            ["Parry"] = "cast",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(HistoryFloorLastMatch))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 0f), new(0.38f, 0.38f), -78f, -120f, 107f, 8f, new(0f, -56f), new(0f, -150f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            HistoryFloorLastMatch.IdleTexturePath);
        profile.Frame(
                "attack",
                HistoryFloorLastMatch.AttackTexturePath)
            .Nudge(14f, 0f)
            .Scale(0.40f);
        profile.Frame(
            "cast",
            HistoryFloorLastMatch.CastTexturePath);
        profile.Frame(
            "hit",
            HistoryFloorLastMatch.HitTexturePath);
        profile.Lunge("attack", 0.2f, 0.1f, 0.25f, "Attack");
        profile.Swap("cast", 0.45f, "Cast", "Parry");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }
}

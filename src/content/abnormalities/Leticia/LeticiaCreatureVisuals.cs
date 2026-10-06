using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.Leticia;

/// <summary>
/// 蕾蒂希娅：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，整图只平移转动，各姿势按标注点对齐），
/// 加载失败时退回逐帧换图。施法照原来换防御姿势。
/// </summary>
public partial class LeticiaCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "leticia",
        "leticia",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "guard",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(Leticia))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -124f), new(0.63f, 0.63f), -92f, -287f, 92f, 8f, new(0f, -126f), new(0f, -330f))
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

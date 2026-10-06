using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

/// <summary>
/// 隐士之杖（木偶）：Spine 身体见 <see cref="LayeredBossSpine"/>（整块，各姿势按标注点对齐），加载失败时退回逐帧换图。
/// </summary>
public sealed partial class HermitStaffCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "hermit_staff", "hermit_staff", "attack", new Dictionary<string, string>());

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(HermitStaff))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -100f), new(0.46f, 0.46f), -90f, -220f, 90f, 8f, new(0f, -100f), new(0f, -250f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HermitStaff.IdleTexturePath)
            .Scale(0.515f);
        profile.Frame("attack", HermitStaff.AttackTexturePath);
        profile.Frame("hit", HermitStaff.HitTexturePath);
        profile.Swap("attack", 0.48f, "Attack");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

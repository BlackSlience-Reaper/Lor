using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.abnormalities.SmilingBodies;

/// <summary>
/// 溶解的死尸：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，整图只平移转动），加载失败时退回贴图。
/// 只有一张待机图，骨架只有待机、受击、死亡；呻吟、生成原来就停在待机图，不另映射。
/// </summary>
public sealed partial class MeltingCorpseCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "smiling_bodies", "melting_corpse", "hurt", new Dictionary<string, string>());

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

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

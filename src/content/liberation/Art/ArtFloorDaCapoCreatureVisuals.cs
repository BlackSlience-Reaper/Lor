using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Art;

/// <summary>Da Capo（含终章）的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class ArtFloorDaCapoCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "art_floor",
        "dacapo",
        "attack",
        new Dictionary<string, string>
        {
            ["Guard"] = "guard",
            ["Special"] = "special",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(ArtFloorDaCapoBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -188f), new(0.58f, 0.58f), -185f, -428f, 182f, -15f, new(0f, -205f), new(0f, -452f))
    {
        TalkPos = new Vector2(0f, -392f),
    };

    // 终幕版本只多了状态条上移。
    [MonsterVisual(typeof(ArtFloorFinalDaCapoBoss))]
    internal static readonly CreatureVisualLayout FinalLayout = new(
        new(0f, -188f), new(0.58f, 0.58f), -215f, -472f, 215f, 36f, new(0f, -205f), new(0f, -452f))
    {
        TalkPos = new Vector2(0f, -392f),
        StateDisplayLiftY = 35f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorDaCapoBoss.IdleTexturePath);
        profile.Frame("attack", ArtFloorDaCapoBoss.AttackTexturePath);
        profile.Frame("hit", ArtFloorDaCapoBoss.HitTexturePath);
        profile.Frame("guard", ArtFloorDaCapoBoss.GuardTexturePath);
        profile.Frame("special", ArtFloorDaCapoBoss.SpecialTexturePath);
        profile.Swap("attack", 0.55f, "Attack");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("guard", 0.55f, "Guard");
        profile.Swap("special", 0.80f, "Special");
        return profile;
    }
}

/// <summary>
/// 第一演奏者：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，只平移、转动），加载失败时退回贴图。
/// 只有一张待机图，动作只有待机轻晃和受击。
/// </summary>
public sealed partial class ArtFloorFirstPerformerCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "art_floor", "first_performer", "hurt", new Dictionary<string, string>());

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(ArtFloorFirstPerformer))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -188f), new(0.64f, 0.64f), -88f, -354f, 88f, -10f, new(0f, -185f), new(0f, -382f))
    {
        TalkPos = new Vector2(0f, -300f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorFirstPerformer.IdleTexturePath);
        return profile;
    }
}

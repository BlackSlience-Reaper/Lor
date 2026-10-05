using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Art;

/// <summary>彼方的碎片的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class BeyondFragmentCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "art_floor",
        "beyond_fragment",
        "attack",
        new Dictionary<string, string>
        {
            ["Attack2"] = "attack2",
            ["Ego"] = "ego",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(ArtFloorBeyondFragmentBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(-108f, -205f), new(0.58f, 0.58f), -400f, -435f, 190f, 14f, new(0f, -210f), new(0f, -438f))
    {
        TalkPos = new Vector2(0f, -352f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorBeyondFragmentBoss.IdleTexturePath);
        profile.Frame(
            "attack",
            ArtFloorBeyondFragmentBoss.AttackTexturePath);
        profile.Frame(
            "attack_2",
            ArtFloorBeyondFragmentBoss.Attack2TexturePath);
        profile.Frame("hit", ArtFloorBeyondFragmentBoss.HitTexturePath);
        profile.Frame("ego", ArtFloorBeyondFragmentBoss.EgoTexturePath);
        profile.Swap("attack", 0.42f, "Attack");
        profile.Swap("attack_2", 0.42f, "Attack2");
        profile.Swap("ego", 0.60f, "Ego");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Art;

/// <summary>我们的小小银河的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class ArtFloorLittleGalaxyCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "art_floor",
        "little_galaxy",
        "attack",
        new Dictionary<string, string>
        {
            ["Ego"] = "attack",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(ArtFloorLittleGalaxyBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -28f), new(0.52f, 0.52f), -170f, -330f, 170f, 38f, new(0f, -88f), new(0f, -420f))
    {
        TalkPos = new Vector2(0f, -240f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorLittleGalaxyBoss.IdleTexturePath);
        profile.Frame("hit", ArtFloorLittleGalaxyBoss.HitTexturePath);
        profile.Frame(
            "attack_s4",
            ArtFloorLittleGalaxyBoss.AttackFrameS4TexturePath);
        profile.Frame(
            "attack_s3",
            ArtFloorLittleGalaxyBoss.AttackFrameS3TexturePath);
        profile.Frame(
            "attack_s2",
            ArtFloorLittleGalaxyBoss.AttackFrameS2TexturePath);
        profile.Frame(
            "attack_s1",
            ArtFloorLittleGalaxyBoss.AttackFrameS1TexturePath);
        profile.Sequence(
            ["attack_s4", "attack_s3", "attack_s2", "attack_s1"],
            AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds,
            "Attack",
            "Ego");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Art;

/// <summary>欢愉的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class ArtFloorPleasureCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "art_floor",
        "pleasure",
        "blunt",
        new Dictionary<string, string>
        {
            ["Blunt"] = "blunt",
            ["AttackBlunt"] = "blunt",
            ["Pierce"] = "pierce",
            ["AttackPierce"] = "pierce",
            ["Slash"] = "slash",
            ["AttackSlash"] = "slash",
            ["Dodge"] = "dodge",
            ["EgoS1"] = "ego_s1",
            ["EgoS2"] = "ego_s2",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(ArtFloorPleasureBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -24f), new(0.58f, 0.58f), -132f, -290f, 132f, 40f, new(0f, -132f), new(0f, -328f))
    {
        TalkPos = new Vector2(0f, -236f),
        StateDisplayLiftY = 10f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorPleasureBoss.IdleTexturePath);
        profile.Frame("hit", ArtFloorPleasureBoss.HitTexturePath);
        profile.Frame(
            "attack_blunt",
            ArtFloorPleasureBoss.AttackBluntTexturePath);
        profile.Frame(
            "attack_pierce",
            ArtFloorPleasureBoss.AttackPierceTexturePath);
        profile.Frame(
            "attack_slash",
            ArtFloorPleasureBoss.AttackSlashTexturePath);
        profile.Frame("ego_s1", ArtFloorPleasureBoss.EgoS1TexturePath);
        profile.Frame("ego_s2", ArtFloorPleasureBoss.EgoS2TexturePath);

        float segment = AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds;
        profile.Swap("attack_blunt", segment, "Blunt", "AttackBlunt");
        profile.Swap(
            "attack_pierce",
            segment,
            "Pierce",
            "AttackPierce");
        profile.Swap("attack_slash", segment, "Slash", "AttackSlash");
        profile.Swap("attack_pierce", 0.42f, "Dodge");
        profile.Swap("ego_s1", segment, "EgoS1");
        profile.Swap("ego_s2", segment, "EgoS2");
        profile.Swap("hit", 0.40f, "Hit");
        return profile;
    }
}

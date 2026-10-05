using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Art;

/// <summary>余香的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class ArtFloorNostalgicScentCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "art_floor",
        "nostalgic_scent",
        "blunt",
        new Dictionary<string, string>
        {
            ["Ranged"] = "ranged",
            ["Blunt"] = "blunt",
            ["Pierce"] = "pierce",
            ["Guard"] = "guard",
            ["EgoS1"] = "ego_s1",
            ["EgoS2"] = "ego_s2",
            ["EgoS3"] = "ego_s3",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(ArtFloorNostalgicScentBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -30f), new(0.58f, 0.58f), -148f, -340f, 148f, 12f, new(0f, -154f), new(0f, -370f))
    {
        TalkPos = new Vector2(0f, -292f),
        StateDisplayLiftY = 30f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            ArtFloorNostalgicScentBoss.IdleTexturePath);
        profile.Frame(
            "ranged",
            ArtFloorNostalgicScentBoss.RangedTexturePath);
        profile.Frame("blunt", ArtFloorNostalgicScentBoss.BluntTexturePath);
        profile.Frame(
            "pierce",
            ArtFloorNostalgicScentBoss.PierceTexturePath);
        profile.Frame("hit", ArtFloorNostalgicScentBoss.HitTexturePath);
        profile.Frame("guard", ArtFloorNostalgicScentBoss.GuardTexturePath);
        profile.Frame("ego_s1", ArtFloorNostalgicScentBoss.EgoS1TexturePath);
        profile.Frame("ego_s2", ArtFloorNostalgicScentBoss.EgoS2TexturePath);
        profile.Frame("ego_s3", ArtFloorNostalgicScentBoss.EgoS3TexturePath);

        float segment = ArtFloorNostalgicScentBoss.AttackSegmentDelaySeconds;
        profile.Swap("ranged", segment, "Ranged");
        profile.Swap("blunt", segment, "Blunt");
        profile.Swap("pierce", segment, "Pierce");
        profile.Swap("hit", 0.5f, "Hit");
        profile.Swap("guard", 0.6f, "Guard");
        profile.Sequence(["ego_s1", "ego_s2", "ego_s3"], segment, "EgoS1");
        profile.Sequence(["ego_s2", "ego_s3", "ego_s1"], segment, "EgoS2");
        profile.Sequence(["ego_s3", "ego_s1", "ego_s2"], segment, "EgoS3");
        return profile;
    }
}

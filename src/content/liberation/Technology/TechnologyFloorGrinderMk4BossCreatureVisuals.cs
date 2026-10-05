using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Technology;

/// <summary>研削机Mk4的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public partial class TechnologyFloorGrinderMk4BossCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "technology_floor",
        "grinder_mk4",
        "attack_slash",
        new Dictionary<string, string>
        {
            ["AttackStrike"] = "attack_slash",
            ["AttackSlash"] = "attack_slash",
            ["AttackThrust"] = "attack_thrust",
            ["Cast"] = "dodge",
            ["EgoS1"] = "ego_s1",
            ["EgoS2"] = "ego_s2",
            ["EgoS3"] = "ego_s3",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(TechnologyFloorGrinderMk4Boss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -155f), new(0.576f, 0.576f), -170f, -315f, 170f, 12f, new(0f, -155f), new(-20f, -340f))
    {
        TalkPos = new Vector2(-20f, -280f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                TechnologyFloorGrinderMk4Boss.IdleTexturePath)
            .At(0f, -120f)
            .Scale(0.50f)
            .IdleOnly();
        profile.Frame(
            "slash",
            TechnologyFloorGrinderMk4Boss.SlashTexturePath);
        profile.Frame(
            "thrust",
            TechnologyFloorGrinderMk4Boss.ThrustTexturePath);
        profile.Frame("hit", TechnologyFloorGrinderMk4Boss.HitTexturePath);
        profile.Frame(
            "dodge",
            TechnologyFloorGrinderMk4Boss.DodgeTexturePath);
        profile.Frame(
            "ego_s1",
            TechnologyFloorGrinderMk4Boss.EgoS1TexturePath);
        profile.Frame(
            "ego_s2",
            TechnologyFloorGrinderMk4Boss.EgoS2TexturePath);
        profile.Frame(
            "ego_s3",
            TechnologyFloorGrinderMk4Boss.EgoS3TexturePath);
        profile.Swap(
            "slash",
            0.5f,
            "Attack",
            "AttackStrike",
            "AttackSlash");
        profile.Swap("thrust", 0.5f, "AttackThrust");
        profile.Swap("hit", 0.28f, "Hit");
        profile.Swap("dodge", 0.6f, "Cast");
        profile.Swap("ego_s1", 0.5f, "EgoS1");
        profile.Swap("ego_s2", 0.5f, "EgoS2");
        profile.Swap("ego_s3", 0.7f, "EgoS3");
        return profile;
    }
}

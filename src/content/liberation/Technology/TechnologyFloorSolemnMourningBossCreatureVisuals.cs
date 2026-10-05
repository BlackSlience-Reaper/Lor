using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Technology;

/// <summary>庄严哀悼的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class TechnologyFloorSolemnMourningBossCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "technology_floor",
        "solemn_mourning",
        "attack",
        new Dictionary<string, string>
        {
            ["AttackWhite"] = "attack",
            ["Guard"] = "guard",
            ["Cast"] = "guard",
            ["EgoS1"] = "ego_s1",
            ["EgoS2"] = "ego_s2",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(TechnologyFloorSolemnMourningBoss))]
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
            TechnologyFloorSolemnMourningBoss.IdleTexturePath);
        profile.Frame(
            "attack",
            TechnologyFloorSolemnMourningBoss.AttackTexturePath);
        profile.Frame(
            "hit",
            TechnologyFloorSolemnMourningBoss.HitTexturePath);
        profile.Frame(
            "parry",
            TechnologyFloorSolemnMourningBoss.ParryTexturePath);
        profile.Frame(
                "ego_s1",
                TechnologyFloorSolemnMourningBoss.EgoS1TexturePath)
            .Nudge(-488f, 0f);
        profile.Frame(
                "ego_s2",
                TechnologyFloorSolemnMourningBoss.EgoS2TexturePath)
            .Nudge(-511f, 0f);
        profile.Swap("attack", 0.42f, "Attack", "AttackWhite");
        profile.Swap("hit", 0.40f, "Hit");
        profile.Swap("parry", 0.40f, "Guard", "Cast");
        profile.Swap("ego_s1", 1.0f, "EgoS1");
        profile.Swap("ego_s2", 1.0f, "EgoS2");
        return profile;
    }
}

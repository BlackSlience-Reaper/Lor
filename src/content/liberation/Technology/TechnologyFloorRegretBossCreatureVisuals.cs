using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Technology;

/// <summary>悔恨的外观：Spine 身体见 <see cref="TechnologyFloorBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public partial class TechnologyFloorRegretBossCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = TechnologyFloorBossSpine.Create(
        "regret",
        "attack_right",
        new Dictionary<string, string>
        {
            ["AttackStrike"] = "attack_right",
            ["AttackThrust"] = "attack_left",
            ["AttackSlash"] = "attack_slash",
            ["EgoFinal"] = "ego",
            ["Cast"] = "parry",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(TechnologyFloorRegretBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -155f), new(0.576f, 0.576f), -170f, -277f, 170f, 12f, new(0f, -155f), new(-20f, -340f))
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
                TechnologyFloorRegretBoss.IdleTexturePath)
            .At(0f, -117f)
            .Scale(0.50f)
            .IdleOnly();
        profile.Frame(
            "attack_right",
            TechnologyFloorRegretBoss.AttackRightTexturePath);
        profile.Frame(
            "attack_left",
            TechnologyFloorRegretBoss.AttackLeftTexturePath);
        profile.Frame(
            "attack_slash",
            TechnologyFloorRegretBoss.AttackSlashTexturePath);
        profile.Frame("hit", TechnologyFloorRegretBoss.HitTexturePath);
        profile.Frame("parry", TechnologyFloorRegretBoss.ParryTexturePath);
        profile.Frame("ego", TechnologyFloorRegretBoss.EgoTexturePath);
        profile.Swap("attack_right", 0.5f, "AttackStrike", "Attack");
        profile.Swap("attack_left", 0.5f, "AttackThrust");
        profile.Swap("attack_slash", 0.5f, "AttackSlash");
        profile.Swap("ego", 1.1f, "EgoFinal");
        profile.Swap("hit", 0.28f, "Hit");
        profile.Swap("parry", 0.6f, "Cast");
        return profile;
    }
}

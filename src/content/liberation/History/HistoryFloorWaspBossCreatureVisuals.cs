using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.History;

/// <summary>蜂后的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public partial class HistoryFloorWaspBossCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "history_floor",
        "wasp",
        "strike",
        new Dictionary<string, string>
        {
            ["AttackPierce"] = "pierce",
            ["Cast"] = "cast",
            ["Loyalty"] = "cast",
            ["Defend"] = "cast",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(HistoryFloorWaspBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -128f), new(0.46f, 0.46f), -170f, -315f, 170f, 24f, new(0f, -126f), new(0f, -342f))
    {
        TalkPos = new Vector2(0f, -268f),
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                HistoryFloorWaspBoss.IdleTexturePath)
            .At(0f, -128f)
            .Scale(0.46f)
            .IdleOnly();
        AddAttack(
            profile,
            "strike",
            HistoryFloorWaspBoss.AttackStrikeTexturePath);
        AddAttack(
            profile,
            "pierce",
            HistoryFloorWaspBoss.AttackPierceTexturePath);
        profile.Frame("hit", HistoryFloorWaspBoss.HitTexturePath);
        profile.Frame(
            "effect",
            HistoryFloorWaspBoss.EffectTexturePath);
        profile.Frame(
            "loyalty",
            HistoryFloorWaspBoss.LoyaltyTexturePath);
        profile.Lunge("strike", 0.22f, 0.12f, 0.25f, "Attack");
        profile.Lunge(
            "pierce",
            0.22f,
            0.12f,
            0.25f,
            "AttackPierce");
        profile.Swap("effect", 0.45f, "Cast");
        profile.Swap("loyalty", 0.45f, "Loyalty");
        profile.Swap("hit", 0.12f, "Hit");
        return profile;
    }

    private static void AddAttack(
        SpriteVisualProfile profile,
        string key,
        string path)
    {
        profile.Frame(key, path)
            .Nudge(26f, -128f)
            .Scale(0.52f);
    }
}

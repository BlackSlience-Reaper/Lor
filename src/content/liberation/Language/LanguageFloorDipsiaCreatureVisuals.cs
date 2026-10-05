using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Language;

/// <summary>渴血症的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class LanguageFloorDipsiaCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "language_floor_liberation",
        "dipsia",
        "strike",
        new Dictionary<string, string>
        {
            ["AttackFire"] = "fire",
            ["AttackStrike"] = "strike",
            ["AttackSlash"] = "slash",
            ["GroupBreak"] = "group_break",
            ["GroupAttack"] = "group_attack",
            ["Cast"] = "evade",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(LanguageFloorDipsia))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -128f), new(0.72f, 0.72f), -160f, -360f, 160f, 12f, new(0f, -150f), new(0f, -395f))
    {
        TalkPos = new Vector2(0f, -305f),
        StateDisplayLiftY = 18f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                LanguageFloorDipsia.IdleTexturePath)
            .Scale(0.72f);

        profile.Frame("fire", LanguageFloorDipsia.FireTexturePath)
            .Scale(0.72f);
        profile.Frame("strike", LanguageFloorDipsia.StrikeTexturePath)
            .Scale(0.72f)
            .Nudge(-100f, 30f);
        profile.Frame("slash", LanguageFloorDipsia.SlashTexturePath)
            .Scale(0.84f)
            .Nudge(65f, 250f);
        profile.Frame(
                "group_break",
                LanguageFloorDipsia.GroupBreakTexturePath)
            .Scale(0.60f);
        profile.Frame(
                "group_attack",
                LanguageFloorDipsia.GroupAttackTexturePath)
            .Scale(0.60f)
            .Nudge(-185f, 105f);
        profile.Frame("evade", LanguageFloorDipsia.EvadeTexturePath)
            .Scale(0.58f);
        profile.Frame("hit", LanguageFloorDipsia.HitTexturePath)
            .Scale(0.50f);

        profile.Swap("fire", 0.45f, "AttackFire");
        profile.Swap("strike", 0.45f, "AttackStrike");
        profile.Swap("slash", 0.45f, "AttackSlash");
        profile.Swap(
            "group_break",
            LanguageFloorDipsia.GroupBreakSegmentSeconds,
            "GroupBreak");
        profile.Swap("group_attack", 0.55f, "GroupAttack");
        profile.Swap("evade", 0.45f, "Cast");
        profile.Swap("hit", 0.18f, "Hit");
        return profile;
    }
}

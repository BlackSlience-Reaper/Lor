using System;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.History;

/// <summary>
/// 历史层阶段 Boss：五个阶段分别用焦化少女、快乐泰迪、小帮手、精灵女王、左红舞鞋的图，Spine 身体直接套用这几只的骨架
/// （骨架原点对齐的是待机贴图，与它们自己的外观同一张图，所以布局不同也对得上），加载失败时退回逐帧换图。
/// 施法、招架照原来换的那张图对到骨架里的动作：二阶段是泰迪的拥抱（攻击 2 图），四、五阶段是各自的施法，其余阶段用攻击图。
/// </summary>
public partial class HistoryFloorPhaseBossCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    private static RuntimeSpineBody.Spec PhaseSpine(RuntimeSpineBody.Spec spec, string castAnimation) => spec with
    {
        ExtraTriggers = new Dictionary<string, string>
        {
            ["Cast"] = castAnimation,
            ["Parry"] = castAnimation,
        },
    };

    internal static readonly RuntimeSpineBody.Spec Phase1Spine =
        PhaseSpine(abnormalities.ScorchedGirl.ScorchedGirlMonsterCreatureVisuals.Spine, "attack");

    internal static readonly RuntimeSpineBody.Spec Phase2Spine =
        PhaseSpine(abnormalities.HappyTeddy.HappyTeddyCreatureVisuals.Spine, "embrace");

    internal static readonly RuntimeSpineBody.Spec Phase3Spine =
        PhaseSpine(abnormalities.AllAroundHelper.AllAroundHelperCreatureVisuals.Spine, "attack");

    internal static readonly RuntimeSpineBody.Spec Phase4Spine =
        PhaseSpine(abnormalities.FairyFestival.FairyQueenCreatureVisuals.Spine, "cast");

    internal static readonly RuntimeSpineBody.Spec Phase5Spine =
        PhaseSpine(abnormalities.RedShoes.RedShoesLeftCreatureVisuals.Spine, "cast");

    internal override RuntimeSpineBody.Spec SpineSpec => Phase1Spine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        [Phase1Spine, Phase2Spine, Phase3Spine, Phase4Spine, Phase5Spine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) => variantKey switch
    {
        "phase_2" => Phase2Spine,
        "phase_3" => Phase3Spine,
        "phase_4" => Phase4Spine,
        "phase_5" => Phase5Spine,
        _ => Phase1Spine,
    };

    [MonsterVisual(typeof(HistoryFloorPhaseBoss))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -118f), new(0.58f, 0.58f), -155f, -310f, 155f, 10f, new(0f, -120f), new(0f, -340f))
    {
        TalkPos = new Vector2(0f, -260f),
    };

    // 初始立绘按当前阶段取；_Ready 里再按阶段切到对应变体。
    [MonsterVisualFactory]
    internal static NCreatureVisuals CreateForMonster(MonsterModel monster)
    {
        string id = monster.Id.Entry;
        int phase = monster is HistoryFloorPhaseBoss boss ? boss.Phase : 1;
        return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<HistoryFloorPhaseBossCreatureVisuals>(
            id,
            HistoryFloorPhaseBoss.IdleTexturePathForPhase(phase));
    }

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        SetSpriteVisualVariant(VariantKey(ResolvePhase()));
    }

    private int ResolvePhase()
    {
        return (GetParent() as NCreature)?.Entity?.Monster
            is HistoryFloorPhaseBoss boss
            ? boss.Phase
            : 1;
    }

    private static string VariantKey(int phase) =>
        $"phase_{Math.Clamp(phase, 1, 5)}";

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        for (int phase = 1; phase <= 5; phase++)
        {
            string variant = VariantKey(phase);
            string attack = variant + "_attack";
            string cast = variant + "_cast";
            string hit = variant + "_hit";
            profile.Variant(
                variant,
                HistoryFloorPhaseBoss.IdleTexturePathForPhase(phase));
            profile.Frame(
                    attack,
                    HistoryFloorPhaseBoss.AttackTexturePathForPhase(phase))
                .ForVariant(variant)
                .Nudge(28f, -116f)
                .Scale(0.60f);
            profile.Frame(
                cast,
                HistoryFloorPhaseBoss.CastTexturePathForPhase(phase))
                .ForVariant(variant);
            profile.Frame(
                hit,
                HistoryFloorPhaseBoss.HitTexturePathForPhase(phase))
                .ForVariant(variant);
            profile.Lunge(
                    attack,
                    0.22f,
                    0.12f,
                    0.25f,
                    "Attack")
                .ForVariant(variant);
            profile.Swap(cast, 0.45f, "Cast", "Parry")
                .ForVariant(variant);
            profile.Swap(hit, 0.12f, "Hit")
                .ForVariant(variant);
        }

        profile.InitialVariant(VariantKey(1));
        return profile;
    }
}

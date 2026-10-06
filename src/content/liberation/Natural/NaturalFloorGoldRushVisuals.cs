using System.Linq;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorGoldRushBoss), ScenePath = NaturalFloorGoldRushVisuals.ScenePath)]
internal sealed partial class NaturalFloorGoldRushVisuals : SceneAnimatedCreatureVisuals
{
    // Spine 身体按动画库（形态）各一副，见 tools/spine_from_layers/build_boss_configs.py 的自然层配置
    internal static readonly RuntimeSpineBody.Spec HumanSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "gold_rush_human",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "intro",
            ["Guard"] = "guard",
            ["SpecialAttack"] = "special_attack",
            ["SpecialIntro"] = "intro",
            ["Transform"] = "intro",
        });

    internal static readonly RuntimeSpineBody.Spec KingSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "gold_rush_king",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "intro",
            ["Guard"] = "guard",
            ["SpecialAttack"] = "special_attack",
            ["SpecialIntro"] = "intro",
            ["Transform"] = "intro",
        });

    internal static readonly RuntimeSpineBody.Spec HumanChargingSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "gold_rush_human_charging",
        "hurt",
        new Dictionary<string, string>
        {
        });

    internal static readonly RuntimeSpineBody.Spec KingChargingSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "gold_rush_king_charging",
        "hurt",
        new Dictionary<string, string>
        {
        });

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        [HumanSpine, KingSpine, HumanChargingSpine, KingChargingSpine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) => library switch
    {
        "king" => KingSpine,
        "human_charging" => HumanChargingSpine,
        "king_charging" => KingChargingSpine,
        _ => HumanSpine,
    };

    internal const string ScenePath = NaturalFloorAssets.GoldRushBossScene;
    internal const float AttackHitTime = 0.48f; // 普通攻击：参照贪婪国王的单次动作时点。
    internal const float GuardTime = 0.48f; // 为了幸福：格挡姿态保持秒数。
    internal const float CastTime = 0.48f; // 强化与饥饿：施放姿态保持秒数。
    internal const float SpecialIntroTime = 1f; // 群体攻击：起手姿态秒数。
    internal const float SpecialHitTime = 1f; // 群体攻击：每段伤害结算时点。
    internal const float TransformTime = 1f; // 闪烁的欲望：变身动作秒数。

    internal static readonly string[] AssetPaths = new[] { ScenePath }
        .Concat(new[] { "human", "king", "human_charging", "king_charging" }.Select(form =>
            "res://scenes/creature_visuals/natural_floor_gold_rush_" + form + "_animations.tres"))
        .Concat(new[] { "human_attack", "king_attack", "human_special", "king_special", "transform", "summon",
                "self_intoxication", "gluttony_success", "guard", "hit" }
            .Select(NaturalFloorGoldRushBoss.SoundPath))
        .Concat(new[] { "flickering_desire", "self_intoxication", "momentary_happiness", "king_of_greed_passive",
                "gluttony", "shining_happiness" }
            .Select(icon => NaturalFloorAssets.PowerIconPrefix + icon + "_power.png"))
        .ToArray();

    private NaturalFloorGoldRushBoss? Boss =>
        (GetParent() as NCreature)?.Entity?.Monster as NaturalFloorGoldRushBoss;

    protected override string ResolveCurrentAnimationLibrary()
    {
        string form = Boss?.IsKingForm == true ? "king" : "human";
        return Boss is { IsChargingSpecial: true, Creature.IsAlive: true } ? form + "_charging" : form;
    }

    protected override string NormalizeTriggerName(string name)
    {
        if (Boss is { IsChargingSpecial: true, Creature.IsAlive: true })
        {
            // 蓄力期间的待机、受击和格挡都保持蓄力图，群攻真正出手时由模型解除。
            return "Idle";
        }

        return name == "Dead" ? "Hit" : name;
    }
}

[MonsterVisual(typeof(NaturalFloorShiningHappiness), ScenePath = NaturalFloorHappinessVisuals.ScenePath)]
internal sealed partial class NaturalFloorHappinessVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = NaturalFloorAssets.ShiningHappinessScene;

    // 整块的 Spine 身体（待机轻浮慢摇），加载失败时退回场景动画
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "natural_floor_liberation", "nf_shining_happiness", "hurt", new Dictionary<string, string>());

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    protected override string ResolveCurrentAnimationLibrary() => "happiness";

    protected override string NormalizeTriggerName(string name) => name == "Dead" ? "Hit" : name;
}

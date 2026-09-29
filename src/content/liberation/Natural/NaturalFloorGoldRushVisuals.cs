using System.Linq;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorGoldRushBoss), ScenePath = NaturalFloorGoldRushVisuals.ScenePath)]
internal sealed partial class NaturalFloorGoldRushVisuals : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = "res://scenes/creature_visuals/natural_floor_gold_rush_boss.tscn";
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
            .Select(icon => "res://images/powers/library_of_ruina_" + icon + "_power.png"))
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
    internal const string ScenePath = "res://scenes/creature_visuals/natural_floor_shining_happiness.tscn";

    protected override string ResolveCurrentAnimationLibrary() => "happiness";

    protected override string NormalizeTriggerName(string name) => name == "Dead" ? "Hit" : name;
}

using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

/// <summary>
/// 原版风格画法（<see cref="IntentDisplayStyle.Vanilla"/>）不画默认画法的目标标记，改为在怪物掷出下回合招式时把悬停才有的
/// 指示线亮一下再淡出，悬停照旧显示。原版 <c>CombatManager</c> 在玩家回合开始时逐个调用 <c>Creature.PrepareForNextTurn</c>
/// （随后 <c>RefreshIntents</c> 淡入意图）；本模组的阶段转换、召唤和友方单位也走它，新出现的意图同样会亮一下。
/// 中途加入战斗的怪物（<c>rollNewMove: false</c>）不掷新招式，不亮；攻击方对侧只有一个单位时也不亮
/// （见 <see cref="TargetedIntentIndicatorPatch.Flash"/>）。只是本机显示，不改模型。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.PrepareForNextTurn))]
internal static class VanillaIntentTargetFlashPatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature __instance, bool rollNewMove)
    {
        if (rollNewMove && IntentDisplayStyleState.UsesVanilla(__instance))
        {
            // 敌方卡牌的出牌计划可能在同一帧稍后再重画意图，等这一帧结束再读节点。
            Callable.From(() => Flash(__instance)).CallDeferred();
        }
    }

    private static void Flash(Creature creature)
    {
        try
        {
            if (!creature.IsAlive
                || !IntentDisplayStyleState.UsesVanilla(creature)
                || CombatQueries.CreatureNodeOf(creature) is not { } creatureNode)
            {
                return;
            }

            var intents = new List<(NIntent, AbstractIntent, IEnumerable<Creature>)>();
            foreach (NIntent node in creatureNode.IntentContainer.GetChildren().OfType<NIntent>())
            {
                if (VanillaPrivate.IntentNodeIntent.Get(node) is not { } intent)
                {
                    continue;
                }

                // 与悬停同一规则（VanillaTargetIndicatorIntentDecorator）：线跟源意图走，附带效果拆出的图标按自身类型判断。
                AbstractIntent lineIntent = VanillaIntentProxies.TryGetSource(intent, out VanillaIntentProxySource? source) && source.IsPrimary
                    ? source.Source
                    : intent;
                intents.Add((node, lineIntent, VanillaPrivate.IntentNodeTargets.Get(node) ?? Array.Empty<Creature>()));
            }

            TargetedIntentIndicatorPatch.Flash(creatureNode, intents);
        }
        catch (Exception exception)
        {
            LorLog.PatchFailure("VanillaIntentTargetFlash", exception);
        }
    }
}

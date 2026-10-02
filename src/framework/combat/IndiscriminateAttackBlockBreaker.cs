using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.combat;

internal static class IndiscriminateAttackBlockBreaker
{
    // 无差别攻击：实际破坏格挡后、伤害结算前的表现停顿秒数。
    private const float BlockBreakPauseSeconds = 1f;

    // 无差别攻击：破坏当前格挡的比例，减少量向下取整。
    private const decimal BlockBreakRatio = 0.5m;

    private static readonly object SuppressionSync = new();
    private static readonly HashSet<BlockBreakSuppression> SuppressedNextDamageHooks = [];

    public static async Task<bool> BreakBlockBeforeDamage(
        MonsterModel attacker,
        int damage,
        IEnumerable<Creature> targets,
        bool suppressNextDamageHook = false)
    {
        ArgumentNullException.ThrowIfNull(attacker);

        Creature? dealer = attacker.Creature;
        CombatStateLike? combatState = dealer?.CombatState;
        if (dealer == null || combatState == null)
        {
            return false;
        }

        bool brokeAnyBlock = false;
        foreach (Creature target in targets
            .Where(static target => target != null)
            .Distinct()
            .OrderBy(static target => target.CombatId)
            .ToArray())
        {
            if (ConsumeSuppressedNextDamageHook(dealer, target, damage))
            {
                continue;
            }

            if (target is not { IsAlive: true, Block: > 0 })
            {
                continue;
            }

            decimal modifiedDamage = GetPreviewModifiedDamage(target, dealer, damage, ValueProp.Move);
            if ((int)modifiedDamage > target.Block)
            {
                decimal lostBlock = Math.Floor(target.Block * BlockBreakRatio);
                if (lostBlock <= 0m)
                {
                    continue;
                }

                await GameApi.LoseBlock(
                    new ThrowingPlayerChoiceContext(),
                    target,
                    lostBlock,
                    dealer);
                if (suppressNextDamageHook)
                {
                    SuppressNextDamageHook(dealer, target, damage);
                }

                brokeAnyBlock = true;
            }
        }

        return brokeAnyBlock;
    }

    public static async Task BreakBlockBeforeAttack(
        MonsterModel attacker,
        int damage,
        IEnumerable<Creature> targets,
        bool suppressNextDamageHook = true)
    {
        if (await BreakBlockBeforeDamage(
                attacker,
                damage,
                targets,
                suppressNextDamageHook))
        {
            await Cmd.CustomScaledWait(BlockBreakPauseSeconds, BlockBreakPauseSeconds);
        }
    }

    public static AttackCommand WithIndiscriminateBlockBreak(
        this AttackCommand attack,
        MonsterModel attacker,
        int damage,
        IEnumerable<Creature> targets)
    {
        ArgumentNullException.ThrowIfNull(attack);
        return attack.BeforeDamage(async () => await BreakBlockBeforeDamage(attacker, damage, targets));
    }

    private static decimal GetPreviewModifiedDamage(Creature target, Creature dealer, decimal damage, ValueProp props)
    {
        return GameApi.ModifyDamage(
            IRunState.GetFrom([target, dealer]),
            target.CombatState,
            target,
            dealer,
            damage,
            props,
            null, null, ModifyDamageHookType.All,
            CardPreviewMode.None,
            out _);
    }

    private static void SuppressNextDamageHook(Creature dealer, Creature target, int damage)
    {
        lock (SuppressionSync)
        {
            SuppressedNextDamageHooks.Add(new BlockBreakSuppression(dealer, target, damage));
        }
    }

    private static bool ConsumeSuppressedNextDamageHook(Creature dealer, Creature target, int damage)
    {
        lock (SuppressionSync)
        {
            return SuppressedNextDamageHooks.Remove(new BlockBreakSuppression(dealer, target, damage));
        }
    }

    private readonly record struct BlockBreakSuppression(Creature Dealer, Creature Target, int Damage);
}

using System;
using System.Threading.Tasks;
using LibraryOfRuina.intents;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.combat;

internal static class IndiscriminateAttackExecutor
{
    public static async Task<AttackCommand?> Execute(
        MonsterModel attacker,
        int damage,
        IEnumerable<Creature>? targets,
        Func<AttackCommand, AttackCommand> configure,
        PlayerChoiceContext? choiceContext = null)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(configure);

        IReadOnlyList<Creature> targetList = GetLivingTargets(
            targets,
            attacker.Creature);
        if (targetList.Count == 0 || attacker.Creature?.CombatState == null)
        {
            return null;
        }

        using (TargetedMonsterAttackHelper.ForceTargets(attacker.Creature, targetList))
        {
            await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(attacker, damage, targetList);
            AttackCommand configured = configure(DamageCmd.Attack(damage).FromMonster(attacker))
                ?? throw new InvalidOperationException("Indiscriminate attack configuration returned null.");

            return await configured
                .WithIndiscriminateBlockBreak(attacker, damage, targetList)
                .Execute(choiceContext);
        }
    }

    private static IReadOnlyList<Creature> GetLivingTargets(
        IEnumerable<Creature>? targets,
        Creature? attacker) =>
        CombatTargets.DeterministicLiving(targets, attacker);
}

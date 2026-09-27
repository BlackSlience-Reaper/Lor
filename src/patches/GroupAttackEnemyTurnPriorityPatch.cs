using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.intents;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.patches;

[HarmonyPatch]
internal static class GroupAttackEnemyTurnPriorityPatch
{
    private static readonly MethodInfo PrioritizeGroupAttackersMethod =
        AccessTools.Method(
            typeof(GroupAttackEnemyTurnPriorityPatch),
            nameof(PrioritizeGroupAttackers))
        ?? throw new MissingMethodException(
            typeof(GroupAttackEnemyTurnPriorityPatch).FullName,
            nameof(PrioritizeGroupAttackers));

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return FindExecuteEnemyTurnMethod() != null;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        MethodInfo method = FindExecuteEnemyTurnMethod()
            ?? throw new MissingMethodException(
                typeof(CombatManager).FullName,
                "ExecuteEnemyTurn(..., Func<Task>)");
        AsyncStateMachineAttribute? attribute = method.GetCustomAttribute<
            AsyncStateMachineAttribute>();
        Type stateMachineType = attribute?.StateMachineType
            ?? throw new MissingMethodException(
                typeof(CombatManager).FullName,
                "ExecuteEnemyTurn async state machine");
        return AccessTools.Method(stateMachineType, "MoveNext")
            ?? throw new MissingMethodException(
                stateMachineType.FullName,
                "MoveNext");
    }

    private static MethodInfo? FindExecuteEnemyTurnMethod()
    {
        return AccessTools.GetDeclaredMethods(typeof(CombatManager))
            .Where(static method =>
                method.Name == "ExecuteEnemyTurn"
                && method.ReturnType == typeof(Task))
            .FirstOrDefault(static method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length > 0
                    && parameters[^1].ParameterType == typeof(Func<Task>);
            });
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        bool replaced = false;

        foreach (CodeInstruction instruction in instructions)
        {
            if (!replaced
                && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                && instruction.operand is MethodInfo method
                && IsCreatureEnumerableToList(method))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = PrioritizeGroupAttackersMethod;
                replaced = true;
            }

            yield return instruction;
        }

        if (!replaced)
        {
            Log.Warn("LibraryOfRuina group attack enemy turn priority patch did not find CombatManager enemy list materialization.");
        }
    }

    private static bool IsCreatureEnumerableToList(MethodInfo method)
    {
        return method.Name == nameof(Enumerable.ToList)
            && method.DeclaringType == typeof(Enumerable)
            && method.IsGenericMethod
            && method.GetGenericArguments().Length == 1
            && method.GetGenericArguments()[0] == typeof(Creature);
    }

    public static List<Creature> PrioritizeGroupAttackers(IEnumerable<Creature> enemies)
    {
        return enemies
            .Select((enemy, index) => new OrderedEnemy(enemy, index))
            .OrderByDescending(static item => ShouldActBeforeStandardEnemies(item.Enemy))
            .ThenBy(static item => item.Index)
            .Select(static item => item.Enemy)
            .ToList();
    }

    private static bool ShouldActBeforeStandardEnemies(Creature enemy)
    {
        return enemy is { IsAlive: true, Monster: not null }
            && !enemy.Monster.SpawnedThisTurn
            && IntendsGroupAttack(enemy);
    }

    private static bool IntendsGroupAttack(Creature enemy)
    {
        return enemy.Monster?.NextMove.Intents.Any(IsGroupAttackIntent) == true;
    }

    private static bool IsGroupAttackIntent(AbstractIntent intent)
    {
        return intent is IGroupAttackIntent { IsGroupAttack: true };
    }

    private readonly record struct OrderedEnemy(Creature Enemy, int Index);
}

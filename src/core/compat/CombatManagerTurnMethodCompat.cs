using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace LibraryOfRuina.core.compat;

internal static class CombatManagerTurnMethodCompat
{
    private const string CombatTurnStateTypeName =
        "MegaCrit.Sts2.Core.Combat.CombatTurnState";

    public static MethodBase Resolve(string methodName)
    {
        MethodInfo[] candidates = AccessTools.GetDeclaredMethods(typeof(CombatManager))
            .Where(method => method.Name == methodName && method.ReturnType == typeof(Task))
            .ToArray();

        MethodInfo? turnStateOverload = candidates.FirstOrDefault(static method =>
        {
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 1
                && parameters[0].ParameterType.FullName == CombatTurnStateTypeName;
        });
        if (turnStateOverload != null)
        {
            return turnStateOverload;
        }

        MethodInfo? parameterlessOverload = candidates.FirstOrDefault(
            static method => method.GetParameters().Length == 0);
        if (parameterlessOverload != null)
        {
            return parameterlessOverload;
        }

        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        throw new MissingMethodException(
            typeof(CombatManager).FullName,
            methodName + " (supported turn-transition overload)");
    }

    public static Task EndCombatAsync(CombatManager combatManager)
    {
        MethodInfo method = AccessTools.DeclaredMethod(
                typeof(CombatManager),
                "EndCombatInternal",
                Type.EmptyTypes)
            ?? throw new MissingMethodException(
                typeof(CombatManager).FullName,
                "EndCombatInternal()");

        return method.Invoke(combatManager, null) as Task
            ?? throw new InvalidOperationException(
                "CombatManager.EndCombatInternal() did not return a Task.");
    }
}

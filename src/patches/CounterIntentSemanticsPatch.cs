using System.Linq;
using HarmonyLib;
using LibraryOfRuina.intents;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.IntendsToAttack), MethodType.Getter)]
internal static class CounterIntentSemanticsPatch
{
    [HarmonyPostfix]
    private static void Postfix(MonsterModel __instance, ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        __result = __instance.NextMove.Intents.Any(static intent =>
            intent is not ICounterIntent
            && (intent.IntentType == IntentType.Attack || intent.IntentType == IntentType.DeathBlow));
    }
}

using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Commands;

namespace LibraryOfRuina.content.liberation.Religion;

[HarmonyPatch(typeof(CreatureCmd), "KillWithoutCheckingWinCondition")]
[LibraryPatch(Reason = "失乐园第二阶段归零后保留战斗至下回合救赎；一罪与百善动画结束后才执行死亡及使徒清理。")]
internal static class ReligionFloorSalvationDeathPatch
{
    private static bool Prefix(Creature creature, ref Task __result)
    {
        if (creature.Monster is not ReligionFloorLostParadise
            || creature.CurrentHp > ReligionFloorRules.SalvationHp
            || creature.CombatState?.Encounter is not ReligionFloorLiberationEncounter
                { Phase: 2, IsSettling: false, Completed: false } encounter)
        {
            return true;
        }

        encounter.CheckSalvation();
        if (encounter.Outcome != ReligionFloorOutcome.Salvation)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

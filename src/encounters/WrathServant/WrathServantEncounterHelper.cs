using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.monsters.WrathServant;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.WrathServant;

internal static class WrathServantEncounterHelper
{
    public const string ServantSfxRoot = "res://audio/sfx/wrath_servant/";
    public const string HermitSfxRoot = "res://audio/sfx/green_stem_hermit/";

    public static readonly string[] PowerIconPaths =
    [
        "res://images/powers/wrath_servant_corrosion_power.png",
        "res://images/powers/wrath_servant_next_turn_corrosion_power.png",
        "res://images/powers/wrath_servant_staff_mark_power.png",
        "res://images/powers/wrath_servant_sinner_counter_power.png",
        "res://images/powers/green_stem_hermit_protection_power.png",
        "res://images/powers/cane_power.png",
        "res://images/atlases/power_atlas.sprites/wrath_servant_today_play_power.tres"
    ];

    public static bool IsWrathServantEncounter(CombatStateLike? combatState)
    {
        return combatState?.RunState.CurrentRoom is CombatRoom room
            && room.Encounter.MonstersWithSlots.Any(pair =>
                pair.Item1 is monsters.WrathServant.WrathServant or GreenStemHermit);
    }

    public static Creature? FindServant(CombatStateLike? combatState)
    {
        return combatState?.Creatures.FirstOrDefault(creature =>
            creature.IsAlive && creature.Monster is monsters.WrathServant.WrathServant);
    }

    public static Creature? FindHermit(CombatStateLike? combatState)
    {
        return combatState?.Creatures.FirstOrDefault(creature =>
            creature.IsAlive && creature.Monster is GreenStemHermit);
    }

    public static IReadOnlyList<Creature> FindStaffs(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return [];
        }

        return CombatTargets.DeterministicLiving(
            combatState.Creatures.Where(static creature =>
                creature.Monster is HermitStaff));
    }

    public static bool StaffsExist(CombatStateLike? combatState)
    {
        return combatState?.Creatures.Any(creature =>
            creature.IsAlive && creature.Monster is HermitStaff) == true;
    }

    /// <summary>
    /// 获取侍从的优先攻击目标：杖 > 隐士
    /// </summary>
    public static IReadOnlyList<Creature> GetServantTargets(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return [];
        }

        // 优先攻击一个杖；单目标列表才能让意图显示当前特殊目标头像。
        IReadOnlyList<Creature> staffs = FindStaffs(combatState);
        if (staffs.Count > 0)
        {
            return [staffs[0]];
        }

        // 其次攻击隐士
        Creature? hermit = FindHermit(combatState);
        return hermit != null ? [hermit] : [];
    }

    /// <summary>
    /// 获取隐士群体攻击目标：所有玩家 + 愤怒侍从
    /// </summary>
    public static IReadOnlyList<Creature> GetHermitGroupTargets(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return [];
        }

        return CombatTargets.DeterministicLiving(
            combatState.Creatures.Where(static creature =>
                creature.IsPlayer
                || creature.Monster
                    is monsters.WrathServant.WrathServant));
    }

    public static async Task CheckSpecialDeathOutcome(
        PlayerChoiceContext choiceContext,
        Creature deadCreature,
        Creature? dealer,
        bool wasRemovalPrevented)
    {
        if (wasRemovalPrevented
            || deadCreature.CombatState == null
            || !IsWrathServantEncounter(deadCreature.CombatState))
        {
            return;
        }

        if (deadCreature.Monster is monsters.WrathServant.WrathServant)
        {
            MarkServantDeathOutcome(deadCreature.CombatState);
            await CleanupRemainingEnemiesAndWin(deadCreature.CombatState);

            return;
        }

        if (CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (deadCreature.Monster is GreenStemHermit)
        {
            await OnHermitDied(choiceContext, deadCreature, dealer);
        }
    }

    private static void MarkServantDeathOutcome(CombatStateLike combatState)
    {
        if (FindHermit(combatState) == null)
        {
            return;
        }

        if (combatState.RunState.CurrentRoom is CombatRoom { Encounter: WrathServantStrong encounter })
        {
            encounter.MarkEndedByServantDeath();
        }
    }

    private static bool IsServantDeathOutcome(CombatStateLike combatState)
    {
        return combatState.RunState.CurrentRoom is CombatRoom { Encounter: WrathServantStrong encounter }
            && encounter.EndedByServantDeath;
    }

    private static async Task OnHermitDied(PlayerChoiceContext choiceContext, Creature hermit, Creature? dealer)
    {
        CombatStateLike? combatState = hermit.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 只有侍从能杀死隐士（有 ProtectionPower 保底），所以如果隐士真的死了，就是侍从杀的
        // 播放胜利动画然后清场
        if (IsServantDeathOutcome(combatState))
        {
            return;
        }

        Creature? servant = FindServant(combatState);
        if (servant != null)
        {
            await PlayVictoryAnimation(servant);
        }

        await CleanupRemainingEnemiesAndWin(combatState);
    }

    public static async Task PlayVictoryAnimation(Creature servant)
    {
        // 切换到特殊图2s
        await CreatureCmd.TriggerAnim(servant, "Victory", 0f);
        await Cmd.CustomScaledWait(2f, 2f);
    }

    private static async Task CleanupRemainingEnemiesAndWin(CombatStateLike combatState)
    {
        foreach (Creature enemy in combatState.Enemies.ToArray())
        {
            if (enemy.IsDead)
            {
                continue;
            }

            await CreatureCmd.Kill(enemy, force: true);
        }

        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }
    }
}

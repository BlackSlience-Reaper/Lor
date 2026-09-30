using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.infra.lifecycle;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

internal static class LittleRedMercenaryEncounterHelper
{
    public const string SfxRoot = LittleRedMercenaryAssets.LittleRedMercenarySfxRoot;
    private static readonly string LittleRedMercenaryPageRelicTitleLocKey =
        $"{ModelDb.GetId<LittleRedMercenaryPageRelic>().Entry}.title";

    public static bool IsLittleRedEncounter(CombatStateLike? combatState)
    {
        return combatState?.RunState.CurrentRoom is CombatRoom room
            && room.Encounter.MonstersWithSlots.Any(pair =>
                pair.Item1 is LittleRedRidingHoodedMercenary or WolfInHerNightmares);
    }

    public static Creature? FindLittleRed(CombatStateLike? combatState)
    {
        return combatState?.Creatures.FirstOrDefault(creature =>
            creature.IsAlive && creature.Monster is LittleRedRidingHoodedMercenary);
    }

    public static Creature? FindWolf(CombatStateLike? combatState)
    {
        return combatState?.Creatures.FirstOrDefault(creature =>
            creature.IsAlive && creature.Monster is WolfInHerNightmares);
    }

    public static IReadOnlyList<Creature> GetIndiscriminateTargets(Creature owner)
    {
        if (owner.CombatState == null)
        {
            return [];
        }

        return owner.CombatState.Creatures
            .Where(creature => creature != owner
                && creature.IsAlive
                && (creature.IsPlayer || creature.Monster is LittleRedRidingHoodedMercenary or WolfInHerNightmares))
            .ToArray();
    }

    public static Creature? GetIndiscriminatePreviewTarget(Creature owner)
    {
        if (owner.CombatState == null)
        {
            return null;
        }

        IReadOnlyList<Creature> targets = GetIndiscriminateTargets(owner);
        return targets.FirstOrDefault(creature => creature.IsPlayer)
            ?? targets.FirstOrDefault();
    }

    public static async Task CheckSpecialDeathOutcome(
        PlayerChoiceContext choiceContext,
        Creature deadCreature,
        Creature? dealer,
        bool wasRemovalPrevented)
    {
        if (wasRemovalPrevented
            || CombatManager.Instance.IsOverOrEnding
            || deadCreature.CombatState == null
            || !IsLittleRedEncounter(deadCreature.CombatState))
        {
            return;
        }

        if (deadCreature.Monster is WolfInHerNightmares)
        {
            await OnWolfDied(choiceContext, deadCreature, dealer);
            return;
        }

        if (deadCreature.Monster is LittleRedRidingHoodedMercenary)
        {
            await OnLittleRedDied(choiceContext, deadCreature);
        }
    }

    public static async Task TriggerVictoryByLittleRed(Creature wolf)
    {
        if (wolf.CombatState == null || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await CleanupRemainingEnemiesAndWin(wolf.CombatState, except: null);
    }

    private static async Task OnWolfDied(PlayerChoiceContext choiceContext, Creature wolf, Creature? dealer)
    {
        CombatStateLike? combatState = wolf.CombatState;
        if (combatState == null)
        {
            return;
        }

        Creature? red = FindLittleRed(combatState);
        if (red == null)
        {
            await CombatManager.Instance.CheckWinCondition();
            return;
        }

        if (dealer?.Monster is LittleRedRidingHoodedMercenary)
        {
            await CleanupRemainingEnemiesAndWin(combatState, except: null);
            return;
        }

        if (red.Monster is LittleRedRidingHoodedMercenary littleRed)
        {
            await littleRed.EnterUnrelievedAnger(choiceContext);
        }
    }

    private static async Task OnLittleRedDied(PlayerChoiceContext choiceContext, Creature deadLittleRed)
    {
        CombatStateLike? combatState = deadLittleRed.CombatState;
        if (combatState == null)
        {
            return;
        }

        Creature? wolf = FindWolf(combatState);
        if (wolf == null)
        {
            return;
        }

        if (wolf.Monster is WolfInHerNightmares wolfModel)
        {
            await wolfModel.TriggerLittleRedDeathFinale();
        }
    }

    private static async Task CleanupRemainingEnemiesAndWin(CombatStateLike combatState, Creature? except)
    {
        foreach (Creature enemy in combatState.Enemies.ToArray())
        {
            if (enemy == except || enemy.IsDead)
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

    public static bool TryAddLittleRedPageReward(AbstractRoom? room, Player player, ICollection<Reward> rewards)
    {
        if (room is not CombatRoom combatRoom || !IsLittleRedEncounter(combatRoom.CombatState))
        {
            return false;
        }

        if (!AbnormalityPageRewardHelper.ShouldAddPageReward<LittleRedMercenaryPageRelic>(
            combatRoom,
            player,
            rewards,
            LittleRedMercenaryPageRelicTitleLocKey))
        {
            return false;
        }

        rewards.Add(new RelicReward(ModelDb.Relic<LittleRedMercenaryPageRelic>().ToMutable(), player));
        Log.Info("[LittleRedMercenaryRewards] Added Little Red page reward for player " + player.NetId + ".");
        return true;
    }
}

public sealed class LittleRedNightmareEndPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "LITTLE_RED_NIGHTMARE_END_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("FinaleDamagePercent", WolfInHerNightmares.LittleRedFinaleDamagePercent)
    ];

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        Creature? dealer = LittleRedDeathContext.Consume(creature);
        await LittleRedMercenaryEncounterHelper.CheckSpecialDeathOutcome(choiceContext, creature, dealer, wasRemovalPrevented);
    }
}

internal static class LittleRedDeathContext
{
    // 致死者按死亡生物弱键存放：没有走到 AfterDeath 的条目不会把整场战斗留在内存里，离开本局时也会清空。
    private static readonly CombatScoped<Creature, Creature?> DealersByDeadCreature = new();

    public static void Clear()
    {
        DealersByDeadCreature.Clear();
    }

    public static void Record(Creature deadCreature, Creature? dealer)
    {
        DealersByDeadCreature.Set(deadCreature, dealer);
    }

    public static Creature? Consume(Creature deadCreature)
    {
        if (!DealersByDeadCreature.TryGetValue(deadCreature, out Creature? dealer))
        {
            return null;
        }

        DealersByDeadCreature.Remove(deadCreature);
        return dealer;
    }
}

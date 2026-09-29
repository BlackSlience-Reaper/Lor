using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.monsters.BurrowingHeaven;
using LibraryOfRuina.powers.BurrowingHeaven;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.encounters.BurrowingHeaven;

internal static class BurrowingHeavenEncounterHelper
{
    public const int SleepTurns = 3;
    public const int CounterReflectPercent = 100;
    public const int FriendlyGazeDamagePercent = 10;

    public static readonly string[] ThornSlots =
    [
        BurrowingHeavenWeak.LeftThornSlot,
        BurrowingHeavenWeak.RightThornSlot
    ];

    public static monsters.BurrowingHeaven.BurrowingHeaven? GetBoss(CombatStateLike? combatState)
    {
        return combatState?.Enemies
            .Select(static creature => creature.Monster)
            .OfType<monsters.BurrowingHeaven.BurrowingHeaven>()
            .FirstOrDefault(static boss => boss.Creature.IsAlive);
    }

    public static IReadOnlyList<HeavenThorn> LivingThorns(CombatStateLike? combatState)
    {
        return combatState?.Enemies
            .Select(static creature => creature.Monster)
            .OfType<HeavenThorn>()
            .Where(static thorn => thorn.Creature.IsAlive)
            .OrderBy(static thorn => GetThornSlotOrder(thorn.Creature.SlotName))
            .ThenBy(static thorn => thorn.Creature.CombatId ?? 0u)
            .ToArray()
            ?? [];
    }

    public static IReadOnlyList<HeavenThorn> AwakeThorns(CombatStateLike? combatState)
    {
        return LivingThorns(combatState)
            .Where(static thorn => thorn.IsAwake)
            .ToArray();
    }

    public static bool HasAwakeThorn(CombatStateLike? combatState) =>
        AwakeThorns(combatState).Count > 0;

    public static bool HasLivingThorn(CombatStateLike? combatState) =>
        LivingThorns(combatState).Count > 0;

    public static bool IsBossStaggered(CombatStateLike? combatState)
    {
        monsters.BurrowingHeaven.BurrowingHeaven? boss = GetBoss(combatState);
        return boss == null || boss.Creature.IsDead || boss.Creature is LibraryLib.Entities.Creatures.LibraryCreature { IsChaoed: true };
    }

    public static bool IsCounterReflectActive(CombatStateLike? combatState)
    {
        monsters.BurrowingHeaven.BurrowingHeaven? boss = GetBoss(combatState);
        return boss != null
            && boss.Creature.IsAlive
            && boss.Creature.HasPower<BurrowingHeavenWingsTowardOldGodPassivePower>();
    }

    public static async Task RefreshEncounterState(
        PlayerChoiceContext choiceContext,
        CombatStateLike? combatState)
    {
        monsters.BurrowingHeaven.BurrowingHeaven? boss = GetBoss(combatState);
        if (boss == null)
        {
            return;
        }

        bool hasAwakeThorn = HasAwakeThorn(combatState);
        bool hasLivingThorn = HasLivingThorn(combatState);
        await boss.SetAwake(
            choiceContext,
            awake: !hasAwakeThorn,
            forceExclusiveHeaven: !hasAwakeThorn && !hasLivingThorn);

        await EnsureSleepPowers(choiceContext, combatState, boss);
    }

    public static async Task EnsureSleepPowers(
        PlayerChoiceContext choiceContext,
        CombatStateLike? combatState,
        monsters.BurrowingHeaven.BurrowingHeaven? boss = null)
    {
        boss ??= GetBoss(combatState);
        if (boss == null || boss.Creature.IsDead)
        {
            return;
        }

        IReadOnlyList<HeavenThorn> livingThorns = LivingThorns(combatState);
        IReadOnlyList<HeavenThorn> dormantThorns = livingThorns
            .Where(static thorn => !thorn.IsAwake)
            .ToArray();

        if (dormantThorns.Count > 0)
        {
            await RemoveBossSleepPower(boss);
            if (dormantThorns.All(static thorn => !thorn.Creature.HasPower<HeavenThornSleepPower>()))
            {
                await PowerCmdCompat.Apply<HeavenThornSleepPower>(
                    choiceContext,
                    dormantThorns[0].Creature,
                    SleepTurns,
                    boss.Creature,
                    null,
                    silent: true);
            }

            return;
        }

        BurrowingHeavenSleepPower? bossSleep = boss.Creature.GetPower<BurrowingHeavenSleepPower>();
        if (bossSleep == null)
        {
            await PowerCmdCompat.Apply<BurrowingHeavenSleepPower>(
                choiceContext,
                boss.Creature,
                SleepTurns,
                boss.Creature,
                null,
                silent: true);
        }
    }

    public static async Task SpawnOrWakeAwakeThorn(
        PlayerChoiceContext choiceContext,
        CombatStateLike? combatState)
    {
        monsters.BurrowingHeaven.BurrowingHeaven? boss = GetBoss(combatState);
        if (boss == null || boss.Creature.IsDead || combatState == null)
        {
            return;
        }

        IReadOnlyList<HeavenThorn> livingThorns = LivingThorns(combatState);
        if (livingThorns.Count >= ThornSlots.Length)
        {
            return;
        }

        HeavenThorn? dormant = livingThorns.FirstOrDefault(static thorn => !thorn.IsAwake);
        if (dormant != null)
        {
            await dormant.WakeFromSleep(choiceContext);
            await RefreshEncounterState(choiceContext, combatState);
            return;
        }

        string? slot = ThornSlots.FirstOrDefault(slotName =>
            livingThorns.All(thorn => thorn.Creature.SlotName != slotName));
        if (slot == null)
        {
            return;
        }

        var thorn = (HeavenThorn)ModelDb.Monster<HeavenThorn>().ToMutable();
        thorn.SetInitialAwake(true);
        Creature spawned = await CreatureCmd.Add(thorn, combatState, CombatSide.Enemy, slot);
        if (spawned.Monster is HeavenThorn spawnedThorn)
        {
            await spawnedThorn.WakeFromSleep(choiceContext);
        }
        else
        {
            spawned.Monster?.RollMove(combatState.PlayerCreatures);
        }

        await RefreshEncounterState(choiceContext, combatState);
    }

    public static bool IsStageThornPassiveController(Creature owner)
    {
        if (owner.Monster is not HeavenThorn { IsAwake: true } || owner.IsDead)
        {
            return false;
        }

        return ReferenceEquals(AwakeThorns(owner.CombatState).FirstOrDefault()?.Creature, owner);
    }

    public static async Task ReflectFullyBlockedCounter(
        PlayerChoiceContext choiceContext,
        Creature counterAttacker,
        IEnumerable<DamageResult> results,
        int reflectPercent)
    {
        if (counterAttacker.IsDead || !IsCounterReflectActive(counterAttacker.CombatState))
        {
            return;
        }

        IReadOnlyList<DamageResult> fullyBlocked = results
            .Where(static result => result.WasFullyBlocked && result.BlockedDamage > 0)
            .ToArray();
        if (fullyBlocked.Count == 0)
        {
            return;
        }

        int damage = fullyBlocked.Sum(result =>
            Math.Max(1, (int)Math.Ceiling(result.BlockedDamage * reflectPercent / 100m)));
        Creature? dealer = fullyBlocked[0].Receiver;

        await LibraryCreatureCmd.ChaoDamage(
            choiceContext,
            [counterAttacker],
            damage,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            dealer,
            null,
            null,
            LibraryDamageType.Blunt);
    }

    public static void TryPlayCounterHitVfx(Creature? target, string path, string source)
    {
        if (target == null || target.IsDead)
        {
            return;
        }

        try
        {
            VfxCmd.PlayOnCreatureCenter(target, path);
        }
        catch (NullReferenceException exception)
        {
            Log.Warn(
                "[BurrowingHeavenCounterVfx] skipped hit VFX source="
                + source
                + " target="
                + (target.CombatId?.ToString() ?? "unknown")
                + " reason="
                + exception.GetType().Name
                + ": "
                + exception.Message);
        }
        catch (ObjectDisposedException exception)
        {
            Log.Warn(
                "[BurrowingHeavenCounterVfx] skipped hit VFX source="
                + source
                + " target="
                + (target.CombatId?.ToString() ?? "unknown")
                + " reason="
                + exception.GetType().Name
                + ": "
                + exception.Message);
        }
    }

    public static IReadOnlyList<Creature> LivingPlayers(CombatStateLike? combatState)
    {
        return combatState?.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .ToArray()
            ?? [];
    }

    public static string PickScreamSfx(Creature owner)
    {
        IReadOnlyList<string> paths = monsters.BurrowingHeaven.BurrowingHeaven.ScreamSfxPaths;
        int round = Math.Max(0, owner.CombatState?.RoundNumber ?? 0);
        return paths[round % paths.Count];
    }

    private static async Task RemoveBossSleepPower(monsters.BurrowingHeaven.BurrowingHeaven boss)
    {
        BurrowingHeavenSleepPower? bossSleep = boss.Creature.GetPower<BurrowingHeavenSleepPower>();
        if (bossSleep != null)
        {
            await PowerCmd.Remove(bossSleep);
        }
    }

    private static int GetThornSlotOrder(string? slotName)
    {
        for (int i = 0; i < ThornSlots.Length; i++)
        {
            if (string.Equals(ThornSlots[i], slotName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return ThornSlots.Length;
    }
}

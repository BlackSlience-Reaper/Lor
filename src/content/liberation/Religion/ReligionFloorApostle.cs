using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.content.liberation.Religion;

public abstract class ReligionFloorApostle : ReligionFloorMonster
{
    public override float DeathAnimLengthOverride => LayeredBossSpine.DeathLength(this);

    [SavedProperty]
    public bool IsFakeDead { get; private set; }

    [SavedProperty]
    public int WakeRound { get; private set; } = -1;

    internal abstract int ApostleIndex { get; }

    internal abstract int MoveWeight(int move);

    public override bool ShouldFadeAfterDeath => false;

    internal bool IsAvailable => Creature.IsAlive && !IsFakeDead && !Creature.IsStunned;

    internal bool CanActThisTurn => IsAvailable && WakeRound != Creature.CombatState?.RoundNumber;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Ensure<MinionPower>(Creature);
        if (this is ReligionFloorScytheApostle && Encounter is { Initialized: false })
        {
            await EnterFalseDeath(countKill: false);
        }
    }

    internal async Task EnterFalseDeath(bool countKill = true)
    {
        if (IsFakeDead || Encounter is not { IsSettling: false, Completed: false } encounter)
        {
            return;
        }

        IsFakeDead = true;
        if (Creature is LibraryCreature library)
        {
            library.RestorePreStunResistance();
            library.SetCurrentChaoValueInternal(0);
            library.RestoreChaoOnNextOwnerTurn = false;
        }

        PlanMove(WaitMove, rollDamage: false);
        // 复用原版死亡保留与 SetCurrentHp 复活流程，假死期间生命为零。
        await CreatureCmd.SetCurrentHp(Creature, ReligionFloorRules.ApostleFakeDeathHp);
        if (countKill)
        {
            encounter.RecordApostleKill();
        }

        encounter.RepairApostlePlans();
    }

    internal async Task Wake()
    {
        IsFakeDead = false;
        WakeRound = Creature.CombatState?.RoundNumber ?? -1;
        await PowerCmdCompat.RemoveIfPresent<ReligionFloorFalseDeathPower>(Creature);
        if (Creature is LibraryCreature library)
        {
            library.RestorePreStunResistance();
            library.SetCurrentChaoValueInternal(library.MaxChaoValue);
        }

        await CreatureCmd.SetCurrentHp(Creature, Creature.MaxHp);
        await PowerCmdCompat.Ensure<MinionPower>(Creature);
        // 清除旧的 STUNNED MoveState，随后再让共享调度器按其余使徒的意图安排新招。
        PlanMove(WaitMove, rollDamage: false);
        Encounter?.RepairApostlePlans(this);
        await CreatureCmd.TriggerAnim(Creature, "Wake", ReligionFloorRules.FrameSeconds);
    }

    internal static LibraryCreatureResistanceData.Resistance Resistance(LibraryDamageType weak, bool chaos)
    {
        var resistance = new LibraryCreatureResistanceData.Resistance(
            chaos ? LibraryResistanceLevel.Endure : LibraryResistanceLevel.Resist);
        LibraryResistanceLevel vulnerability = chaos ? LibraryResistanceLevel.Fatal : LibraryResistanceLevel.Vulnerable;
        switch (weak)
        {
            case LibraryDamageType.Slash:
                resistance.Slash = vulnerability;
                break;
            case LibraryDamageType.Pierce:
                resistance.Pierce = vulnerability;
                break;
            case LibraryDamageType.Blunt:
                resistance.Blunt = vulnerability;
                break;
        }

        return resistance;
    }
}

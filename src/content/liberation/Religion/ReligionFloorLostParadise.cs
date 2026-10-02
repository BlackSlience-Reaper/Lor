using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.content.guests.DawnOffice;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Religion;

public sealed class ReligionFloorLostParadise : ReligionFloorMonster, IEncounterDynamicBgmTrackSource
{
    internal override string AssetName => "lost_paradise";

    public override int MinInitialHp => ReligionFloorRules.ParadiseHp;

    public override int MaxInitialHp => ReligionFloorRules.ParadiseHp;

    public override int DefaultChaoResistance => ReligionFloorRules.ParadiseChao;

    public override LibraryCreatureResistanceData.Resistance DefaultPhysicalResistanceData => new(LibraryResistanceLevel.Endure);

    public override LibraryCreatureResistanceData.Resistance DefaultChaoResistanceData => new(LibraryResistanceLevel.Endure);

    public override bool ShouldFadeAfterDeath => false;

    public int CurrentEncounterBgmTrackIndex => Encounter?.MusicIndex ?? 0;

    // 我是你的救主：场上没有其他未假死敌人时唤醒更多使徒，且不超过当前假死使徒数。
    internal int SaviorWakeCount
    {
        get
        {
            if (Encounter is not { } encounter)
            {
                return ReligionFloorRules.SaviorWakeCount;
            }

            bool hasOtherEnemies = encounter.Apostles.Any(static apostle => apostle.Creature.IsAlive && !apostle.IsFakeDead);
            int wakeCount = hasOtherEnemies
                ? ReligionFloorRules.SaviorWakeCount
                : ReligionFloorRules.SaviorLoneWakeCount;
            int fakeDeadCount = encounter.Apostles.Count(static apostle => apostle.IsFakeDead);
            return fakeDeadCount > 0 ? Math.Min(wakeCount, fakeDeadCount) : wakeCount;
        }
    }

    internal override (int Min, int Max, int Hits) AttackValues(int move) =>
        move == 2 ? (ReligionFloorRules.SaviorDamageMin, ReligionFloorRules.SaviorDamageMax, ReligionFloorRules.SaviorHits) : (0, 0, 0);

    protected override IEnumerable<AbstractIntent> CreateIntents(int move) => move switch
    {
        0 => [new ReligionFloorEffectIntent("RELIGION_WELCOME", ReligionFloorRules.WelcomeBlock, ReligionEffectKind.Defend), new BuffIntent()],
        1 => [new ReligionFloorEffectIntent("RELIGION_AWE", ReligionFloorRules.AweCards, ReligionEffectKind.CardDebuff)],
        2 => [AttackIntent(move), new ReligionFloorEffectIntent("RELIGION_WAKE", () => SaviorWakeCount, ReligionEffectKind.Summon)],
        SalvationMove => [new ReligionFloorEffectIntent("RELIGION_SALVATION", ReligionFloorRules.CrownProtection, ReligionEffectKind.Buff)],
        ExplosionMove => [new ReligionFloorAttackIntent(() => Encounter?.ExplosionDamage ?? 0, () => 1)],
        _ => [new StunIntent()]
    };

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (Encounter is { } encounter && Creature.CombatState is { } state)
        {
            encounter.Initialize(state);
            await encounter.EnsurePhasePowers();
        }
    }

    public override Task AfterStun(Creature creature)
    {
        if (creature.Monster is ReligionFloorApostle)
        {
            Encounter?.RepairApostlePlans();
        }
        return base.AfterStun(creature);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Encounter?.CleanupPresentation();
        return base.AfterCombatEnd(room);
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        Encounter?.OnCardMoved(card);
        return base.AfterCardChangedPiles(card, oldPileType, clonedBy);
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike state)
    {
        await base.BeforeSideTurnStart(context, side, participants, state);
        if (Encounter is { } encounter)
        {
            await encounter.BeforeTurn(context, side, state);
        }
    }

    public override async Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEndLate(context, side, participants);
        if (side == CombatSide.Enemy && Encounter is { } encounter)
        {
            await encounter.EndEnemyTurn(context);
        }
    }

    protected override async Task PerformReligionMove(int move, IReadOnlyList<Creature> targets)
    {
        if (Encounter is not { } encounter)
        {
            return;
        }

        if (move == 2)
        {
            int wakeCount = SaviorWakeCount;
            await AttackPlayers(move, targets, "Attack");
            List<ReligionFloorApostle> dead = encounter.Apostles.Where(static apostle => apostle.IsFakeDead).ToList();
            for (int woken = 0; woken < wakeCount && dead.Count > 0; woken++)
            {
                int index = RunRng.MonsterAi.NextInt(dead.Count);
                ReligionFloorApostle apostle = dead[index];
                dead.RemoveAt(index);
                await apostle.Wake();
            }
            return;
        }

        await CreatureCmd.TriggerAnim(Creature, "Cast", ReligionFloorRules.FrameSeconds);
        if (move == 0)
        {
            foreach (Creature enemy in Creature.CombatState!.Enemies.Where(static enemy => enemy.IsAlive))
            {
                if (enemy.Monster is ReligionFloorApostle { IsFakeDead: true })
                {
                    continue;
                }

                await CreatureCmd.GainBlock(enemy, ReligionFloorRules.WelcomeBlock, ValueProp.Move, null);
                await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(enemy, ReligionFloorRules.WelcomeStrength, Creature, null);
            }
        }
        else
        {
            foreach (Creature target in targets)
            {
                await PowerCmdCompat.Apply<ReligionFloorAwePower>(target, ReligionFloorRules.AweCards, Creature, null);
            }
        }
    }
}

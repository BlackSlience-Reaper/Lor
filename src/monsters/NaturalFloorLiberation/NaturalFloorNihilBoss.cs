using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.NaturalFloorLiberation;

public sealed class NaturalFloorNihilBoss : NaturalFloorNihilMonster, ILiberationPrimaryPhaseBoss
{
    public NaturalFloorNihilForm Form { get; private set; }

    public int PendingForm { get; private set; } = -1;

    public int CompletedFormTurns { get; private set; }

    public int NormalMoveIndex { get; private set; }

    public int HatredHitCount { get; private set; }

    public int SealedSwords { get; private set; }

    public bool WrathStaggered { get; private set; }

    public bool NihilApplied { get; private set; }

    public bool GreedGroupPending { get; private set; }

    public int LiberationPhase => 5;

    public Task TriggerReviveAndEmpowerState() => Task.CompletedTask;

    public void ForceReviveAndEmpowerState()
    {
    }

    internal override string VisualId => "boss";

    protected override IEnumerable<NaturalFloorNihilAction> AvailableActions =>
        Enum.GetValues<NaturalFloorNihilAction>().Where(action => action <= NaturalFloorNihilAction.WrathIncarnation);

    public override int MinInitialHp => NaturalFloorNihilMoves.BossHp;

    public override int MaxInitialHp => NaturalFloorNihilMoves.BossHp;

    public override int DefaultChaoResistance => NaturalFloorNihilMoves.BossChaos;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new(LibraryResistanceLevel.Resist);

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new(LibraryResistanceLevel.Fatal);

    internal int HatredThreshold =>
        NaturalFloorNihilMoves.HatredHitThreshold(Creature?.CombatState?.Players.Count ?? 1);

    private int FormHpLossPercent => NaturalFloorNihilMoves.TransitionHpLossPercent(Form);

    internal NaturalFloorGirlKind? VulnerableGirl => Form switch
    {
        NaturalFloorNihilForm.Greed => NaturalFloorGirlKind.Happiness,
        NaturalFloorNihilForm.Hatred => NaturalFloorGirlKind.Love,
        NaturalFloorNihilForm.Despair => NaturalFloorGirlKind.Justice,
        NaturalFloorNihilForm.Wrath => NaturalFloorGirlKind.Courage,
        _ => null
    };

    protected override IEnumerable<(NaturalFloorNihilAction Action, string Target)> PlanActions()
    {
        if (PendingForm >= 0)
        {
            yield break;
        }

        int turn = CompletedFormTurns + 1;
        switch (Form)
        {
            case NaturalFloorNihilForm.Nihil:
                yield return (NaturalFloorNihilAction.NihilWill, "PLAYERS");
                break;
            case NaturalFloorNihilForm.Greed:
                if (GreedGroupPending)
                {
                    yield return (NaturalFloorNihilAction.TyrantPath, "ALL");
                    break;
                }

                var remaining = new System.Collections.Generic.List<NaturalFloorNihilAction>
                {
                    NaturalFloorNihilAction.Hunger, NaturalFloorNihilAction.Gluttony,
                    NaturalFloorNihilAction.Craving, NaturalFloorNihilAction.Obsession
                };
                foreach (NaturalFloorGirlKind kind in Enum.GetValues<NaturalFloorGirlKind>())
                {
                    int selected = RunRng.MonsterAi.NextInt(remaining.Count);
                    NaturalFloorNihilAction action = remaining[selected];
                    remaining.RemoveAt(selected);
                    yield return (action, PlanGirlTarget(kind));
                }

                NaturalFloorNihilAction[] playerMoves =
                [NaturalFloorNihilAction.Hunger, NaturalFloorNihilAction.Gluttony, NaturalFloorNihilAction.Craving, NaturalFloorNihilAction.Obsession];
                yield return (playerMoves[RunRng.MonsterAi.NextInt(playerMoves.Length)], PlanPlayerTarget());
                break;
            case NaturalFloorNihilForm.Hatred:
                if (CompletedFormTurns == 0)
                {
                    yield return (NaturalFloorNihilAction.Hatred, PlanGirlTarget(NaturalFloorGirlKind.Love));
                }
                else if (HatredHitCount >= HatredThreshold)
                {
                    yield return (NaturalFloorNihilAction.BossMagic, "ALL");
                }
                else
                {
                    NaturalFloorNihilAction action = NormalMoveIndex % 2 == 0
                        ? NaturalFloorNihilAction.HatredLight : NaturalFloorNihilAction.LoveAndHate;
                    yield return (action, PlanGirlTarget(NaturalFloorGirlKind.Love));
                }
                break;
            case NaturalFloorNihilForm.Despair:
                NaturalFloorNihilAction[] swords =
                [NaturalFloorNihilAction.HeartPierce, NaturalFloorNihilAction.HeartSplit, NaturalFloorNihilAction.HeartDestroy];
                for (int i = 0; i < swords.Length; i++)
                {
                    if ((SealedSwords & (1 << i)) == 0)
                    {
                        yield return (swords[i], PlanGirlTarget(NaturalFloorGirlKind.Justice));
                    }
                }
                break;
            case NaturalFloorNihilForm.Wrath:
                if (turn % NaturalFloorNihilMoves.WrathGroupInterval == 0)
                {
                    yield return (NaturalFloorNihilAction.WrathIncarnation, "ALL");
                    break;
                }

                NaturalFloorNihilAction[] wrathMoves =
                [NaturalFloorNihilAction.WrathGroan, NaturalFloorNihilAction.WrathCry, NaturalFloorNihilAction.WrathRoar];
                yield return (wrathMoves[NormalMoveIndex % wrathMoves.Length], PlanGirlTarget(NaturalFloorGirlKind.Courage));
                break;
        }
    }

    protected override async Task ApplyPassives()
    {
        await PowerCmdCompat.Ensure<NaturalFloorEverythingIsEmptyPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<NaturalFloorNihilImmunityPower>(Creature, 1, Creature, null, silent: true);
        //await PowerCmdCompat.Ensure<RegenPower>(Creature, 75, Creature, null, true);
        //await PowerCmd.Apply<HardenedShellPower>(new ThrowingPlayerChoiceContext(), Creature, 150, Creature, null,
        //    true);
        await ReplaceFormPassive();
        if (NihilApplied)
        {
            foreach (Creature player in Encounter?.LivingPlayers() ?? [])
            {
                await PowerCmdCompat.Ensure<NaturalFloorNihilPower>(player, 1, Creature, null, silent: true);
            }
        }
    }

    private async Task ReplaceFormPassive()
    {
        foreach (NaturalFloorNihilFormPower old in Creature.Powers.OfType<NaturalFloorNihilFormPower>().ToArray())
        {
            if (old.Form != Form)
            {
                await PowerCmd.Remove(old);
            }
        }

        switch (Form)
        {
            case NaturalFloorNihilForm.Greed:
                await PowerCmdCompat.Ensure<NaturalFloorNihilGreedPower>(Creature, 1, Creature, null, silent: true);
                break;
            case NaturalFloorNihilForm.Hatred:
                await PowerCmdCompat.Ensure<NaturalFloorNihilHatredPower>(Creature, 1, Creature, null, silent: true);
                break;
            case NaturalFloorNihilForm.Despair:
                await PowerCmdCompat.Ensure<NaturalFloorNihilDespairPower>(Creature, 1, Creature, null, silent: true);
                break;
            case NaturalFloorNihilForm.Wrath:
                await PowerCmdCompat.Ensure<NaturalFloorNihilWrathPower>(Creature, 1, Creature, null, silent: true);
                break;
        }
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        await base.AfterDamageGiven(context, dealer, result, props, target, cardSource);
        if (dealer != Creature || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        if (Form == NaturalFloorNihilForm.Hatred && result.UnblockedDamage > 0)
        {
            HatredHitCount++;
            Creature.GetPower<NaturalFloorNihilHatredPower>()?.RefreshCounter();
        }

        if (Form == NaturalFloorNihilForm.Greed && target.IsAlive)
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(target, NaturalFloorNihilMoves.GreedHitBleed, Creature, null);
        }
    }

    protected override void BeforeAction(NaturalFloorNihilAction action)
    {
        if (action == NaturalFloorNihilAction.TyrantPath)
        {
            // 开始结算时消耗本次群攻预告。
            GreedGroupPending = false;
        }
    }

    protected override async Task AfterAction(NaturalFloorNihilAction action, Dictionary<Creature, int> losses)
    {
        if (!CanAct)
        {
            return;
        }

        if (Form == NaturalFloorNihilForm.Greed
            && losses.Count > 0
            && losses.Values.All(loss => loss == 0))
        {
            // 整次攻击没有造成未被格挡的伤害时触发，避免多段攻击重复发放。
            GreedGroupPending = true;
            if (Encounter is { } encounter)
            {
                await encounter.GrantNihilGreedReward();
            }
        }

        switch (action)
        {
            case NaturalFloorNihilAction.NihilWill:
                NihilApplied = true;
                PendingForm = (int)NaturalFloorNihilForm.Greed;
                break;
            case NaturalFloorNihilAction.TyrantPath:
                if (losses.Count > 0 && losses.Values.All(loss => loss == 0))
                {
                    await QueueForm(NaturalFloorNihilForm.Hatred);
                }
                else if (losses.Count(entry => entry.Key.IsPlayer && entry.Value > 0) >= NaturalFloorNihilMoves.TyrantPlayerHitThreshold)
                {
                    await CreatureCmd.Heal(Creature, Creature.MaxHp * NaturalFloorNihilMoves.TyrantHealPercent / 100m);
                }
                break;
            case NaturalFloorNihilAction.BossMagic:
                HatredHitCount = 0;
                Creature.GetPower<NaturalFloorNihilHatredPower>()?.RefreshCounter();
                break;
            case NaturalFloorNihilAction.HatredLight:
            case NaturalFloorNihilAction.LoveAndHate:
            case NaturalFloorNihilAction.WrathGroan:
            case NaturalFloorNihilAction.WrathCry:
            case NaturalFloorNihilAction.WrathRoar:
                NormalMoveIndex++;
                break;
            case NaturalFloorNihilAction.HeartPierce:
            case NaturalFloorNihilAction.HeartSplit:
            case NaturalFloorNihilAction.HeartDestroy:
                if (losses.Count > 0 && losses.Values.All(loss => loss == 0))
                {
                    SealedSwords |= 1 << ((int)action - (int)NaturalFloorNihilAction.HeartPierce);
                }
                break;
        }
    }

    protected override async Task AfterTurnPerformed()
    {
        CompletedFormTurns++;
        if (CanAct && Form == NaturalFloorNihilForm.Despair && SealedSwords == (1 << NaturalFloorNihilMoves.SealedSwordCount) - 1)
        {
            await QueueForm(NaturalFloorNihilForm.Wrath);
        }
    }

    internal async Task QueueForm(NaturalFloorNihilForm next)
    {
        if (!CanAct || PendingForm >= 0)
        {
            return;
        }

        PendingForm = (int)next;
        await LoseLifePercent(Creature, FormHpLossPercent);
    }

    internal async Task BeginPlayerRound()
    {
        if (!CanAct)
        {
            return;
        }

        if (WrathStaggered && Creature is LibraryCreature { IsChaoed: false })
        {
            PendingForm = (int)NaturalFloorNihilForm.Nihil;
        }

        if (PendingForm < 0)
        {
            return;
        }

        Form = (NaturalFloorNihilForm)PendingForm;
        PendingForm = -1;
        CompletedFormTurns = 0;
        NormalMoveIndex = 0;
        HatredHitCount = 0;
        SealedSwords = 0;
        WrathStaggered = false;
        GreedGroupPending = false;
        await ReplaceFormPassive();
        RefreshPlannedTurn(replacePlan: true);
        await NaturalFloorNihilTransition.PlayAsync(Form);
        await CreatureCmd.TriggerAnim(Creature, "Idle", 0f);
    }

    public override async Task AfterStun(Creature creature)
    {
        await base.AfterStun(creature);
        if (creature == Creature && Form == NaturalFloorNihilForm.Wrath && !WrathStaggered)
        {
            WrathStaggered = true;
            await LoseLifePercent(Creature, NaturalFloorNihilMoves.WrathStaggerHpLossPercent);
        }
    }

    public override async Task AfterDeath(PlayerChoiceContext context, Creature creature, bool prevented, float deathAnimLength)
    {
        await base.AfterDeath(context, creature, prevented, deathAnimLength);
        if (creature == Creature && !prevented && Encounter is { } encounter)
        {
            await encounter.CompleteNihilPhase();
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        NaturalFloorNihilTransition.Cleanup();
        return base.AfterCombatEnd(room);
    }

    public override void BeforeRemovedFromRoom()
    {
        NaturalFloorNihilTransition.Cleanup();
        base.BeforeRemovedFromRoom();
    }
}

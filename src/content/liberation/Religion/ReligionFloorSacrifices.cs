using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.content.liberation.Religion;

public sealed partial class ReligionFloorLiberationEncounter
{
    private sealed record HeldCard(CardModel Card, int Sequence, int Expiry, bool Ready, int Wing);
    private List<HeldCard> _sacrifices = [];

    // 每条依次保存战斗卡牌编号、全局序号、到期敌方回合、完成结算标记、翼节点编号；原牌保留在 PlayPile。
    [SavedProperty]
    public int[] SacrificeRecords { get; private set; } = [];

    [SavedProperty]
    public int NextSacrificeSequence { get; private set; }

    // 使用百分比的分子累计，避免每个伤害段分别取整导致阈值漂移。
    [SavedProperty]
    public int ReclaimProgress { get; private set; }

    private void CloneSacrificeState()
    {
        _sacrifices = [];
        SacrificeRecords = [.. SacrificeRecords];
    }

    internal void ReserveSacrifice(CardModel card)
    {
        if (_sacrifices.Any(entry => entry.Card == card) || Completed || IsSettling)
        {
            return;
        }
        HeldCard[] owned = _sacrifices.Where(entry => entry.Card.Owner == card.Owner).OrderBy(static entry => entry.Sequence).ToArray();
        int start = owned.Length > 0 ? (owned[^1].Wing + 1) % ReligionFloorRules.AweCards : 0;
        int wing = Enumerable.Range(0, ReligionFloorRules.AweCards)
            .Select(offset => (start + offset) % ReligionFloorRules.AweCards)
            .First(index => owned.All(entry => entry.Wing != index));
        _sacrifices.Add(new HeldCard(card, NextSacrificeSequence++, EnemyTurnsCompleted + 1, false, wing));
        PublishSacrificeState();
    }

    internal void FinishSacrificePlay(CardModel card)
    {
        int index = _sacrifices.FindIndex(entry => entry.Card == card);
        if (index < 0)
        {
            return;
        }
        if (card.Pile?.Type != PileType.Play || card.Owner.Creature.IsDead || Completed || IsSettling)
        {
            _sacrifices.RemoveAt(index);
        }
        else
        {
            _sacrifices[index] = _sacrifices[index] with { Ready = true };
            ReligionFloorPresentation.HoldCard(this, card);
        }
        PublishSacrificeState();
    }

    internal IReadOnlyList<CardModel> HeldCards => _sacrifices.Where(static entry => entry.Ready).OrderBy(static entry => entry.Sequence).Select(static entry => entry.Card).ToArray();

    internal int WingFor(CardModel card) => _sacrifices.First(entry => entry.Card == card).Wing;

    internal void OnCardMoved(CardModel card)
    {
        if (card.Pile?.Type == PileType.Play)
        {
            return;
        }
        if (_sacrifices.RemoveAll(entry => entry.Card == card) > 0)
        {
            ReligionFloorPresentation.ReleaseCard(card);
            PublishSacrificeState();
        }
    }

    private void PruneSacrifices()
    {
        foreach (HeldCard entry in _sacrifices.Where(static entry => entry.Card.Pile?.Type != PileType.Play || entry.Card.Owner.Creature.IsDead).ToArray())
        {
            _sacrifices.Remove(entry);
            ReligionFloorPresentation.ReleaseCard(entry.Card);
        }
        PublishSacrificeState();
    }

    private void PublishSacrificeState()
    {
        SacrificeRecords = _sacrifices.OrderBy(static entry => entry.Sequence).SelectMany(entry => new[]
        {
            checked((int)NetCombatCard.FromModel(entry.Card).CombatCardIndex), entry.Sequence, entry.Expiry, entry.Ready ? 1 : 0, entry.Wing
        }).ToArray();
    }

    private void ResetReclaimProgress()
    {
        ReclaimProgress = 0;
        PruneSacrifices();
    }

    private void RestoreSacrificeReferences()
    {
        if (_sacrifices.Count > 0 || SacrificeRecords.Length == 0)
        {
            return;
        }
        for (int index = 0; index + 4 < SacrificeRecords.Length; index += 5)
        {
            if (NetCombatCardDb.Instance.TryGetCard((uint)SacrificeRecords[index], out CardModel? card)
                && card is { Pile.Type: PileType.Play })
            {
                _sacrifices.Add(new HeldCard(card, SacrificeRecords[index + 1], SacrificeRecords[index + 2], SacrificeRecords[index + 3] != 0, SacrificeRecords[index + 4]));
            }
        }
        foreach (HeldCard entry in _sacrifices.Where(static entry => entry.Ready))
        {
            ReligionFloorPresentation.HoldCard(this, entry.Card);
        }
    }

    internal async Task RecordBossHpLoss(decimal amount)
    {
        if (_combat?.CurrentSide != CombatSide.Player || Boss is not { } boss || amount <= 0 || IsSettling || Completed)
        {
            return;
        }
        PruneSacrifices();
        ReclaimProgress += decimal.ToInt32(amount) * 100;
        int threshold = boss.Creature.MaxHp * ReclaimPercent;
        while (ReclaimProgress >= threshold)
        {
            ReclaimProgress -= threshold;
            foreach (var player in _combat.Players)
            {
                HeldCard? entry = _sacrifices.Where(entry => entry.Ready && entry.Card.Owner == player).OrderBy(static entry => entry.Sequence).FirstOrDefault();
                if (entry == null)
                {
                    continue;
                }
                _sacrifices.Remove(entry);
                ReligionFloorPresentation.ReleaseCard(entry.Card);
                await CardPileCmd.Add(entry.Card, PileType.Hand, CardPilePosition.Bottom, boss);
            }
        }
        PublishSacrificeState();
    }

    private async Task ExhaustSacrifices(PlayerChoiceContext context)
    {
        PruneSacrifices();
        if (Boss is not { } boss)
        {
            return;
        }
        foreach (HeldCard entry in _sacrifices.Where(entry => entry.Ready && entry.Expiry <= EnemyTurnsCompleted).OrderBy(static entry => entry.Sequence).ToArray())
        {
            _sacrifices.Remove(entry);
            ReligionFloorPresentation.ReleaseCard(entry.Card);
            if (entry.Card.Pile?.Type != PileType.Play || entry.Card.Owner.Creature.IsDead)
            {
                continue;
            }
            await CardCmd.Exhaust(context, entry.Card, causedByEthereal: false);
            if (entry.Card.Pile?.Type == PileType.Exhaust && boss.Creature.IsAlive && !IsSettling)
            {
                await CreatureCmd.Heal(boss.Creature, Math.Ceiling(boss.Creature.MaxHp * ReligionFloorRules.SacrificeHealPercent / 100m));
            }
        }
        PublishSacrificeState();
    }

    private void ClearSacrifices()
    {
        foreach (HeldCard entry in _sacrifices)
        {
            ReligionFloorPresentation.ReleaseCard(entry.Card);
        }
        _sacrifices.Clear();
        SacrificeRecords = [];
        ReclaimProgress = 0;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
[LibraryPatch(Reason = "AfterCardPlayed 在原版最终去向结算之前触发；敬畏须等 Play 完整结束后才将保留在 PlayPile 的同一张牌交给翼节点。")]
internal static class ReligionFloorSacrificePlayPatch
{
    private static void Postfix(CardModel __instance, ref Task __result)
    {
        if (__instance.Owner.Creature.CombatState?.Encounter is ReligionFloorLiberationEncounter encounter)
        {
            __result = Complete(__result, encounter, __instance);
        }
    }

    private static async Task Complete(Task play, ReligionFloorLiberationEncounter encounter, CardModel card)
    {
        try
        {
            await play;
        }
        finally
        {
            encounter.FinishSacrificePlay(card);
        }
    }
}

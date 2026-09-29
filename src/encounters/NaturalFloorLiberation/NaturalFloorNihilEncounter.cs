using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.events.NaturalFloorLiberation;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using LibraryOfRuina.relics.DespairKnight;
using LibraryOfRuina.relics.KingOfGreed;
using LibraryOfRuina.relics.QueenOfHatred;
using LibraryOfRuina.relics.WrathServant;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.encounters.NaturalFloorLiberation;

public sealed partial class NaturalFloorLiberationEncounter
{
    internal const string NihilBossSlot = "nihil_boss";
    private const float NihilCameraScaling = 0.82f; // 终战：沿用历史层第一阶段的整体缩放。
    private static readonly Vector2 NihilCameraOffset = new(-100f, 50f);
    internal static readonly string[] NihilGirlSlots = ["nihil_love", "nihil_justice", "nihil_happiness", "nihil_courage"];
    internal static readonly string[] NihilStatueSlots =
    ["nihil_love_statue", "nihil_justice_statue", "nihil_happiness_statue", "nihil_courage_statue"];
    private List<NaturalFloorPhaseMonster> _phaseFive = [];
    private bool _entryBranchChosen;
    private bool _nihilReplacingStatues;
    private int _nihilLastPlayerRound = -1;

    public bool NihilCompleted { get; private set; }

    internal NaturalFloorNihilBoss? NihilBoss => _phaseFive.OfType<NaturalFloorNihilBoss>().FirstOrDefault();

    internal static IEnumerable<MonsterModel> NihilPossibleMonsters() =>
    [
        ModelDb.Monster<NaturalFloorNihilBoss>(),
        ModelDb.Monster<NaturalFloorLoveStatue>(), ModelDb.Monster<NaturalFloorJusticeStatue>(),
        ModelDb.Monster<NaturalFloorHappinessStatue>(), ModelDb.Monster<NaturalFloorCourageStatue>(),
        ModelDb.Monster<NaturalFloorLoveGirl>(), ModelDb.Monster<NaturalFloorJusticeGirl>(),
        ModelDb.Monster<NaturalFloorHappinessGirl>(), ModelDb.Monster<NaturalFloorCourageGirl>()
    ];

    internal void ChooseEntryBranch(IRunState runState)
    {
        if (_entryBranchChosen || runState.Players.Count == 0)
        {
            return;
        }

        _entryBranchChosen = true;
        bool hasAllPages = runState.Players.All(player =>
                player.Relics.Any(relic => relic is QueenOfHatredPageRelic)
                && player.Relics.Any(relic => relic is DespairKnightPageRelic)
                && player.Relics.Any(relic => relic is WrathServantPageRelic)
                && player.Relics.Any(relic => relic is KingOfGreedPageRelic));
        if (hasAllPages)
        {
            CurrentPhase = 5;
        }

        Log.Info($"[NaturalFloorLiberation] Entry selected: phase={CurrentPhase}, players={runState.Players.Count}, allFourPages={hasAllPages}.");
    }

    private IReadOnlyList<(MonsterModel, string?)> CreatePhaseFive()
    {
        _phaseFive = [];
        // 战后读档仅继续结算；未完成的终战重新生成初始阵容。
        if (SettlementTriggered)
        {
            return [];
        }

        var result = new List<(MonsterModel, string?)>();
        Add((NaturalFloorPhaseMonster)ModelDb.Monster<NaturalFloorNihilBoss>().ToMutable(), NihilBossSlot);
        foreach (NaturalFloorGirlKind kind in Enum.GetValues<NaturalFloorGirlKind>())
        {
            Add(NewNihilStatue(kind), NihilStatueSlots[(int)kind]);
        }

        return result;

        void Add(NaturalFloorPhaseMonster monster, string slot)
        {
            _phaseFive.Add(monster);
            result.Add((monster, slot));
        }
    }

    private static NaturalFloorNihilStatue NewNihilStatue(NaturalFloorGirlKind kind) => kind switch
    {
        NaturalFloorGirlKind.Love => (NaturalFloorNihilStatue)ModelDb.Monster<NaturalFloorLoveStatue>().ToMutable(),
        NaturalFloorGirlKind.Justice => (NaturalFloorNihilStatue)ModelDb.Monster<NaturalFloorJusticeStatue>().ToMutable(),
        NaturalFloorGirlKind.Happiness => (NaturalFloorNihilStatue)ModelDb.Monster<NaturalFloorHappinessStatue>().ToMutable(),
        _ => (NaturalFloorNihilStatue)ModelDb.Monster<NaturalFloorCourageStatue>().ToMutable()
    };

    private static NaturalFloorMagicalGirl NewNihilGirl(NaturalFloorGirlKind kind) => kind switch
    {
        NaturalFloorGirlKind.Love => (NaturalFloorMagicalGirl)ModelDb.Monster<NaturalFloorLoveGirl>().ToMutable(),
        NaturalFloorGirlKind.Justice => (NaturalFloorMagicalGirl)ModelDb.Monster<NaturalFloorJusticeGirl>().ToMutable(),
        NaturalFloorGirlKind.Happiness => (NaturalFloorMagicalGirl)ModelDb.Monster<NaturalFloorHappinessGirl>().ToMutable(),
        _ => (NaturalFloorMagicalGirl)ModelDb.Monster<NaturalFloorCourageGirl>().ToMutable()
    };

    internal NaturalFloorMagicalGirl? FindNihilGirl(NaturalFloorGirlKind kind) =>
        _phaseFive.OfType<NaturalFloorMagicalGirl>().FirstOrDefault(girl => girl.Kind == kind && girl.Creature is { IsAlive: true });

    internal NaturalFloorMagicalGirl[] LivingNihilGirls() =>
        _phaseFive.OfType<NaturalFloorMagicalGirl>().Where(girl => girl.Creature is { IsAlive: true }).OrderBy(girl => girl.Kind).ToArray();

    internal IReadOnlyList<Creature> NihilCombatTargets() =>
        LivingPlayers().OrderBy(player => player.Player!.NetId)
            .Concat(LivingNihilGirls().Select(girl => girl.Creature)).ToArray();

    internal async Task ReplaceBrokenNihilStatues(Creature? dyingStatue = null)
    {
        if (CurrentPhase != 5 || SettlementTriggered || _nihilReplacingStatues || _combatState == null)
        {
            return;
        }

        _nihilReplacingStatues = true;
        try
        {
            foreach (NaturalFloorNihilStatue statue in _phaseFive.OfType<NaturalFloorNihilStatue>()
                         .Where(statue => statue.SummonPending).OrderBy(statue => statue.Kind).ToArray())
            {
                // 当前死亡回调中的石像由原版死亡流程清理，保留死亡批次的上下文。
                if (statue.Creature != dyingStatue)
                {
                    await LiberationPhaseCleanup.RemoveTransitionCreature(statue.Creature, _combatState);
                }
                _phaseFive.Remove(statue);
                NaturalFloorMagicalGirl girl = NewNihilGirl(statue.Kind);
                _phaseFive.Add(girl);
                Creature creature = await CreatureCmd.Add(girl, _combatState, CombatSide.Enemy, NihilGirlSlots[(int)girl.Kind]);
                creature.PrepareForNextTurn(_combatState.PlayerCreatures);
            }
        }
        finally
        {
            _nihilReplacingStatues = false;
        }
    }

    private async Task BeforeNihilTurnStart(CombatSide side)
    {
        if (side != CombatSide.Player || _combatState == null || _nihilLastPlayerRound == _combatState.RoundNumber)
        {
            return;
        }

        _nihilLastPlayerRound = _combatState.RoundNumber;
        await ReplaceBrokenNihilStatues();
        if (NihilBoss is { } boss)
        {
            await boss.BeginPlayerRound();
        }
    }

    internal async Task GrantNihilGreedReward()
    {
        Player[] players = LivingPlayers()
            .OrderBy(creature => creature.Player!.NetId)
            .Select(creature => creature.Player!)
            .ToArray();
        if (players.Length == 0)
        {
            return;
        }

        Player[] missingShard = players
            .Where(player => !PileType.Hand.GetPile(player).Cards
                .Any(card => card is NaturalFloorNihilHappinessShard))
            .ToArray();
        if (missingShard.Length > 0)
        {
            foreach (Player player in missingShard)
            {
                await GiveNihilShard(player);
            }

            return;
        }

        NaturalFloorMagicalGirl[] eligibleGirls = LivingNihilGirls()
            .Where(girl => !girl.HasReceivedGreedBlock && girl.PendingGreedBlock == 0)
            .ToArray();
        if (eligibleGirls.Length > 0)
        {
            int index = players[0].RunState.Rng.MonsterAi.NextInt(eligibleGirls.Length);
            eligibleGirls[index].QueueGreedBlock();
        }
    }

    internal async Task GrantNihilDeferredBlock()
    {
        if (CurrentPhase != 5 || SettlementTriggered)
        {
            return;
        }

        foreach (NaturalFloorMagicalGirl girl in LivingNihilGirls())
        {
            await girl.GrantGreedBlock();
        }
    }

    private async Task GiveNihilShard(Player player)
    {
        CardPile hand = PileType.Hand.GetPile(player);
        if (hand.Cards.Any(card => card is NaturalFloorNihilHappinessShard))
        {
            return;
        }

        if (hand.Cards.Count >= CardPile.MaxCardsInHand)
        {
            CardModel discard = hand.Cards[player.RunState.Rng.CombatCardSelection.NextInt(hand.Cards.Count)];
            await CardCmd.Discard(new ThrowingPlayerChoiceContext(), discard);
        }

        await CardPileCmdCompat.AddToCombatAndPreview<NaturalFloorNihilHappinessShard>(
            [player.Creature], PileType.Hand, NaturalFloorNihilMoves.ShardCount, addedByPlayer: false);
    }

    internal Task CompleteNihilPhase()
    {
        if (CurrentPhase != 5 || SettlementTriggered || _combatState == null)
        {
            return Task.CompletedTask;
        }

        NihilCompleted = true;
        KilledBossCount = PlannedMaxPhase;
        SettlementTriggered = true;
        NaturalFloorLiberationSettlementStore.Record(this);
        NaturalFloorNihilTransition.Cleanup();
        // 原生死亡批次随后清理所有 Minion，保留其上下文供同批伤害和死亡钩子使用。
        return Task.CompletedTask;
    }
}

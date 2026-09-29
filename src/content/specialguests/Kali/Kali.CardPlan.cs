using System;
using System.Linq;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.content.specialguests.Kali;

// 敌方卡牌计划：本回合要打的牌存在基类的五个槽位里（卡牌 ID 映射成 0–6），读档后按槽位恢复同一份计划；
// 没有存储的计划时才用 MonsterAi 抽一份新的，并先把被“重复”排进来的牌放进去。
public sealed partial class Kali
{

    private EnemyCardRuntime CreateEnemyCardRuntime()
    {
        var runtime = new EnemyCardRuntime(this, 0, GetCurrentPlan);
        runtime.InitializeDeck(AllEnemyCardSpecs());
        return runtime;
    }

    private IReadOnlyList<EnemyCardSpec> GetCurrentPlan()
    {
        Dictionary<string, EnemyCardSpec> specs = EnemyCardSpecs;
        if (HasStoredIntentPlan)
        {
            EnemyCardSpec[] restored = Enumerable.Range(0, StoredIntentSlots)
                .Select(GetStoredIntent)
                .TakeWhile(static move => move >= 0)
                .Select(StoredMoveToCardId)
                .Where(static cardId => cardId != null)
                .Select(cardId => specs[cardId!])
                .ToArray();
            if (restored.Length > 0)
            {
                return restored;
            }

            ClearStoredIntentPlan();
        }

        PersistedEnemyCardPlanNumber++;
        int cardLimit = ResolvePlanCardLimit(
            PersistedEnemyCardPlanNumber,
            IntentCapacity);
        bool forceManifestationCards = _forceManifestationCardsInNextPlan;
        _forceManifestationCardsInNextPlan = false;
        IReadOnlyList<string> planCardIds = BuildPlanCardIds(
            cardLimit,
            EgoActive,
            forceManifestationCards,
            GetQueuedExtraCardIds(),
            PickOne);
        PersistedQueuedExtraCardIds = string.Empty;
        EnemyCardSpec[] plan = planCardIds
            .Select(cardId => specs[cardId])
            .ToArray();
        SaveStoredPlan(plan);
        return plan;
    }

    internal static IReadOnlyList<string> BuildPlanCardIds(
        int cardLimit,
        bool egoActive,
        bool forceManifestationCards,
        IReadOnlyList<string> queuedCardIds,
        Func<IReadOnlyList<string>, string> pickOne)
    {
        int resolvedLimit = Math.Clamp(cardLimit, 1, StoredIntentSlots);
        if (forceManifestationCards)
        {
            resolvedLimit = Math.Max(
                resolvedLimit,
                ManifestationRequiredCardIds.Count);
        }

        var plan = new List<string>(resolvedLimit);
        if (forceManifestationCards)
        {
            plan.AddRange(ManifestationRequiredCardIds.Take(resolvedLimit));
        }

        foreach (string cardId in queuedCardIds)
        {
            if (plan.Count >= resolvedLimit)
            {
                break;
            }

            if (RandomPlanCardIds.Contains(cardId, StringComparer.Ordinal))
            {
                plan.Add(cardId);
            }
        }

        IReadOnlyList<string> randomPool = egoActive
            ? RandomPlanCardIds
            : RandomPlanCardIds
                .Where(cardId => cardId != RedMistFieldOfCorpsesCardId)
                .ToArray();
        while (plan.Count < resolvedLimit)
        {
            string[] unusedCardIds = randomPool
                .Where(cardId => !plan.Contains(cardId, StringComparer.Ordinal))
                .ToArray();
            IReadOnlyList<string> candidates = unusedCardIds.Length > 0
                ? unusedCardIds
                : randomPool;
            plan.Add(pickOne(candidates));
        }

        return plan;
    }

    private void SaveStoredPlan(IReadOnlyList<EnemyCardSpec> plan)
    {
        ClearStoredIntentPlan();
        int count = Math.Min(plan.Count, StoredIntentSlots);
        for (int slot = 0; slot < count; slot++)
        {
            SetStoredIntent(slot, CardIdToStoredMove(plan[slot].Id));
        }
    }

    private IReadOnlyList<string> GetQueuedExtraCardIds() =>
        PersistedQueuedExtraCardIds.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void QueueExtraCardId(string cardId)
    {
        PersistedQueuedExtraCardIds = string.IsNullOrEmpty(PersistedQueuedExtraCardIds)
            ? cardId
            : PersistedQueuedExtraCardIds + "\n" + cardId;
    }

    private static int CardIdToStoredMove(string cardId) => cardId switch
    {
        RedMistVerticalSplitCardId => 0,
        RedMistThrustCardId => 1,
        RedMistHorizontalSlashCardId => 2,
        RedMistFocusBreathCardId => 3,
        RedMistBattleWillCardId => 4,
        RedMistBloodMistCardId => 5,
        RedMistFieldOfCorpsesCardId => 6,
        _ => -1,
    };

    private static string? StoredMoveToCardId(int move) => move switch
    {
        0 => RedMistVerticalSplitCardId,
        1 => RedMistThrustCardId,
        2 => RedMistHorizontalSlashCardId,
        3 => RedMistFocusBreathCardId,
        4 => RedMistBattleWillCardId,
        5 => RedMistBloodMistCardId,
        6 => RedMistFieldOfCorpsesCardId,
        _ => null,
    };

    private static int GetPlanCardLimit(int planNumber)
    {
        return planNumber switch
        {
            1 => OpeningPlanFirstTurnIntentCount,
            2 => OpeningPlanSecondTurnIntentCount,
            _ => int.MaxValue
        };
    }

    internal static int ResolvePlanCardLimit(
        int planNumber,
        int intentCapacity) =>
        Math.Min(
            GetPlanCardLimit(planNumber),
            Math.Clamp(intentCapacity, 1, StoredIntentSlots));

    private IReadOnlyList<EnemyCardSpec> AllEnemyCardSpecs()
    {
        return EnemyCardSpecs.Values.ToArray();
    }

    private Dictionary<string, EnemyCardSpec> EnemyCardSpecs =>
        _enemyCardSpecs ??= CreateEnemyCardSpecs();

    private Dictionary<string, EnemyCardSpec> CreateEnemyCardSpecs()
    {
        return new Dictionary<string, EnemyCardSpec>
        {
            [RedMistVerticalSplitCardId] = new(
                RedMistVerticalSplitCardId,
                CreateVerticalSplitDisplayCard,
                cost: 2,
                priority: 20,
                execute: (_, targets) => PlayVerticalSplit(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => VerticalSplitDamage, () => VerticalSplitHits)),
            [RedMistThrustCardId] = new(
                RedMistThrustCardId,
                CreateThrustDisplayCard,
                cost: 2,
                priority: 20,
                execute: (_, targets) => PlayThrust(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => ThrustDamage, () => ThrustHits)),
            [RedMistHorizontalSlashCardId] = new(
                RedMistHorizontalSlashCardId,
                CreateHorizontalSlashDisplayCard,
                cost: 2,
                priority: 30,
                execute: (_, targets) => PlayHorizontalSlash(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => HorizontalSlashDamage, () => HorizontalSlashHits)),
            [RedMistFocusBreathCardId] = new(
                RedMistFocusBreathCardId,
                CreateFocusBreathDisplayCard,
                cost: 2,
                priority: 70,
                execute: (_, _) => PlayFocusBreath(),
                createIntent: spec => EnemyCardCombinedIntentFactory.DefendBuff(spec)),
            [RedMistBattleWillCardId] = new(
                RedMistBattleWillCardId,
                CreateBattleWillDisplayCard,
                cost: 3,
                priority: 50,
                execute: (_, targets) => PlayBattleWill(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => BattleWillDamage)),
            [RedMistBloodMistCardId] = new(
                RedMistBloodMistCardId,
                CreateBloodMistDisplayCard,
                cost: 5,
                priority: 40,
                execute: (_, targets) => PlayBloodMist(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => BloodMistDamage)),
            [RedMistFieldOfCorpsesCardId] = new(
                RedMistFieldOfCorpsesCardId,
                CreateFieldOfCorpsesDisplayCard,
                cost: 6,
                priority: 50,
                execute: (_, targets) => PlayFieldOfCorpses(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => FieldOfCorpsesDamage))
        };
    }

    private AbstractIntent[] CreatePreviewCardIntents()
    {
        Dictionary<string, EnemyCardSpec> specs = EnemyCardSpecs;
        return EnemyCardSpec.CreateIntentSequence(
        [
            specs[RedMistVerticalSplitCardId],
            specs[RedMistThrustCardId],
            specs[RedMistBloodMistCardId]
        ]).ToArray();
    }

    private string PickOne(IReadOnlyList<string> cardIds)
    {
        return ResolvePlanRng().NextItem(cardIds)
               ?? cardIds[0];
    }

    private Rng ResolvePlanRng()
    {
        return Creature?.CombatState?.RunState.Rng.MonsterAi ?? Rng;
    }

    private static CardModel CreateVerticalSplitDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistVerticalSplitEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(VerticalSplitDamage); });
    }

    private static CardModel CreateThrustDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistThrustEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(ThrustDamage); });
    }

    private static CardModel CreateHorizontalSlashDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistHorizontalSlashEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(HorizontalSlashDamage); });
    }

    private static CardModel CreateBloodMistDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistBloodMistEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(BloodMistDamage); });
    }

    private static CardModel CreateBattleWillDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistBattleWillEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(BattleWillDamage); });
    }

    private static CardModel CreateFocusBreathDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistFocusBreathEgoCard>(
            card => { card.UpgradePreview(); });
    }

    private static CardModel CreateFieldOfCorpsesDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistFieldOfCorpsesEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(FieldOfCorpsesDamage); });
    }

}

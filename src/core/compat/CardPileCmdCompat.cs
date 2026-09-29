using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.core.compat;

internal static class CardPileCmdCompat
{
    private const string LogTag = "LibraryOfRuina.CardPileCmdCompat";

    // 与原版 CardPileCmd.AddToCombatAndPreview 一致的本地预览参数。
    private const float PreviewDisplaySeconds = 1.2f;
    private const float PreviewWaitSeconds = 1f;
    private const float HandAddWaitSeconds = 0.1f;
    private const int HorizontalPreviewMaxCards = 5;

    private static readonly CardPileAddResult FailedAddResult = new()
    {
        success = false,
        cardAdded = null!
    };

    private readonly record struct PendingPreview(Player Player, CardPileAddResult[] Results);

    private static Player? ResolveCreator(Creature target, bool addedByPlayer)
    {
        return addedByPlayer ? target.Player ?? target.PetOwner : null;
    }

    private static Player? ResolveCreator(CardModel card, bool addedByPlayer)
    {
        return addedByPlayer ? card.Owner : null;
    }

    private static bool CanAddGeneratedCombatCard(Creature target, int count)
    {
        if (count <= 0 || !CombatManager.Instance.IsInProgress || target.IsDead || target.CombatState == null)
        {
            return false;
        }

        Player? player = target.Player ?? target.PetOwner;
        return player != null && !player.Creature.IsDead;
    }

    private static bool CanAddGeneratedCombatCard(CardModel card)
    {
        Creature? ownerCreature = card.Owner?.Creature;
        return ownerCreature != null
            && !ownerCreature.IsDead
            && ownerCreature.CombatState != null
            && CombatManager.Instance.IsInProgress;
    }

    /// <summary>
    /// 先为所有目标生成并加入卡牌，再只在本地端播放预览。
    /// 发牌是各端一致的状态变更，预览只属于本地表现：若沿用原版“逐个目标发牌后立刻预览”的顺序，
    /// 本地预览一旦抛错，后续目标的卡牌只会在对端加入，多人各端的牌堆与卡牌编号随即分叉，
    /// 之后对方的出牌请求无法解析、卡牌队列停摆。
    /// </summary>
    public static async Task AddToCombatAndPreview<T>(
        IEnumerable<Creature> targets,
        PileType pileType,
        int count,
        bool addedByPlayer,
        CardPilePosition position = CardPilePosition.Bottom)
        where T : CardModel
    {
        var previews = new List<PendingPreview>();
        foreach (Creature target in targets)
        {
            PendingPreview? preview = await AddGeneratedCardsForTarget<T>(target, pileType, count, addedByPlayer, position);
            if (preview != null)
            {
                previews.Add(preview.Value);
            }
        }

        foreach (PendingPreview preview in previews)
        {
            await PreviewForLocalPlayer(preview, pileType);
        }
    }

    public static async Task AddToCombatAndPreview<T>(
        Creature target,
        PileType pileType,
        int count,
        bool addedByPlayer,
        CardPilePosition position = CardPilePosition.Bottom)
        where T : CardModel
    {
        PendingPreview? preview = await AddGeneratedCardsForTarget<T>(target, pileType, count, addedByPlayer, position);
        if (preview != null)
        {
            await PreviewForLocalPlayer(preview.Value, pileType);
        }
    }

    private static async Task<PendingPreview?> AddGeneratedCardsForTarget<T>(
        Creature target,
        PileType pileType,
        int count,
        bool addedByPlayer,
        CardPilePosition position)
        where T : CardModel
    {
        if (!CanAddGeneratedCombatCard(target, count))
        {
            return null;
        }

        Player player = (target.Player ?? target.PetOwner)!;
        Player? creator = ResolveCreator(target, addedByPlayer);
        var results = new CardPileAddResult[count];
        for (int i = 0; i < count; i++)
        {
            CardModel? card = target.CombatState?.CreateCard<T>(player);
            if (card == null)
            {
                continue;
            }

            try
            {
                results[i] = await CardPileCmd.AddGeneratedCardToCombat(card, pileType, creator, position);
            }
            catch (ArgumentOutOfRangeException) when (!CanAddGeneratedCombatCard(target, count))
            {
                // 目标在发牌途中退出战斗（阵亡等），与原实现一致：不再预览。
                return null;
            }
        }

        return new PendingPreview(player, results);
    }

    private static async Task PreviewForLocalPlayer(PendingPreview preview, PileType pileType)
    {
        if (!LocalContext.IsMe(preview.Player))
        {
            return;
        }

        if (pileType == PileType.Hand)
        {
            await Cmd.Wait(HandAddWaitSeconds);
            return;
        }

        try
        {
            CardPreviewStyle style = preview.Results.Length <= HorizontalPreviewMaxCards
                ? CardPreviewStyle.HorizontalLayout
                : CardPreviewStyle.MessyLayout;
            CardCmd.PreviewCardPileAdd(preview.Results, PreviewDisplaySeconds, style);
        }
        catch (Exception exception)
        {
            // 预览失败只影响本地画面：清掉已挂到预览容器上的卡牌节点，战斗流程照常继续。
            RemoveStuckPreviewCards(preview.Results);
            Log.Warn(
                "[" + LogTag + "] card pile add preview failed; skipped presentation only"
                + " cards=" + DescribeCards(preview.Results)
                + " error=" + exception.GetType().Name + ": " + exception.Message);
        }

        await Cmd.Wait(PreviewWaitSeconds);
    }

    private static void RemoveStuckPreviewCards(IReadOnlyList<CardPileAddResult> results)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null)
        {
            return;
        }

        var previewed = new HashSet<CardModel>(
            results.Where(static result => result.cardAdded != null).Select(static result => result.cardAdded));
        foreach (Control? container in new Control?[] { room.Ui.CardPreviewContainer, room.Ui.MessyCardPreviewContainer })
        {
            if (container == null || !GodotObject.IsInstanceValid(container))
            {
                continue;
            }

            foreach (Node child in container.GetChildren())
            {
                if (child is NCard { Model: { } model } card && previewed.Contains(model))
                {
                    card.QueueFreeSafely();
                }
            }
        }
    }

    private static string DescribeCards(IReadOnlyList<CardPileAddResult> results)
    {
        return string.Join(
            ",",
            results
                .Where(static result => result.cardAdded != null)
                .Select(static result => result.cardAdded.Id.Entry)
                .Distinct());
    }

    public static async Task AddToCombatWithoutPreview<T>(
        Creature target,
        PileType pileType,
        int count,
        bool addedByPlayer,
        CardPilePosition position = CardPilePosition.Bottom)
        where T : CardModel
    {
        Player? player = target.Player ?? target.PetOwner;
        if (count <= 0 || !CombatManager.Instance.IsInProgress || player == null || player.Creature.IsDead)
        {
            return;
        }

        var combatState = target.CombatState;
        if (combatState == null)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            CardModel card = combatState.CreateCard<T>(player);
            await AddGeneratedCardToCombat(card, pileType, addedByPlayer, position);
        }
    }

    public static async Task<CardPileAddResult> AddGeneratedCardToCombat(
        CardModel card,
        PileType newPileType,
        bool addedByPlayer,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        if (!CanAddGeneratedCombatCard(card))
        {
            return FailedAddResult;
        }

        try
        {
            return await CardPileCmd.AddGeneratedCardToCombat(card, newPileType, ResolveCreator(card, addedByPlayer), position);
        }
        catch (ArgumentOutOfRangeException) when (!CanAddGeneratedCombatCard(card))
        {
            return FailedAddResult;
        }
    }
}

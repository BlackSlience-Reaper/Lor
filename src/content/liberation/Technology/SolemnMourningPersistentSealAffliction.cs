using LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;
using LibraryOfRuina.infra.lifecycle;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed class SolemnMourningPersistentSealAffliction : AfflictionModel
{
    // 本回合已计入救赎之手的封印牌数，全队共用一个计数，玩家回合开始时由救赎之手清零。
    // 按战斗状态存放：同一场战斗里两端按同样的出牌顺序累加；读档重开的战斗、之后的战斗和另一局都从 0 开始，
    // 不会读到上一场没有清零（例如战斗中途退出）留下的值。
    private static readonly CombatScoped<CombatStateLike, int> SealedCardsPlayedThisTurn = new();

    internal static int GetSealedCardsPlayedThisTurn(CombatStateLike? combatState) =>
        SealedCardsPlayedThisTurn.GetValueOrDefault(combatState, 0);

    internal static void SetSealedCardsPlayedThisTurn(CombatStateLike combatState, int count) =>
        SealedCardsPlayedThisTurn.Set(combatState, count);

    public override bool HasExtraCardText => true;

    public override bool CanAfflict(CardModel card) => base.CanAfflict(card);

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card != Card) return true;
        // 原版 CanPlay 与自动打出都经 Hook.ShouldPlay 问到这里；计数由救赎之手在出牌后累加、玩家回合开始时清零，
        // 两端按同样的出牌顺序得到同样的结果。
        return GetSealedCardsPlayedThisTurn(card.CombatState)
               < SolemnMourningRedemptionHandPower.MaxSealedCardsPerTurn;
    }

    internal static bool IsPersistentSeal(CardModel card) =>
        card.Affliction is SolemnMourningPersistentSealAffliction;

    internal static void ClearPersistentSeal(CardModel card)
    {
        if (IsPersistentSeal(card))
        {
            CardCmd.ClearAffliction(card);
        }
    }

    internal static void ClearAllPersistentSeals(CombatStateLike combatState)
    {
        foreach (CardModel card in GetAllPlayerCards(combatState))
        {
            ClearPersistentSeal(card);
        }
    }

    internal static bool IsAnySeal(CardModel card) =>
        FuneralSealAffliction.IsSeal(card) || IsPersistentSeal(card);

    private static IEnumerable<CardModel> GetAllPlayerCards(CombatStateLike combatState)
    {
        foreach (var player in combatState.Players)
        {
            if (player.PlayerCombatState == null)
            {
                continue;
            }

            foreach (CardModel card in player.PlayerCombatState.Hand.Cards)
            {
                yield return card;
            }

            foreach (CardModel card in player.PlayerCombatState.DrawPile.Cards)
            {
                yield return card;
            }

            foreach (CardModel card in player.PlayerCombatState.DiscardPile.Cards)
            {
                yield return card;
            }

            foreach (CardModel card in player.PlayerCombatState.ExhaustPile.Cards)
            {
                yield return card;
            }
        }
    }
}

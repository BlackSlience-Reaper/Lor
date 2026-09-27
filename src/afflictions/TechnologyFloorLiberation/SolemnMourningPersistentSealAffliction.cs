using LibraryOfRuina.afflictions.FuneralOfTheDeadButterflies;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.afflictions.TechnologyFloorLiberation;

public sealed class SolemnMourningPersistentSealAffliction : AfflictionModel
{
    private const int MaxSealedCardsPerTurn = 6;

    internal static int SealedCardsPlayedThisTurn;

    public override bool HasExtraCardText => true;

    public override bool CanAfflict(CardModel card) => base.CanAfflict(card);

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card != Card) return true;
        return SealedCardsPlayedThisTurn < MaxSealedCardsPerTurn;
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

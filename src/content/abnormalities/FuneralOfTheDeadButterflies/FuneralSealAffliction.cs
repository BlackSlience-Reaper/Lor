using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;

public sealed class FuneralSealAffliction : AfflictionModel
{
    public override bool HasExtraCardText => true;

    public override bool CanAfflict(CardModel card)
    {
        return card.Pile?.Type == PileType.Hand && base.CanAfflict(card);
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != Card;
    }

    internal static bool IsSeal(CardModel card) =>
        card.Affliction is FuneralSealAffliction;

    internal static void ClearSeal(CardModel card)
    {
        if (IsSeal(card))
        {
            CardCmd.ClearAffliction(card);
        }
    }

    internal static void ClearPlayerHandSeals(CombatStateLike combatState)
    {
        foreach (CardModel card in GetPlayerHandCards(combatState))
        {
            ClearSeal(card);
        }
    }

    internal static bool AnySealedCardsInPlayerHands(CombatStateLike combatState) =>
        GetPlayerHandCards(combatState).Any(IsSeal);

    private static IEnumerable<CardModel> GetPlayerHandCards(CombatStateLike combatState)
    {
        foreach (var player in combatState.Players)
        {
            if (player.PlayerCombatState?.Hand == null)
            {
                continue;
            }

            foreach (CardModel card in player.PlayerCombatState.Hand.Cards)
            {
                yield return card;
            }
        }
    }
}

using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.features.secondascension;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.encounters;

internal static class LiberationPhasePlayerRecovery
{
    public static async Task RestorePlayers(
        CombatStateLike combatState,
        decimal healAmount)
    {
        // 都市怪谈（2级）：解放战转阶段不再恢复玩家生命。
        if (LibrarySecondAscensionState.SkipsLiberationPhaseRecovery(combatState.RunState))
        {
            return;
        }

        foreach (Creature playerCreature in combatState.PlayerCreatures)
        {
            bool wasDead = playerCreature.IsDead;
            await CreatureCmd.Heal(playerCreature, healAmount);

            if (wasDead && playerCreature.IsAlive)
            {
                await RebuildRevivedPlayerDrawPile(
                    combatState,
                    playerCreature);
            }
        }
    }

    private static async Task RebuildRevivedPlayerDrawPile(
        CombatStateLike combatState,
        Creature playerCreature)
    {
        if (playerCreature.Player?.PlayerCombatState is not { } playerCombatState
            || playerCombatState.AllCards.Any())
        {
            return;
        }

        List<CardModel> combatCards = [];
        foreach (CardModel deckCard in playerCreature.Player.Deck.Cards.ToList())
        {
            CardModel combatCard = combatState.CloneCard(deckCard);
            combatCard.DeckVersion = deckCard;
            combatCards.Add(combatCard);
        }

        combatCards.StableShuffle(playerCreature.Player.RunState.Rng.Shuffle);

        foreach (CardModel combatCard in combatCards)
        {
            CardPileAddResult result = await CardPileCmd.Add(
                combatCard,
                PileType.Draw,
                CardPilePosition.Bottom,
                null,
                true);
            if (result.success)
            {
                combatCard.Pile?.InvokeCardAddFinished();
            }
        }
    }
}

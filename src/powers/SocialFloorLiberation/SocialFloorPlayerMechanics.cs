using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.ScarecrowSearchingForWisdom;
using LibraryOfRuina.cards.SocialFloorLiberation;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.powers.ScarecrowSearchingForWisdom;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.powers.SocialFloorLiberation;

/// <summary>
/// Encounter-facing API for player-owned Social Floor trial state.
/// All ownership is resolved by Player.NetId; no multiplayer state is shared
/// through process-static booleans or Creature.CombatId.
/// </summary>
public static class SocialFloorPlayerMechanics
{
    internal static string FormatHolderNetId(ulong netId) =>
        netId.ToString(CultureInfo.InvariantCulture);

    internal static ulong ParseHolderNetId(string? serializedNetId) =>
        ulong.TryParse(
            serializedNetId,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out ulong netId)
            ? netId
            : 0UL;

    public static bool VerifyHolderNetIdSerializationRoundTrip(ulong netId) =>
        ParseHolderNetId(FormatHolderNetId(netId)) == netId;

    public static async Task<SocialFloorScaredyCatPower?> GrantScaredyCat(
        Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.IsDead || player.Creature.CombatState == null)
        {
            return null;
        }

        SocialFloorScaredyCatPower? power =
            await PowerCmdCompat.Ensure<SocialFloorScaredyCatPower>(
                player.Creature,
                1m,
                player.Creature,
                null,
                silent: true);
        if (power == null)
        {
            return null;
        }

        power.BindHolder(player);
        if (!power.CourageCardGranted)
        {
            SocialFloorCourageCard card =
                player.Creature.CombatState.CreateCard<SocialFloorCourageCard>(
                    player);
            CardPileAddResult result =
                await AddGeneratedCardToCombatAndPreview(
                    card,
                    PileType.Hand,
                    addedByPlayer: false);
            if (!result.success)
            {
                result = await AddGeneratedCardToCombatAndPreview(
                    card,
                    PileType.Discard,
                    addedByPlayer: false);
            }
            if (result.success)
            {
                power.MarkCourageCardGranted();
            }
        }

        return power;
    }

    public static async Task<SocialFloorScaredyCatPower?> RestoreScaredyCat(
        Player player,
        int cardsSubmittedThisTurn,
        bool courageCardGranted,
        bool isHolderActive,
        bool ensureCourageCard)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.IsDead
            || player.Creature.CombatState == null
            || player.PlayerCombatState == null)
        {
            return null;
        }

        SocialFloorScaredyCatPower? power =
            await PowerCmdCompat.Ensure<SocialFloorScaredyCatPower>(
                player.Creature,
                1m,
                player.Creature,
                null,
                silent: true);
        if (power == null)
        {
            return null;
        }

        SocialFloorCourageCard[] existing = player.PlayerCombatState
            .AllCards
            .OfType<SocialFloorCourageCard>()
            .ToArray();
        bool hasCard = existing.Length > 0;
        if (ensureCourageCard && !hasCard)
        {
            SocialFloorCourageCard card = player.Creature.CombatState
                .CreateCard<SocialFloorCourageCard>(player);
            CardPileAddResult result =
                await AddGeneratedCardToCombatAndPreview(
                    card,
                    PileType.Hand,
                    addedByPlayer: false);
            if (!result.success)
            {
                result = await AddGeneratedCardToCombatAndPreview(
                    card,
                    PileType.Discard,
                    addedByPlayer: false);
            }
            hasCard = result.success;
        }

        power.RestoreState(
            player,
            cardsSubmittedThisTurn,
            courageCardGranted && (!ensureCourageCard || hasCard),
            isHolderActive);
        return power;
    }

    public static async Task<SocialFloorCouragePower?> ApplyCourage(
        Player player,
        CardModel? cardSource = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.IsDead || player.Creature.CombatState == null)
        {
            return null;
        }

        SocialFloorCouragePower? power =
            await PowerCmdCompat.Ensure<SocialFloorCouragePower>(
                player.Creature,
                1m,
                player.Creature,
                cardSource,
                silent: false);
        power?.BindHolder(player);
        return power;
    }

    public static async Task<SocialFloorCouragePower?> RestoreCourage(
        Player player,
        int pendingTurnStartActivations,
        bool isEnergyOverrideActive,
        bool removeAtNextPlayerTurnEnd)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.IsDead || player.Creature.CombatState == null)
        {
            return null;
        }

        SocialFloorCouragePower? power =
            await PowerCmdCompat.Ensure<SocialFloorCouragePower>(
                player.Creature,
                1m,
                player.Creature,
                null,
                silent: true);
        if (power == null)
        {
            return null;
        }

        power.RestoreState(
            player,
            pendingTurnStartActivations,
            isEnergyOverrideActive,
            removeAtNextPlayerTurnEnd);
        if (power.IsEnergyOverrideActive
            && power.PendingTurnStartActivations == 0
            && power.RemoveAtNextPlayerTurnEnd)
        {
            await power.RestoreActiveEffects();
        }

        return power;
    }

    public static async Task<SocialFloorCowardPower?> ApplyCoward(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.IsDead || player.Creature.CombatState == null)
        {
            return null;
        }

        SocialFloorCowardPower? power =
            await PowerCmdCompat.Ensure<SocialFloorCowardPower>(
                player.Creature,
                1m,
                player.Creature,
                null,
                silent: false);
        power?.BindHolder(player);
        return power;
    }

    public static async Task<SocialFloorCowardPower?> RestoreCoward(
        Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.IsDead || player.Creature.CombatState == null)
        {
            return null;
        }

        SocialFloorCowardPower? power =
            await PowerCmdCompat.Ensure<SocialFloorCowardPower>(
                player.Creature,
                1m,
                player.Creature,
                null,
                silent: true);
        power?.BindHolder(player);
        return power;
    }

    public static async Task<SocialFloorOzmaPower?> GrantOzma(
        Player player,
        int initialCost = SocialFloorMagicalPowderCard.DefaultInternalCost)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.IsDead || player.Creature.CombatState == null)
        {
            return null;
        }

        await InvalidatePowdersOnly(player);
        SocialFloorOzmaPower? power =
            await PowerCmdCompat.Ensure<SocialFloorOzmaPower>(
                player.Creature,
                1m,
                player.Creature,
                null,
                silent: true);
        if (power == null)
        {
            return null;
        }

        power.BindHolder(player);
        SocialFloorMagicalPowderCard powder =
            player.Creature.CombatState
                .CreateCard<SocialFloorMagicalPowderCard>(player);
        powder.ResetForHolder(player, Math.Max(0, initialCost));
        CardPileAddResult result =
            await AddGeneratedCardToCombatAndPreview(
                powder,
                PileType.Hand,
                addedByPlayer: false);
        if (!result.success)
        {
            result = await AddGeneratedCardToCombatAndPreview(
                powder,
                PileType.Discard,
                addedByPlayer: false);
        }
        if (!result.success)
        {
            powder.Invalidate();
        }

        return power;
    }

    public static async Task<SocialFloorOzmaPower?> RestoreOzma(
        Player player,
        int internalCost,
        bool ensurePowder)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Creature.IsDead
            || player.Creature.CombatState == null
            || player.PlayerCombatState == null)
        {
            return null;
        }

        SocialFloorOzmaPower? power =
            await PowerCmdCompat.Ensure<SocialFloorOzmaPower>(
                player.Creature,
                1m,
                player.Creature,
                null,
                silent: true);
        if (power == null)
        {
            return null;
        }
        power.BindHolder(player);

        SocialFloorMagicalPowderCard[] powders = player.PlayerCombatState
            .AllCards
            .OfType<SocialFloorMagicalPowderCard>()
            .ToArray();
        SocialFloorMagicalPowderCard? powder = ensurePowder
            ? powders.FirstOrDefault(static card => card.IsHolderValid)
            : null;
        foreach (SocialFloorMagicalPowderCard stale in powders)
        {
            if (!ReferenceEquals(stale, powder))
            {
                stale.Invalidate();
            }
        }

        if (!ensurePowder)
        {
            return power;
        }

        if (powder == null)
        {
            powder = player.Creature.CombatState
                .CreateCard<SocialFloorMagicalPowderCard>(player);
            powder.ResetForHolder(player, Math.Max(0, internalCost));
            CardPileAddResult result =
                await AddGeneratedCardToCombatAndPreview(
                    powder,
                    PileType.Hand,
                    addedByPlayer: false);
            if (!result.success)
            {
                result = await AddGeneratedCardToCombatAndPreview(
                    powder,
                    PileType.Discard,
                    addedByPlayer: false);
            }
            if (!result.success)
            {
                powder.Invalidate();
            }
        }
        else
        {
            powder.ResetForHolder(player, Math.Max(0, internalCost));
        }

        return power;
    }

    public static Task InvalidateOzma(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Creature.GetPower<SocialFloorOzmaPower>()?.InvalidateHolder();
        return InvalidatePowdersOnly(player);
    }

    public static bool TryGetActivePowder(
        Player player,
        out SocialFloorMagicalPowderCard? powder)
    {
        ArgumentNullException.ThrowIfNull(player);
        powder = player.PlayerCombatState?.AllCards
            .OfType<SocialFloorMagicalPowderCard>()
            .FirstOrDefault(card =>
                card.IsHolderValid
                && card.HolderNetId == player.NetId);
        return powder != null;
    }

    public static int? GetMagicalPowderCost(Player player) =>
        TryGetActivePowder(player, out SocialFloorMagicalPowderCard? powder)
            ? powder!.InternalCost
            : null;

    public static int GetWisdomStacks(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Creature
            .GetPowerInstances<ScarecrowWisdomPower>()
            .Sum(static power => Math.Max(0, power.Amount));
    }

    public static bool HasWisdomAbove(Player player, int threshold) =>
        GetWisdomStacks(player) > threshold;

    public static IReadOnlyDictionary<ulong, int> GetWisdomStacksByPlayer(
        CombatStateLike combatState) =>
        combatState.Players
            .OrderBy(static player => player.NetId)
            .ToDictionary(
                static player => player.NetId,
                GetWisdomStacks);

    public static async Task AddWisdomCards(
        CombatStateLike combatState,
        int count)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        if (count <= 0)
        {
            return;
        }

        foreach (Player player in combatState.Players
                     .Where(static player => player.Creature.IsAlive)
                     .OrderBy(static player => player.NetId))
        {
            await CardPileCmdCompat
                .AddToCombatAndPreview<ScarecrowWisdomStatusCard>(
                    player.Creature,
                    PileType.Discard,
                    count,
                    addedByPlayer: false);
        }
    }

    internal static bool IsScarecrowTrialCard(CardModel card) =>
        card is ScarecrowWisdomStatusCard;

    internal static bool IsScarecrowTrialPower(PowerModel power) =>
        power is ScarecrowWisdomPower;

    internal static bool IsLionTrialCard(CardModel card) =>
        card is SocialFloorCourageCard;

    internal static bool IsLionTrialPower(PowerModel power) =>
        power is SocialFloorScaredyCatPower or SocialFloorCouragePower;

    internal static async Task ClearScarecrowTrialState(
        CombatStateLike combatState)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        foreach (Player player in combatState.Players
                     .OrderBy(static player => player.NetId))
        {
            await RemoveTrialCards(player, IsScarecrowTrialCard);

            foreach (PowerModel power in player.Creature.Powers
                         .Where(IsScarecrowTrialPower)
                         .ToArray())
            {
                await PowerCmd.Remove(power);
            }
        }
    }

    internal static async Task ClearLionTrialState(
        CombatStateLike combatState,
        ulong? holderNetId)
    {
        ArgumentNullException.ThrowIfNull(combatState);
        foreach (Player player in combatState.Players
                     .Where(player => IsLionTrialHolder(player, holderNetId))
                     .OrderBy(static player => player.NetId))
        {
            await RemoveTrialCards(player, IsLionTrialCard);

            foreach (PowerModel power in player.Creature.Powers
                         .Where(IsLionTrialPower)
                         .ToArray())
            {
                if (power is SocialFloorScaredyCatPower scaredyCat)
                {
                    scaredyCat.InvalidateHolder();
                }
                await PowerCmd.Remove(power);
            }
        }
    }

    internal static async Task<CardPileAddResult>
        AddGeneratedCardToCombatAndPreview(
            CardModel card,
            PileType newPileType,
            bool addedByPlayer,
            CardPilePosition position = CardPilePosition.Bottom)
    {
        CardPileAddResult result =
            await CardPileCmdCompat.AddGeneratedCardToCombat(
                card,
                newPileType,
                addedByPlayer,
                position);
        CardCmd.PreviewCardPileAdd(result);
        return result;
    }

    private static async Task RemoveTrialCards(
        Player player,
        Func<CardModel, bool> predicate)
    {
        if (player.PlayerCombatState is not { } playerCombatState)
        {
            return;
        }

        CardModel[] visibleCards = playerCombatState.AllPiles
            .Where(static pile => pile.Type is PileType.Hand or PileType.Play)
            .SelectMany(static pile => pile.Cards)
            .Where(predicate)
            .Distinct()
            .ToArray();
        if (visibleCards.Length > 0)
        {
            await CardPileCmd.RemoveFromCombat(visibleCards);
        }

        // Re-query after the visible animation. This also catches draw,
        // discard, exhaust and transient pile states that the old
        // IsCombatPile filter could leave behind at a phase boundary.
        CardModel[] remainingCards = playerCombatState.AllCards
            .Where(predicate)
            .ToArray();
        if (remainingCards.Length > 0)
        {
            await CardPileCmd.RemoveFromCombat(
                remainingCards,
                skipVisuals: true);
        }
    }

    private static bool IsLionTrialHolder(Player player, ulong? holderNetId) =>
        player.NetId == holderNetId
        || player.Creature.Powers.Any(power =>
            IsLionTrialPower(power)
            && power switch
            {
                SocialFloorScaredyCatPower scaredyCat =>
                    scaredyCat.HolderNetId == player.NetId,
                SocialFloorCouragePower courage =>
                    courage.HolderNetId == player.NetId,
                _ => false
            });

    private static Task InvalidatePowdersOnly(Player player)
    {
        foreach (SocialFloorMagicalPowderCard powder in
                 player.PlayerCombatState?.AllCards
                     .OfType<SocialFloorMagicalPowderCard>()
                     .ToArray()
                 ?? [])
        {
            powder.Invalidate();
        }

        return Task.CompletedTask;
    }
}

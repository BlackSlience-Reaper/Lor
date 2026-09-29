using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.ScarecrowSearchingForWisdom;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.SocialFloorLiberation;
using LibraryOfRuina.powers.ScarecrowSearchingForWisdom;
using LibraryOfRuina.powers.SocialFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.encounters.SocialFloorLiberation;

/// <summary>
/// 稻草人试炼：每名玩家拿到智慧卡。第 2 回合结束时智慧层数不到要求的存活玩家失去当前生命的一定比例，然后进入狮子试炼。
/// 智慧卡张数读 <c>FalseThrone.UsesToughValues</c>，判定要求读本局的进阶等级，两处写法保持原样。
/// </summary>
internal sealed class SocialTrialScarecrow : SocialTrial
{
    internal override FalseThroneMove ResolveDefaultMove(int round) =>
        FalseThroneMove.UnknownTrial;

    internal override async Task SetupAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        encounter.SavedWisdomStacks = string.Empty;
        encounter.SavedWisdomCards = string.Empty;
        await SocialFloorPlayerMechanics.AddWisdomCards(
            combatState,
            boss.UsesToughValues
                ? SocialFloorLiberationEncounter.ToughWisdomCardCount
                : SocialFloorLiberationEncounter.NormalWisdomCardCount);
    }

    internal override async Task AfterEnemyTurnAsync(
        SocialFloorLiberationEncounter encounter,
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (encounter.TrialRound >= 2)
        {
            await ResolveScarecrowPenalty(choiceContext, combatState);
            encounter.QueueTrial(SocialFloorTrial.Lion);
            await encounter.EnterPendingTrial(choiceContext, boss, combatState);
        }
        else
        {
            encounter.PlannedMove = FalseThroneMove.UnknownTrial;
            boss.ForcePlannedMove(encounter.PlannedMove);
        }
    }

    internal override async Task CleanupAsync(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState)
    {
        await SocialFloorPlayerMechanics
            .ClearScarecrowTrialState(combatState);
        encounter.SavedWisdomStacks = string.Empty;
        encounter.SavedWisdomCards = string.Empty;
    }

    internal override async Task RestoreBeforeSharedStateAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        IReadOnlyDictionary<ulong, int> wisdomStacks =
            SocialFloorLiberationEncounter.ParsePlayerValues(encounter.SavedWisdomStacks);
        IReadOnlyDictionary<ulong, int> wisdomCards =
            SocialFloorLiberationEncounter.ParsePlayerValues(encounter.SavedWisdomCards);
        // Combat rooms are reconstructed from encounter state. Normalize
        // any partially restored trial objects before recreating the exact
        // saved per-player state so loading is idempotent.
        await SocialFloorPlayerMechanics
            .ClearScarecrowTrialState(combatState);
        int initialCards = boss.UsesToughValues
            ? SocialFloorLiberationEncounter.ToughWisdomCardCount
            : SocialFloorLiberationEncounter.NormalWisdomCardCount;
        foreach (Player player in combatState.Players
                     .Where(static player => player.Creature.IsAlive)
                     .OrderBy(static player => player.NetId))
        {
            int stacks = Math.Max(
                0,
                Math.Min(
                    SocialFloorLiberationEncounter.NormalWisdomCardCount,
                    wisdomStacks.GetValueOrDefault(player.NetId)));
            if (stacks > 0)
            {
                await PowerCmdCompat.Ensure<ScarecrowWisdomPower>(
                    player.Creature,
                    stacks,
                    player.Creature,
                    null,
                    silent: true);
            }

            int cards = wisdomCards.TryGetValue(
                player.NetId,
                out int savedCardCount)
                    ? Math.Clamp(
                        savedCardCount,
                        0,
                        initialCards)
                    : Math.Max(0, initialCards - stacks);
            await CardPileCmdCompat
                .AddToCombatAndPreview<ScarecrowWisdomStatusCard>(
                    player.Creature,
                    PileType.Discard,
                    cards,
                    addedByPlayer: false);
        }
    }

    private static async Task ResolveScarecrowPenalty(
        PlayerChoiceContext choiceContext,
        CombatStateLike combatState)
    {
        int required = combatState.RunState.AscensionLevel
            >= (int)AscensionLevel
                .ToughEnemies
            ? SocialFloorLiberationEncounter.ToughWisdomRequirement
            : SocialFloorLiberationEncounter.NormalWisdomRequirement;
        foreach (Player player in combatState.Players
                     .Where(static player => player.Creature.IsAlive)
                     .OrderBy(static player => player.NetId))
        {
            if (SocialFloorPlayerMechanics.GetWisdomStacks(player)
                >= required)
            {
                continue;
            }

            decimal loss = Math.Ceiling(
                player.Creature.CurrentHp
                * SocialFloorLiberationEncounter.ScarecrowPenaltyHpLossPercent
                / 100m);
            if (loss > 0m)
            {
                await CreatureCmd.Damage(
                    choiceContext,
                    player.Creature,
                    loss,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    null,
                    null);
            }
        }
    }
}

using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Social;

/// <summary>
/// 狮子试炼：四张面孔；一名存活玩家拿到胆小猫（已记录的持有者仍存活就沿用，否则按 CombatTargets 随机）。
/// 每个玩家回合开始时全体存活玩家得到虚弱与缴械。
/// 面孔全部摧毁时进入家园试炼；第 2 回合结束时仍有面孔则全体得到懦弱、移除面孔后进入家园试炼。
/// 胆小猫持有者以外的玩家死亡也会让全体得到懦弱。
/// </summary>
internal sealed class SocialTrialLion : SocialTrial
{
    internal override FalseThroneMove ResolveDefaultMove(int round) =>
        FalseThroneMove.StunTrial;

    internal override void AddRoomSummons(
        SocialFloorLiberationEncounter encounter,
        List<(MonsterModel, string?)> monsters)
    {
        int hp = SocialFloorLiberationEncounter.ResolveFaceHpForPlayerCount(
            encounter.ParticipantCount);
        for (int index = 0; index < SocialFloorLiberationEncounter.FaceSlots.Count; index++)
        {
            if ((encounter.DestroyedFaceMask & (1 << index)) != 0)
            {
                continue;
            }

            var face = (ScowlingFace)ModelDb
                .Monster<ScowlingFace>()
                .ToMutable();
            face.ConfigureInitialHp(hp);
            monsters.Add((face, SocialFloorLiberationEncounter.FaceSlots[index]));
        }
    }

    internal override async Task SetupAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        encounter.DestroyedFaceMask = 0;
        encounter.LionCardsSubmitted = 0;
        encounter.CouragePlayed = false;
        encounter.CouragePowerPresent = false;
        encounter.CouragePendingActivations = 0;
        encounter.CourageEnergyActive = false;
        encounter.CourageRemoveAtTurnEnd = false;
        await SpawnMissingFaces(encounter, combatState);
        await GrantScaredyCat(encounter, combatState);
    }

    internal override async Task SpawnMissingSummonsAsync(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState)
    {
        await SpawnMissingFaces(encounter, combatState);
    }

    internal override async Task AfterEnemyTurnAsync(
        SocialFloorLiberationEncounter encounter,
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (encounter.AreAllFacesDestroyed)
        {
            encounter.QueueTrial(SocialFloorTrial.Home);
            await encounter.EnterPendingTrial(choiceContext, boss, combatState);
        }
        else if (encounter.TrialRound >= 2)
        {
            await encounter.ApplyCowardToAllLivingPlayers();
            await RemoveTrialSummons<ScowlingFace>(combatState);
            encounter.QueueTrial(SocialFloorTrial.Home);
            await encounter.EnterPendingTrial(choiceContext, boss, combatState);
        }
        else
        {
            encounter.PlannedMove = FalseThroneMove.StunTrial;
            boss.ForcePlannedMove(encounter.PlannedMove);
        }
    }

    internal override async Task BeforePlayerTurnAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        await ApplyLionRoundDebuffs(combatState, boss.Creature);
    }

    internal override async Task CleanupAsync(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState)
    {
        await RemoveTrialSummons<ScowlingFace>(combatState);
        await SocialFloorPlayerMechanics.ClearLionTrialState(
            combatState,
            encounter.ScaredyCatPlayerNetId);
        encounter.LionCardsSubmitted = 0;
        encounter.CouragePowerPresent = false;
        encounter.CouragePendingActivations = 0;
        encounter.CourageEnergyActive = false;
        encounter.CourageRemoveAtTurnEnd = false;
    }

    internal override async Task RestoreBeforeSharedStateAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        await SocialFloorPlayerMechanics.ClearLionTrialState(
            combatState,
            encounter.ScaredyCatPlayerNetId);

        Player? catHolder = SocialFloorLiberationEncounter.ResolveLivingPlayer(
            combatState,
            encounter.ScaredyCatPlayerNetId);
        if (catHolder != null)
        {
            await SocialFloorPlayerMechanics.RestoreScaredyCat(
                catHolder,
                encounter.LionCardsSubmitted,
                courageCardGranted: !encounter.CouragePlayed,
                isHolderActive: true,
                ensureCourageCard: !encounter.CouragePlayed);
        }
    }

    internal override async Task OnPlayerDiedAsync(
        SocialFloorLiberationEncounter encounter,
        Player player,
        ulong netId)
    {
        if (encounter.ScaredyCatPlayerNetId is { } catNetId
            && netId != catNetId)
        {
            await encounter.ApplyCowardToAllLivingPlayers();
        }
    }

    private static async Task SpawnMissingFaces(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState)
    {
        int hp = SocialFloorLiberationEncounter.ResolveFaceHpForPlayerCount(
            combatState.Players.Count);
        for (int index = 0; index < SocialFloorLiberationEncounter.FaceSlots.Count; index++)
        {
            int bit = 1 << index;
            string slot = SocialFloorLiberationEncounter.FaceSlots[index];
            if ((encounter.DestroyedFaceMask & bit) != 0
                || combatState.Enemies.Any(enemy =>
                    enemy.IsAlive
                    && enemy.Monster is ScowlingFace
                    && enemy.SlotName == slot))
            {
                continue;
            }

            var face = (ScowlingFace)ModelDb
                .Monster<ScowlingFace>()
                .ToMutable();
            face.ConfigureInitialHp(hp);
            Creature spawned = await CreatureCmd.Add(
                face,
                combatState,
                CombatSide.Enemy,
                slot);
            spawned.PrepareForNextTurn(combatState.PlayerCreatures);
        }

        combatState.SortEnemiesBySlotName();
    }

    private static async Task GrantScaredyCat(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState)
    {
        Player? holder = SocialFloorLiberationEncounter.ResolveLivingPlayer(
            combatState,
            encounter.ScaredyCatPlayerNetId);
        holder ??= SocialFloorLiberationEncounter.ChooseRandomLivingPlayer(combatState);
        if (holder == null)
        {
            return;
        }

        encounter.ScaredyCatPlayerNetId = holder.NetId;
        await SocialFloorPlayerMechanics.GrantScaredyCat(holder);
    }

    private static async Task ApplyLionRoundDebuffs(
        CombatStateLike combatState,
        Creature applier)
    {
        PlayerChoiceContext context = new ThrowingPlayerChoiceContext();
        foreach (Creature player in SocialFloorLiberationEncounter.LivingPlayers(combatState))
        {
            await LibraryPowerCmd
                .Apply<LibraryWeakPower>(
                    context,
                    player,
                    999m,
                    turns: 0,
                    IsPermanent: false,
                    applier,
                    null);
            await LibraryPowerCmd
                .Apply<LibraryDisarmPower>(
                    context,
                    player,
                    999m,
                    turns: 0,
                    IsPermanent: false,
                    applier,
                    null);
        }
    }
}

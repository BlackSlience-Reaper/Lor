using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.cards.Leticia;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.Leticia;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.Leticia;
using LibraryOfRuina.visuals.Leticia;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.monsters.Leticia;

public sealed class SurpriseGiftBox : LibraryMonsterModel
{
    private const string MoveOneId = "EE_YO_LI_WOO";
    private const string MoveTwoId = "COUGH_OMM_JJI_AO";
    private const string MoveThreeId = "PU_AO_JJI_AH";

    private const int CountdownTurns = 3;
    private const int MoveTwoHits = 3;
    private const int SelfDestructDamage = 10;
    private const int GiftCardCount = 1;

    private const string AttackSfxPath = "res://audio/sfx/leticia/friend_hit.ogg";
    private const string GiftOpenSfxPath = "res://audio/sfx/leticia/gift_open.ogg";
    private const string GiftCloseSfxPath = "res://audio/sfx/leticia/gift_close.ogg";
    private const string FriendSpawnSfxPath = "res://audio/sfx/leticia/friend_spawn.ogg";

    private static readonly string[] ExtraAssetPaths =
    [
        AttackSfxPath,
        GiftOpenSfxPath,
        GiftCloseSfxPath,
        FriendSpawnSfxPath,
        LeticiaFilterOverlayController.FilterOneTexturePath
    ];

    private bool _giftGranted;
    private bool _friendSpawned;

    private int SmallDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 2, 1);

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 40, 39);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 42, 40);

    public override int DefaultChaoResistance => 30;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                SurpriseGiftBoxCreatureVisuals.Profile.AssetPaths.Count
                + ExtraAssetPaths.Length
                + 12);
            paths.AddRange(
                SurpriseGiftBoxCreatureVisuals.Profile.AssetPaths);
            paths.AddRange(ExtraAssetPaths);

            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _giftGranted = false;
        _friendSpawned = false;

        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, null, null, silent: true);
        await PowerCmdCompat.Apply<LeticiaSurpriseAppearancePower>(
            Creature,
            CountdownTurns,
            Creature,
            null,
            silent: false);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return;
        }

        // Leticia can die in the same kill batch, so defer one turn of async scheduling
        // before re-checking whether this encounter still owns a valid summon window.
        await Task.Yield();
        await SpawnFriendAtGiftSlotOnce();
    }

    public async Task TriggerCountdownDeath(PlayerChoiceContext choiceContext)
    {
        if (Creature.IsDead)
        {
            return;
        }

        await GrantGiftOnce();
        await CreatureCmd.Kill(Creature);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var moveOne = new MoveState(
            MoveOneId,
            MoveOne,
            new SingleAttackIntent(SmallDamage),
            new DetailedStatusCardIntent<LeticiaGift>(GiftCardCount, PileType.Hand, DetailedIntentScopeText.Target));

        var moveTwo = new MoveState(
            MoveTwoId,
            MoveTwo,
            new MultiAttackIntent(SmallDamage, MoveTwoHits));

        var moveThree = new MoveState(
            MoveThreeId,
            MoveThree,
            new DetailedStatusCardIntent<LeticiaGift>(GiftCardCount, PileType.Hand, DetailedIntentScopeText.Target),
            new DeathBlowIntent(() => SelfDestructDamage));

        moveOne.FollowUpState = moveTwo;
        moveTwo.FollowUpState = moveThree;
        moveThree.FollowUpState = moveOne;

        return new MonsterMoveStateMachine([moveOne, moveTwo, moveThree], moveOne);
    }

    private async Task MoveOne(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await AbnormalityAnimHelper.ExecuteAttackSegment(this, SmallDamage);

        await AddGiftsToPlayers(GiftCardCount);
    }

    private async Task MoveTwo(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < MoveTwoHits; i++)
        {
            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            await AbnormalityAnimHelper.ExecuteAttackSegment(this, SmallDamage);
        }
    }

    private async Task MoveThree(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(GiftCloseSfxPath, -2f);
        await AbnormalityAnimHelper.ExecuteAttackSegment(this, SelfDestructDamage);

        await AddGiftsToPlayers(GiftCardCount);
        await CreatureCmd.Kill(Creature);
    }

    private async Task GrantGiftOnce()
    {
        if (_giftGranted)
        {
            return;
        }

        _giftGranted = true;
        LocalOggOneShotPlayer.Play(GiftOpenSfxPath, -2f);
        await AddGiftsToPlayers(GiftCardCount);
    }

    private async Task AddGiftsToPlayers(int count)
    {
        IReadOnlyList<Creature> players = Creature.CombatState?.Players
            .Select(player => player.Creature)
            .Where(creature => creature.IsAlive)
            .ToArray() ?? [];

        if (players.Count == 0)
        {
            return;
        }

        LeticiaFilterOverlayController.PlayGiftOpenOverlay();
        await CardPileCmdCompat.AddToCombatAndPreview<LeticiaGift>(
            players,
            PileType.Hand,
            count,
            addedByPlayer: false);
    }

    private async Task SpawnFriendAtGiftSlotOnce()
    {
        if (_friendSpawned)
        {
            return;
        }

        string? slot = Creature.SlotName;
        CombatStateLike? combatState = Creature.CombatState;
        if (string.IsNullOrEmpty(slot) || combatState == null)
        {
            return;
        }

        if (!CanSpawnFriendAtGiftSlot(combatState, slot))
        {
            return;
        }

        _friendSpawned = true;
        LocalOggOneShotPlayer.Play(FriendSpawnSfxPath, -2f);
        var friend = (LittleWitchFriend)ModelDb.Monster<LittleWitchFriend>().ToMutable();
        friend.ConfigureInitialMoveForSlot(slot);
        Creature friendCreature = await CreatureCmd.Add(friend, combatState, CombatSide.Enemy, slot);
        await CreatureCmd.Stun(friendCreature, friend.ConfiguredInitialMoveId);
        Leticia.NotifyLittleWitchFriendSpawned(combatState);
    }

    private static bool CanSpawnFriendAtGiftSlot(CombatStateLike combatState, string slot)
    {
        if (!CombatManager.Instance.IsInProgress || CombatManager.Instance.IsOverOrEnding)
        {
            return false;
        }

        if (combatState.RunState.CurrentRoom is not CombatRoom room || !IsLeticiaEncounter(room))
        {
            return false;
        }

        if (!combatState.Enemies.Any(enemy => enemy.IsAlive && enemy.Monster is Leticia))
        {
            return false;
        }

        return !combatState.Enemies.Any(enemy =>
            enemy.IsAlive
            && enemy.SlotName == slot
            && enemy.Monster is LittleWitchFriend);
    }

    private static bool IsLeticiaEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is Leticia);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (!state.IsMove || state is not MoveState moveState)
            {
                continue;
            }

            foreach (AbstractIntent intent in moveState.Intents)
            {
                yield return intent;
            }
        }
    }
}

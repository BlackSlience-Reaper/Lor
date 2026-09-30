using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.abnormalities.Leticia;

internal enum LittleWitchFriendInitialMove
{
    Move1 = 1,
    Move2 = 2,
    Move3 = 3
}

public sealed class LittleWitchFriend : LorMonsterModel
{
    private const string MoveOneId = "GLITCH_FLUTTER";
    private const string MoveTwoId = "BROKEN_LAUGHTER";
    private const string MoveThreeId = "SNATCH_GIFT";

    private const int MoveOneHits = 2;
    private const int MoveTwoHits = 3;
    private const int FlawAmount = 1;
    private const int FlawTurns = 1;
    private const int BleedAmount = 1;

    private const string HitSfxPath = "res://audio/sfx/leticia/friend_hit.ogg";
    private const string PierceSfxPath = "res://audio/sfx/leticia/friend_pierce.ogg";
    private const string SpawnSfxPath = "res://audio/sfx/leticia/friend_spawn.ogg";
    private const string GiftCloseSfxPath = "res://audio/sfx/leticia/gift_close.ogg";

    private static readonly string[] ExtraAssetPaths =
    [
        HitSfxPath,
        PierceSfxPath,
        SpawnSfxPath,
        GiftCloseSfxPath,
        LeticiaFilterOverlayController.FilterTwoTexturePath
    ];

    private LittleWitchFriendInitialMove _initialMove = LittleWitchFriendInitialMove.Move1;

    private int SmallDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 1, 0);

    private int SnatchGiftDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 6);

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 27, 25);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 28, 26);

    public override int DefaultChaoResistance => 20;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
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
                LittleWitchFriendCreatureVisuals.Profile.AssetPaths.Count
                + ExtraAssetPaths.Length
                + 12);
            paths.AddRange(
                LittleWitchFriendCreatureVisuals.Profile.AssetPaths);
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
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, null, null, silent: true);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            Leticia.NotifyLittleWitchFriendRosterChanged(Creature.CombatState);
        }

        return Task.CompletedTask;
    }

    internal void ConfigureInitialMoveForSlot(string? slot)
    {
        AssertMutable();
        _initialMove = string.Equals(slot, "gift_right", StringComparison.Ordinal)
            ? LittleWitchFriendInitialMove.Move2
            : LittleWitchFriendInitialMove.Move1;
    }

    internal string ConfiguredInitialMoveId => _initialMove switch
    {
        LittleWitchFriendInitialMove.Move2 => MoveTwoId,
        LittleWitchFriendInitialMove.Move3 => MoveThreeId,
        _ => MoveOneId
    };

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var moveOne = new MoveState(
            MoveOneId,
            MoveOne,
            new BadgedAttackIntent(
                SmallDamage,
                MoveOneHits,
                IntentBadge.Flaw(FlawAmount, FlawTurns)));

        var moveTwo = new MoveState(
            MoveTwoId,
            MoveTwo,
            new BadgedAttackIntent(
                SmallDamage,
                MoveTwoHits,
                IntentBadge.Bleed(BleedAmount)));

        var moveThree = new MoveState(
            MoveThreeId,
            MoveThree,
            new BadgedAttackIntent(
                SnatchGiftDamage,
                IntentBadge.Strength()),
            new BuffIntent());

        moveOne.FollowUpState = moveTwo;
        moveTwo.FollowUpState = moveThree;
        moveThree.FollowUpState = moveOne;

        MonsterState initialState = _initialMove switch
        {
            LittleWitchFriendInitialMove.Move2 => moveTwo,
            LittleWitchFriendInitialMove.Move3 => moveThree,
            _ => moveOne
        };

        return new MonsterMoveStateMachine([moveOne, moveTwo, moveThree], initialState);
    }

    private async Task MoveOne(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < MoveOneHits; i++)
        {
            LocalOggOneShotPlayer.Play(HitSfxPath, -2f);
            await AbnormalityAnimHelper.ExecuteAttackSegment(this, SmallDamage);
        }

        if (targets.Count > 0)
        {
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(targets[0], FlawAmount, FlawTurns, Creature, null);
        }
    }

    private async Task MoveTwo(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < MoveTwoHits; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(PierceSfxPath, -2f);
            var command = await DamageCmd.Attack(SmallDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);

            IReadOnlyList<DamageResult> results = AttackCommandCompat.Results(command);
            IReadOnlyList<Creature> damagedTargets = results
                .Where(result => result.UnblockedDamage > 0)
                .Select(result => result.Receiver)
                .Distinct()
                .ToArray();

            if (damagedTargets.Count > 0)
            {
                await PowerCmdCompat.Apply<LibraryBleedingPower>(
                    damagedTargets,
                    BleedAmount,
                    Creature,
                    null);
            }
        }
    }

    private async Task MoveThree(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(SpawnSfxPath, -2f);
        LeticiaFilterOverlayController.PlayGiftCloseOverlay();
        await AbnormalityAnimHelper.TriggerCast(Creature);
        LocalOggOneShotPlayer.Play(GiftCloseSfxPath, -2f);
        await AbnormalityAnimHelper.ExecuteAttackSegment(this, SnatchGiftDamage);

        int maxRemovedFromOnePlayer = await RemoveLeticiaGiftsFromAllHands();
        if (maxRemovedFromOnePlayer > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(
                Creature,
                maxRemovedFromOnePlayer,
                Creature,
                null);
        }
    }

    private async Task<int> RemoveLeticiaGiftsFromAllHands()
    {
        int maxRemoved = 0;
        List<CardModel> giftsToRemove = [];

        foreach (Creature playerCreature in GetLivingPlayers())
        {
            if (playerCreature.Player == null)
            {
                continue;
            }

            List<CardModel> playerGifts = PileType.Hand
                .GetPile(playerCreature.Player)
                .Cards
                .Where(card => card is LeticiaGift)
                .ToList();

            maxRemoved = Math.Max(maxRemoved, playerGifts.Count);
            giftsToRemove.AddRange(playerGifts);
        }

        if (giftsToRemove.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(giftsToRemove);
        }

        return maxRemoved;
    }

    private IEnumerable<Creature> GetLivingPlayers()
    {
        return Creature.CombatState?.Players
            .Select(player => player.Creature)
            .Where(creature => creature.IsAlive)
            .ToArray() ?? [];
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

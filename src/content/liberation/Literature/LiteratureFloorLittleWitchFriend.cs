using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.liberation.Literature;

internal enum LiteratureFloorLittleWitchFriendInitialMove
{
    Move1 = 1,
    Move2 = 2,
    Move3 = 3
}

public sealed class LiteratureFloorLittleWitchFriend : LorMonsterModel
{
    private const string MoveOneId = "GLITCH_FLUTTER";
    private const string MoveTwoId = "BROKEN_LAUGHTER";
    private const string MoveThreeId = "SNATCH_GIFT";

    private const int MoveOneBleed = 3;
    private const int MoveTwoHits = 4;

    private const string HitSfxPath =
        "res://audio/sfx/leticia/friend_hit.ogg";
    private const string PierceSfxPath =
        "res://audio/sfx/leticia/friend_pierce.ogg";
    private const string GiftCloseSfxPath =
        "res://audio/sfx/leticia/gift_close.ogg";

    private LiteratureFloorLittleWitchFriendInitialMove _initialMove =
        LiteratureFloorLittleWitchFriendInitialMove.Move1;

    private int MoveOneDamage => AscensionHelper.GetValueIfAscension(
        AscensionLevel.DeadlyEnemies,
        8,
        7);

    private int MoveTwoDamage => AscensionHelper.GetValueIfAscension(
        AscensionLevel.DeadlyEnemies,
        3,
        2);

    internal static int DebugMoveOneDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 9 : 7;

    internal static int DebugMoveTwoDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 3 : 2;

    public override int MinInitialHp => 70;

    public override int MaxInitialHp => 70;

    public override int DefaultChaoResistance => 40;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData =>
        new()
        {
            Slash = LibraryResistanceLevel.Endure,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Vulnerable
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData =>
        new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Vulnerable
        };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                LiteratureFloorLittleWitchFriendCreatureVisuals.ScenePath,
                HitSfxPath,
                PierceSfxPath,
                GiftCloseSfxPath,
                LeticiaFilterOverlayController.FilterTwoTexturePath,
                "res://images/powers/library_passive_green.png"
            };
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
        await PowerCmdCompat.Apply<
            LiteratureFloorLittleWitchFriendHandItOverPassivePower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<MinionPower>(
            Creature,
            1,
            null,
            null,
            silent: true);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented
            && creature == Creature
            && creature.CombatState?.Enemies
                .Where(static enemy => enemy.IsAlive)
                .Select(static enemy => enemy.Monster)
                .OfType<LiteratureFloorLaetitiaBoss>()
                .FirstOrDefault() is { } laetitia)
        {
            await laetitia.RefreshLonelyResistance();
        }
    }

    internal void ConfigureInitialMoveForSlot(string? slot)
    {
        AssertMutable();
        _initialMove = string.Equals(
            slot,
            "gift_right",
            StringComparison.Ordinal)
            ? LiteratureFloorLittleWitchFriendInitialMove.Move2
            : LiteratureFloorLittleWitchFriendInitialMove.Move1;
    }

    internal string ConfiguredInitialMoveId => _initialMove switch
    {
        LiteratureFloorLittleWitchFriendInitialMove.Move2 => MoveTwoId,
        LiteratureFloorLittleWitchFriendInitialMove.Move3 => MoveThreeId,
        _ => MoveOneId
    };

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var moveOne = new MoveState(
            MoveOneId,
            MoveOne,
            new BadgedAttackIntent(
                MoveOneDamage,
                IntentBadge.Bleed(MoveOneBleed)));
        var moveTwo = new MoveState(
            MoveTwoId,
            MoveTwo,
            new MultiAttackIntent(MoveTwoDamage, MoveTwoHits));
        var moveThree = new MoveState(
            MoveThreeId,
            MoveThree,
            new BuffIntent());

        moveOne.FollowUpState = moveTwo;
        moveTwo.FollowUpState = moveThree;
        moveThree.FollowUpState = moveOne;

        MonsterState initialState = _initialMove switch
        {
            LiteratureFloorLittleWitchFriendInitialMove.Move2 => moveTwo,
            LiteratureFloorLittleWitchFriendInitialMove.Move3 => moveThree,
            _ => moveOne
        };
        return new MonsterMoveStateMachine(
            [moveOne, moveTwo, moveThree],
            initialState);
    }

    private async Task MoveOne(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(HitSfxPath, -2f);
        await DamageCmd.Attack(MoveOneDamage)
            .FromMonster(this)
            .WithAttackerAnim(
                "Attack",
                LiteratureFloorLittleWitchFriendAnimationContract
                    .AttackDurationSeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        Creature[] livingTargets = targets
            .Where(static target => target.IsAlive)
            .Distinct()
            .ToArray();
        if (livingTargets.Length > 0)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(
                livingTargets,
                MoveOneBleed,
                Creature,
                null);
        }
    }

    private async Task MoveTwo(IReadOnlyList<Creature> targets)
    {
        for (int hit = 0; hit < MoveTwoHits && Creature.IsAlive; hit++)
        {
            LocalOggOneShotPlayer.Play(PierceSfxPath, -2f);
            await DamageCmd.Attack(MoveTwoDamage)
                .FromMonster(this)
                .WithAttackerAnim(
                    hit % 2 == 0 ? "Attack" : "AttackAlt",
                    LiteratureFloorLittleWitchFriendAnimationContract
                        .AttackDurationSeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }
    }

    private async Task MoveThree(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(GiftCloseSfxPath, -2f);
        LeticiaFilterOverlayController.PlayGiftCloseOverlay();
        await CreatureCmd.TriggerAnim(
            Creature,
            "Cast",
            LiteratureFloorLittleWitchFriendAnimationContract
                .CastDurationSeconds);

        int maxRemovedFromOnePlayer = await RemoveGiftsFromAllHands();
        if (maxRemovedFromOnePlayer > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(
                Creature,
                maxRemovedFromOnePlayer,
                Creature,
                null);
        }
    }

    private async Task<int> RemoveGiftsFromAllHands()
    {
        int maxRemoved = 0;
        var giftsToRemove = new List<CardModel>();
        foreach (Creature player in Creature.CombatState?.PlayerCreatures
                     .Where(static creature => creature.IsAlive)
                     ?? [])
        {
            if (player.Player == null)
            {
                continue;
            }

            CardModel[] playerGifts = PileType.Hand
                .GetPile(player.Player)
                .Cards
                .Where(static card => card is LeticiaGift)
                .ToArray();
            maxRemoved = Math.Max(maxRemoved, playerGifts.Length);
            giftsToRemove.AddRange(playerGifts);
        }

        if (giftsToRemove.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(giftsToRemove);
        }

        return maxRemoved;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MoveState move in stateMachine.States.Values
                     .OfType<MoveState>())
        {
            foreach (AbstractIntent intent in move.Intents)
            {
                yield return intent;
            }
        }
    }
}

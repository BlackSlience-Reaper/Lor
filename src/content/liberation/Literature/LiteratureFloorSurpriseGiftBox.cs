using LibraryLib.Models;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.content.liberation.Literature;

public sealed class LiteratureFloorSurpriseGiftBox : LorMonsterModel, ILibraryAbstractModel
{
    private const string MoveOneId = "EE_YO_LI_WOO";
    private const string MoveTwoId = "COUGH_OMM_JJI_AO";
    private const string MoveThreeId = "PU_AO_JJI_AH";
    private const string RouterStateId = "GIFT_BOX_COUNTDOWN_ROUTER";

    private const int MoveTwoHits = 3;
    private const int DiscardGiftCards = 1;
    private const int SelfDestructGiftCards = 3;

    private bool _selfDestructStarted;
    private bool _selfDestructCompleted;
    private bool _friendScheduled;

    private int SmallDamage => AscensionHelper.GetValueIfAscension(
        AscensionLevel.DeadlyEnemies,
        3,
        2);

    private int SelfDestructDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            19,
            16);

    internal static (int Min, int Max) DebugHpRange(bool toughEnemies) =>
        toughEnemies ? (38, 40) : (30, 34);

    internal static int DebugSmallDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 3 : 2;

    internal static int DebugSelfDestructDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 19 : 16;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(
        AscensionLevel.ToughEnemies,
        38,
        30);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(
        AscensionLevel.ToughEnemies,
        40,
        34);

    public override int DefaultChaoResistance => 20;

    public override float DeathAnimLengthOverride => LayeredBossSpine.DeathLength(this);

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => CreateResistance();

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => CreateResistance();

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                LiteratureFloorGiftBoxCreatureVisuals.ScenePath,
                LiteratureFloorAssets.FriendHitSfx,
                LiteratureFloorAssets.GiftOpenSfx,
                LiteratureFloorAssets.GiftCloseSfx,
                LeticiaFilterOverlayController.FilterOneTexturePath,
                LiteratureFloorAssets.LibraryPassiveGreenIcon
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
        _selfDestructStarted = false;
        _selfDestructCompleted = false;
        _friendScheduled = false;
        await PowerCmdCompat.Apply<LiteratureFloorGiftBoxBoomPassivePower>(
            Creature,
            LiteratureFloorGiftBoxBoomPassivePower.SurvivalTurns,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Apply<
            LiteratureFloorSurpriseAppearancePassivePower>(
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
        if (wasRemovalPrevented
            || creature != Creature
            || _friendScheduled)
        {
            return;
        }

        _friendScheduled = true;
        if (creature.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter)
        {
            await encounter.OnGiftBoxDeath(
                creature,
                weakenedFriend: !_selfDestructCompleted);
        }
    }

    public Task AfterStun(Creature creature)
    {
        if (creature != Creature
            || MoveStateMachine is not { } stateMachine)
        {
            return Task.CompletedTask;
        }

        if (NextMove is { Id: "STUNNED" } stunnedMove
            && stateMachine.States.TryGetValue(
                RouterStateId,
                out MonsterState? router))
        {
            stunnedMove.FollowUpState = router;
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var moveOne = new MoveState(
            MoveOneId,
            MoveOne,
            new DetailedStatusCardIntent<LeticiaGift>(
                DiscardGiftCards,
                PileType.Discard,
                DetailedIntentScopeText.Target));
        var moveTwo = new MoveState(
            MoveTwoId,
            MoveTwo,
            new MultiAttackIntent(SmallDamage, MoveTwoHits),
            new DetailedStatusCardIntent<LeticiaGift>(
                DiscardGiftCards,
                PileType.Discard,
                DetailedIntentScopeText.Target));
        var moveThree = new MoveState(
            MoveThreeId,
            MoveThree,
            new DeathBlowIntent(() => SelfDestructDamage),
            new DetailedStatusCardIntent<LeticiaGift>(
                SelfDestructGiftCards,
                PileType.Draw,
                DetailedIntentScopeText.Target));
        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (owner, _) => ResolveCountdownMoveId(owner));
        moveOne.FollowUpState = router;
        moveTwo.FollowUpState = router;
        moveThree.FollowUpState = router;
        return new MonsterMoveStateMachine(
            [moveOne, moveTwo, moveThree, router],
            router);
    }

    private static string ResolveCountdownMoveId(Creature owner) =>
        owner.GetPower<LiteratureFloorGiftBoxBoomPassivePower>()?.Amount
        switch
        {
            <= 1 => MoveThreeId,
            2 => MoveTwoId,
            _ => MoveOneId
        };

    private async Task MoveOne(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(LiteratureFloorAssets.GiftOpenSfx, -2f);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Cast",
            LiteratureFloorGiftBoxAnimationContract.CastDurationSeconds);
        await AddGiftsToPlayers(DiscardGiftCards, PileType.Discard);
    }

    private async Task MoveTwo(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < MoveTwoHits && Creature.IsAlive; i++)
        {
            LocalOggOneShotPlayer.Play(LiteratureFloorAssets.FriendHitSfx, -2f);
            await DamageCmd.Attack(SmallDamage)
                .FromMonster(this)
                .WithAttackerAnim(
                    "Attack",
                    LiteratureFloorGiftBoxAnimationContract
                        .AttackDurationSeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
        }

        await AddGiftsToPlayers(DiscardGiftCards, PileType.Discard);
    }

    private Task MoveThree(IReadOnlyList<Creature> targets) =>
        ExecuteSelfDestruct();

    private async Task ExecuteSelfDestruct()
    {
        if (_selfDestructStarted || Creature.IsDead)
        {
            return;
        }

        _selfDestructStarted = true;
        LocalOggOneShotPlayer.Play(LiteratureFloorAssets.GiftCloseSfx, -1f);
        LeticiaFilterOverlayController.PlayGiftCloseOverlay();
        await CreatureCmd.TriggerAnim(Creature, "SelfDestruct", 0f);
        await Cmd.Wait(
            LiteratureFloorGiftBoxAnimationContract
                .SelfDestructDamageDelaySeconds);
        await DamageCmd.Attack(SelfDestructDamage)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
        await AddGiftsToPlayers(
            SelfDestructGiftCards,
            PileType.Draw);
        _selfDestructCompleted = true;
        await CreatureCmd.Kill(Creature);
    }

    private async Task AddGiftsToPlayers(int count, PileType pileType)
    {
        Creature[] players = Creature.CombatState?.LivingPlayerCreatures()
            .ToArray() ?? [];
        if (players.Length == 0)
        {
            return;
        }

        LeticiaFilterOverlayController.PlayGiftOpenOverlay();
        await CardPileCmdCompat.AddToCombatAndPreview<LeticiaGift>(
            players,
            pileType,
            count,
            addedByPlayer: false);
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

    private static LibraryCreatureResistanceData.Resistance
        CreateResistance() =>
        new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Vulnerable
        };
}

using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Leticia;

public sealed class Leticia : LorMonsterModel
{
    private const string SendGiftMoveId = "SEND_GIFT";
    private const string DontGetHurtMoveId = "DONT_GET_HURT";
    private const string HaveFunMoveId = "HAVE_FUN";
    private const string ItsAGiftMoveId = "ITS_A_GIFT";

    private const int SendGiftBlock = 9;
    private const int HealOtherEnemies = 9;
    private const int DontGetHurtBlock = 6;
    private const int HaveFunStrength = 3;
    private const int HaveFunGuard = 2;
    private const int HaveFunGuardTurns = 3;
    private const int HaveFunBlock = 11;
    private const int HaveFunWeak = 2;

    private int ItsAGiftStrength =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 9, 8);

    private const int GiftCardCount = 2;

    private static readonly string[] ExtraAssetPaths =
    [
        LeticiaAssets.LeticiaAttackSfx,
        LeticiaAssets.LeticiaGuardSfx,
        LeticiaAssets.GiftOpenSfx,
        LeticiaFilterOverlayController.FilterOneTexturePath
    ];

    private static readonly string[] DefaultBackgroundTextLineKeys =
    [
        "LETICIA.backgroundText.default.0",
        "LETICIA.backgroundText.default.1",
        "LETICIA.backgroundText.default.2",
        "LETICIA.backgroundText.default.3",
        "LETICIA.backgroundText.default.4",
        "LETICIA.backgroundText.default.5",
    ];

    private static readonly string[] WithFriendsBackgroundTextLineKeys =
    [
        "LETICIA.backgroundText.withFriends.0",
        "LETICIA.backgroundText.withFriends.1",
        "LETICIA.backgroundText.withFriends.2",
        "LETICIA.backgroundText.withFriends.3",
        "LETICIA.backgroundText.withFriends.4",
    ];

    private static readonly string[] LonelyBackgroundTextLineKeys =
    [
        "LETICIA.backgroundText.lonely.0",
        "LETICIA.backgroundText.lonely.1",
        "LETICIA.backgroundText.lonely.2",
        "LETICIA.backgroundText.lonely.3",
        "LETICIA.backgroundText.lonely.4",
    ];

    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private static readonly string LeticiaPageRelicTitleLocKey =
        $"{ModelDb.GetId<LeticiaPageRelic>().Entry}.title";

    private bool _backgroundMoonTextLoopStarted;
    private bool _friendsEverSpawnedThisCombat;

    private int SendGiftDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 94, 91);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 96, 93);

    public override int DefaultChaoResistance => 60;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                LeticiaCreatureVisuals.Profile.AssetPaths.Count
                + ExtraAssetPaths.Length
                + 16);
            paths.AddRange(LeticiaCreatureVisuals.Profile.AssetPaths);
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
        _backgroundMoonTextLoopStarted = false;
        _friendsEverSpawnedThisCombat = false;
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead && !_backgroundMoonTextLoopStarted)
        {
            _backgroundMoonTextLoopStarted = true;
            _friendsEverSpawnedThisCombat = false;
            StartBackgroundMoonTextLoopForCurrentState();
        }

        return Task.CompletedTask;
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return Task.CompletedTask;
        }

        AddLeticiaPageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    internal static void NotifyLittleWitchFriendRosterChanged(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (Creature enemy in combatState.Enemies)
        {
            if (enemy.Monster is Leticia leticia && enemy.IsAlive)
            {
                leticia.RefreshBackgroundMoonTextLoop();
                return;
            }
        }
    }

    internal static void NotifyLittleWitchFriendSpawned(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (Creature enemy in combatState.Enemies)
        {
            if (enemy.Monster is Leticia leticia && enemy.IsAlive)
            {
                leticia._friendsEverSpawnedThisCombat = true;
                leticia.RefreshBackgroundMoonTextLoop();
                return;
            }
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var sendGift = new MoveState(
            SendGiftMoveId,
            SendGiftMove,
            new SingleAttackIntent(SendGiftDamage),
            new DetailedStatusCardIntent<LeticiaGift>(GiftCardCount, PileType.Draw, DetailedIntentScopeText.Target),
            new DefendIntent());

        var dontGetHurt = new MoveState(
            DontGetHurtMoveId,
            DontGetHurtMove,
            new HealIntent(),
            new DefendIntent());

        var haveFun = new MoveState(
            HaveFunMoveId,
            HaveFunMove,
            new DetailedBuffGroupIntent(
                DetailedIntentVisualEffect.FromBadge(
                    IntentBadge.Strength(HaveFunStrength),
                    DetailedIntentScopeText.OtherEnemies),
                DetailedIntentVisualEffect.FromBadge(
                    IntentBadge.Guard(HaveFunGuard, HaveFunGuardTurns),
                    DetailedIntentScopeText.OtherEnemies)),
            new DebuffIntent(strong: true));

        var itsAGift = new MoveState(
            ItsAGiftMoveId,
            ItsAGiftMove,
            new BuffIntent(),
            new DetailedStatusCardIntent<LeticiaGift>(GiftCardCount, PileType.Draw, DetailedIntentScopeText.Target));

        sendGift.FollowUpState = dontGetHurt;
        dontGetHurt.FollowUpState = haveFun;
        haveFun.FollowUpState = itsAGift;
        itsAGift.FollowUpState = sendGift;

        return new MonsterMoveStateMachine(
            [sendGift, dontGetHurt, haveFun, itsAGift],
            sendGift);
    }

    private async Task SendGiftMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(LeticiaAssets.LeticiaAttackSfx, -2f);
        await AbnormalityAnimHelper.ExecuteAttackSegment(this, SendGiftDamage);

        await AddGiftsToPlayers(GiftCardCount);
        await CreatureCmd.GainBlock(Creature, SendGiftBlock, ValueProp.Move, null);
    }

    private async Task DontGetHurtMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(LeticiaAssets.LeticiaAttackSfx, -2f);
        await AbnormalityAnimHelper.TriggerCast(Creature);

        foreach (Creature enemy in LivingOtherEnemies())
        {
            await CreatureCmd.Heal(enemy, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(enemy, HealOtherEnemies));
        }

        await CreatureCmd.GainBlock(Creature, DontGetHurtBlock, ValueProp.Move, null);
    }

    private async Task HaveFunMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(LeticiaAssets.LeticiaGuardSfx, -2f);
        await AbnormalityAnimHelper.TriggerCast(Creature);

        IReadOnlyList<Creature> otherEnemies = LivingOtherEnemies().ToArray();
        if (otherEnemies.Count > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(otherEnemies, HaveFunStrength, Creature, null);
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                new ThrowingPlayerChoiceContext(),
                otherEnemies,
                HaveFunGuard,
                HaveFunGuardTurns,
                IsPermanent: false,
                Creature,
                null);
        }

        await CreatureCmd.GainBlock(Creature, HaveFunBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<WeakPower>(LivingPlayers(), HaveFunWeak, Creature, null);
    }

    private async Task ItsAGiftMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(LeticiaAssets.GiftOpenSfx, -2f);
        await AbnormalityAnimHelper.TriggerCast(Creature);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, ItsAGiftStrength, Creature, null);
        await AddGiftsToPlayers(GiftCardCount);
    }

    private async Task AddGiftsToPlayers(int count)
    {
        IReadOnlyList<Creature> players = LivingPlayers().ToArray();
        if (players.Count == 0)
        {
            return;
        }

        LeticiaFilterOverlayController.PlayGiftOpenOverlay();
        await CardPileCmdCompat.AddToCombatAndPreview<LeticiaGift>(
            players,
            PileType.Draw,
            count,
            addedByPlayer: false);
    }

    private IEnumerable<Creature> LivingPlayers()
    {
        return Creature.CombatState?.Players
            .Select(player => player.Creature)
            .Where(creature => creature.IsAlive)
            .ToArray() ?? [];
    }

    private IEnumerable<Creature> LivingOtherEnemies()
    {
        return Creature.CombatState?.Enemies
            .Where(enemy => enemy.IsAlive && enemy != Creature)
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

    private void RefreshBackgroundMoonTextLoop()
    {
        if (Creature.IsDead || !_backgroundMoonTextLoopStarted)
        {
            return;
        }

        StartBackgroundMoonTextLoopForCurrentState();
    }

    private void StartBackgroundMoonTextLoopForCurrentState()
    {
        CombatStateLike? combatState = Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        IReadOnlyList<string> lineKeys = ResolveBackgroundTextLineKeys(combatState);
        MonsterMoonTextLoop.Start(lineKeys, BackgroundTextIntervalSeconds, BackgroundTextSpawnArea);
    }

    private IReadOnlyList<string> ResolveBackgroundTextLineKeys(CombatStateLike combatState)
    {
        bool hasLivingFriend = combatState.Enemies.Any(enemy =>
            enemy.IsAlive && enemy.Monster is LittleWitchFriend);

        if (hasLivingFriend)
        {
            return WithFriendsBackgroundTextLineKeys;
        }

        if (_friendsEverSpawnedThisCombat)
        {
            return LonelyBackgroundTextLineKeys;
        }

        return DefaultBackgroundTextLineKeys;
    }

    private void AddLeticiaPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room || !IsLeticiaEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<LeticiaPageRelic>(room, LeticiaPageRelicTitleLocKey);
    }

    private static bool IsLeticiaEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is Leticia);
    }
}

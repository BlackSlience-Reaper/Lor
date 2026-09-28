using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.GalaxyChild;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.GalaxyChild;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.GalaxyChild;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.GalaxyChild;
using LibraryOfRuina.visuals.GalaxyChild;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.GalaxyChild;

internal enum GalaxyFriendInitialMove
{
    Wait,
    StarlightFall,
    Twinkle
}

public sealed class GalaxyFriend : LibraryMonsterModel
{
    public const string IdleTexturePath = "res://images/monsters/galaxy_friend/idle.png";
    public const string AttackTexturePath = "res://images/monsters/galaxy_friend/attack.png";
    public const string HitTexturePath = "res://images/monsters/galaxy_friend/hit.png";
    public const string ParryTexturePath = "res://images/monsters/galaxy_friend/parry.png";
    public const string AttackSfxPath = "res://audio/sfx/galaxy_child/attack.ogg";
    public const string HealSfxPath = "res://audio/sfx/galaxy_child/heal.ogg";
    public const string ParrySfxPath = "res://audio/sfx/galaxy_child/parry.ogg";

    private const string WaitMoveId = "WAIT";
    private const string StarlightFallMoveId = "STARLIGHT_FALL";
    private const string TwinkleMoveId = "TWINKLE";
    private const string StunnedMoveId = "STUNNED";
    private const string FakeDeathHiddenMoveId = "FAKE_DEATH_HIDDEN";
    private const string FakeDeathReviveMoveId = "FAKE_DEATH_REVIVE";

    private const int WaitBlock = 18;
    private const int StarlightHits = 3;
    private const int TwinkleHeal = 9;
    private const int TwinkleVulnerable = 2;
    private const int TwinkleStrength = 2;
    public const int FakeDeathHp = 0;
    public const int FakeDeathStunTurns = 2;
    private const int MinimumRecoveryHp = 1;
    private const float SegmentDelaySeconds = 0.48f;
    private const float BackgroundTextIntervalSeconds = 5f;

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "GALAXY_FRIEND.backgroundText.normal.0",
        "GALAXY_FRIEND.backgroundText.normal.1",
        "GALAXY_FRIEND.backgroundText.normal.2"
    ];

    private static readonly string[] FakeDeathBackgroundTextLineKeys =
    [
        "GALAXY_FRIEND.backgroundText.fakeDeath.0",
        "GALAXY_FRIEND.backgroundText.fakeDeath.1",
        "GALAXY_FRIEND.backgroundText.fakeDeath.2"
    ];

    private static readonly object BackgroundTextOwner = typeof(GalaxyFriend);
    private const string NormalBackgroundTextScope = "galaxy_child_normal";
    private const string FakeDeathBackgroundTextScope = "galaxy_child_fake_death";

    private static readonly string GalaxyChildPageRelicTitleLocKey =
        $"{ModelDb.GetId<GalaxyChildPageRelic>().Entry}.title";

    private GalaxyFriendInitialMove _initialMove = GalaxyFriendInitialMove.Wait;
    private bool _isFakeDead;
    private bool _isResolvingPartingTearsVictory;
    private int _fakeDeathPlayerTurnStunsRemaining;
    private string _fakeDeathRecoveryMoveId = WaitMoveId;
    private MoveState _waitState = null!;
    private MoveState _starlightState = null!;
    private MoveState _twinkleState = null!;
    private MoveState _fakeDeathHiddenState = null!;

    private int StarlightDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 4);

    public bool IsFakeDead => _isFakeDead;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 160, 139);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 170, 144);

    public override int DefaultChaoResistance => 120;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Vulnerable,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Vulnerable,
        Pierce = LibraryResistanceLevel.Fatal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                GalaxyFriendCreatureVisuals.Profile.AssetPaths)
            {
                AttackSfxPath,
                HealSfxPath,
                ParrySfxPath
            };
            paths.AddRange(GalaxyChildBackgroundController.AssetPaths);

            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    internal void ConfigureInitialMove(GalaxyFriendInitialMove initialMove)
    {
        AssertMutable();
        _initialMove = initialMove;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        ResetFakeDeathState();
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<GalaxyChildPebblePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<GalaxyDoNotLeaveMePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<GalaxyPartingTearsPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead)
        {
            GalaxyChildBackgroundController.Reset();
            StartNormalBackgroundTextLoop();
        }

        return Task.CompletedTask;
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        StopAllGalaxyChildPresentation();
        base.BeforeRemovedFromRoom();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        StopAllGalaxyChildPresentation();
        ResetFakeDeathState();
        return Task.CompletedTask;
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature || _isFakeDead)
        {
            return Task.CompletedTask;
        }

        AddGalaxyChildPageRewardsFromDeathHook(creature);
        if (!GetFriends(creature.CombatState).Any(static friend => friend.Creature.IsAlive || friend._isFakeDead))
        {
            StopAllGalaxyChildPresentation();
        }

        return Task.CompletedTask;
    }

    public bool CanEnterFakeDeath(Creature creature)
    {
        return creature == Creature
            && !_isFakeDead
            && !_isResolvingPartingTearsVictory
            && !CombatManager.Instance.IsOverOrEnding
            && creature.CombatState?.Encounter is GalaxyChildWeak;
    }

    public async Task EnterFakeDeath()
    {
        if (_isFakeDead)
        {
            return;
        }

        _fakeDeathRecoveryMoveId = ResolveRecoveryMoveId();
        _fakeDeathPlayerTurnStunsRemaining = Math.Max(0, FakeDeathStunTurns);
        _isFakeDead = true;

        await FakeDeathDebuffHelper.ClearDebuffs(Creature);
        ForceFakeDeathChaoZero();
        ForceFakeDeathHiddenIntent();

        GalaxyChildBackgroundController.SetFakeDeathMode(true);
        StartFakeDeathBackgroundTextLoop();
        // Parting Tears victory is triggered once the kill batch completes
        // (GalaxyChildPartingTearsKillBatchPatch); player turn start is the fallback.
    }

    public async Task TickFakeDeathOnPlayerTurnStart()
    {
        if (!_isFakeDead)
        {
            return;
        }

        if (ShouldTriggerPartingTears(Creature.CombatState))
        {
            await TriggerPartingTearsVictory(Creature.CombatState);
            return;
        }

        ForceFakeDeathChaoZero();
        if (_fakeDeathPlayerTurnStunsRemaining > 1)
        {
            _fakeDeathPlayerTurnStunsRemaining--;
            ForceFakeDeathHiddenIntent();
            return;
        }

        if (_fakeDeathPlayerTurnStunsRemaining == 1)
        {
            _fakeDeathPlayerTurnStunsRemaining = 0;
            ForceFakeDeathReviveIntent();
            return;
        }

        ForceFakeDeathHiddenIntent();
    }

    public static bool ShouldTriggerPartingTears(CombatStateLike? combatState)
    {
        IReadOnlyList<GalaxyFriend> friends = GetFriends(combatState).ToArray();
        return friends.Count == 2
            && friends.All(static friend => friend.IsDeadOrFakeDeadForPartingTears());
    }

    public static bool ShouldHoldCombatOpenForFakeDeath(CombatStateLike? combatState)
    {
        IReadOnlyList<GalaxyFriend> friends = GetFriends(combatState).ToArray();
        return friends.Count == 2
            && friends.Any(static friend => friend._isFakeDead)
            && friends.Any(static friend => friend.Creature.IsAlive && !friend._isFakeDead);
    }

    public static bool AreAllOtherFriendsDeadOrFakeDead(CombatStateLike? combatState, GalaxyFriend owner)
    {
        return GetFriends(combatState)
            .Where(friend => !ReferenceEquals(friend, owner))
            .All(static friend => friend.IsDeadOrFakeDeadForPartingTears());
    }

    public static async Task TriggerPartingTearsVictory(CombatStateLike? combatState)
    {
        IReadOnlyList<GalaxyFriend> friends = GetFriends(combatState).ToArray();
        if (friends.Count != 2
            || friends.Any(static friend => !friend.IsDeadOrFakeDeadForPartingTears())
            || friends.Any(static friend => friend._isResolvingPartingTearsVictory))
        {
            return;
        }

        StopAllGalaxyChildPresentation();
        Log.Warn("[GalaxyFriend] forcing Parting Tears victory after both friends entered fake death.");
        foreach (GalaxyFriend friend in friends)
        {
            friend._isResolvingPartingTearsVictory = true;
            friend.ResetFakeDeathState(clearPartingTearsVictoryState: false);
        }

        await CreatureCmd.Kill(friends.Select(static friend => friend.Creature).ToArray(), force: true);
        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }
    }

    public static bool IsCreatureFakeDead(Creature creature)
    {
        return creature.Monster is GalaxyFriend friend && friend._isFakeDead;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _waitState = new MoveState(
            WaitMoveId,
            WaitMove,
            new DefendIntent());

        _starlightState = new MoveState(
            StarlightFallMoveId,
            StarlightFallMove,
            new MultiAttackIntent(StarlightDamage, StarlightHits));

        _twinkleState = new MoveState(
            TwinkleMoveId,
            TwinkleMove,
            new HealIntent(),
            new DebuffIntent(),
            new DetailedBuffIntent<StrengthPower>(TwinkleStrength));

        _fakeDeathHiddenState = new MoveState(
            FakeDeathHiddenMoveId,
            static _ => Task.CompletedTask,
            new HiddenIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };

        _waitState.FollowUpState = _starlightState;
        _starlightState.FollowUpState = _twinkleState;
        _twinkleState.FollowUpState = _waitState;
        _fakeDeathHiddenState.FollowUpState = _fakeDeathHiddenState;

        MonsterState initialState = _initialMove switch
        {
            GalaxyFriendInitialMove.StarlightFall => _starlightState,
            GalaxyFriendInitialMove.Twinkle => _twinkleState,
            _ => _waitState
        };

        return new MonsterMoveStateMachine(
            [_waitState, _starlightState, _twinkleState, _fakeDeathHiddenState],
            initialState);
    }

    private async Task WaitMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(ParrySfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Parry", SegmentDelaySeconds);
        await CreatureCmd.GainBlock(Creature, WaitBlock, ValueProp.Move, null);
    }

    private async Task StarlightFallMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < StarlightHits; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            await AbnormalityAnimHelper.ExecuteAttackSegment(
                this,
                StarlightDamage,
                animId: "Attack",
                delaySeconds: SegmentDelaySeconds);
        }
    }

    private async Task TwinkleMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(HealSfxPath, -2f);
        await AbnormalityAnimHelper.TriggerCast(Creature, SegmentDelaySeconds);

        Creature? healTarget = LivingOtherFriends()
            .OrderBy(static creature => creature.CurrentHp)
            .FirstOrDefault();
        if (healTarget != null)
        {
            await CreatureCmd.Heal(healTarget, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(healTarget, TwinkleHeal));
        }

        IReadOnlyList<Creature> playerTargets = Creature.CombatState?.Players
            .Select(static player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .ToArray() ?? [];
        if (playerTargets.Count > 0)
        {
            await PowerCmdCompat.ApplyDebuff<VulnerablePower>(
                playerTargets,
                TwinkleVulnerable,
                Creature,
                null);
        }

        await PowerCmdCompat.Apply<StrengthPower>(Creature, TwinkleStrength, Creature, null);
    }

    private async Task RecoverFromFakeDeath()
    {
        if (!_isFakeDead)
        {
            return;
        }

        Creature? other = LivingOtherFriends()
            .FirstOrDefault(creature => creature.Monster is GalaxyFriend friend && !friend._isFakeDead);
        int targetHp = Math.Max(MinimumRecoveryHp, other?.CurrentHp ?? MinimumRecoveryHp);

        ResetFakeDeathState();
        await CreatureCmd.SetCurrentHp(Creature, targetHp);
        await RestoreChaoAfterFakeDeath();

        if (!GetFriends(Creature.CombatState).Any(static friend => friend._isFakeDead))
        {
            GalaxyChildBackgroundController.SetFakeDeathMode(false);
            StartNormalBackgroundTextLoop();
        }
    }

    private void ForceFakeDeathChaoZero()
    {
        if (Creature is not LibraryCreature lc || !lc.HasChaoResistance)
        {
            return;
        }

        if (lc.CurrentChaoValue != 0)
        {
            // Doom can invoke fake death while death hooks are still unwinding.
            lc.SetCurrentChaoValueInternal(0m);
        }

        RefreshChaoBar(lc);
    }

    private void ForceFakeDeathHiddenIntent()
    {
        TrySetFakeDeathMove(_fakeDeathHiddenState);
    }

    private void ForceFakeDeathReviveIntent()
    {
        MoveState recoveryState = ResolveRecoveryState();
        TrySetFakeDeathMove(CreateFakeDeathReviveMove(recoveryState));
    }

    private void TrySetFakeDeathMove(MoveState state)
    {
        if (CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        try
        {
            Creature.Monster?.SetMoveImmediate(state, forceTransition: true);
        }
        catch (NullReferenceException exception) when (CombatManager.Instance.IsOverOrEnding)
        {
            Log.Warn("[GalaxyFriend] skipped fake-death move update after combat end: " + exception.Message);
        }
    }

    private async Task RestoreChaoAfterFakeDeath()
    {
        if (Creature is not LibraryCreature lc || !lc.HasChaoResistance)
        {
            return;
        }

        if (lc.IsChaoed)
        {
            lc.RestoreChaoOnNextOwnerTurn = false;
            lc.RestorePreStunResistance();
        }

        if (lc.CurrentChaoValue < lc.MaxChaoValue)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(lc, lc.MaxChaoValue);
        }

        RefreshChaoBar(lc);
    }

    private static void RefreshChaoBar(LibraryCreature creature)
    {
        creature.HealthBar?.RefreshValues();
    }

    private MoveState CreateFakeDeathReviveMove(MoveState recoveryState)
    {
        return new LibraryPhaseTransitionMoveState(
            FakeDeathReviveMoveId,
            ReviveAndPassFakeDeathTurn,
            new HealIntent(),
            new BuffIntent())
        {
            FollowUpState = ResolveFollowUpState(recoveryState),
            MustPerformOnceBeforeTransitioning = true
        };
    }

    private Task ReviveAndPassFakeDeathTurn(IReadOnlyList<Creature> _) => RecoverFromFakeDeath();

    private IEnumerable<Creature> LivingOtherFriends(bool includeFakeDead = false)
    {
        return Creature.CombatState?.Enemies
            .Where(enemy =>
                enemy != Creature
                && enemy.Monster is GalaxyFriend friend
                && (enemy.IsAlive || includeFakeDead && friend._isFakeDead)
                && (includeFakeDead || !friend._isFakeDead))
            .ToArray() ?? [];
    }

    private MoveState ResolveRecoveryState() => _fakeDeathRecoveryMoveId switch
    {
        StarlightFallMoveId => _starlightState,
        TwinkleMoveId => _twinkleState,
        _ => _waitState
    };

    private MoveState ResolveFollowUpState(MoveState state)
    {
        string? nextStateId = state.FollowUpState?.Id ?? state.FollowUpStateId;
        return nextStateId switch
        {
            StarlightFallMoveId => _starlightState,
            TwinkleMoveId => _twinkleState,
            _ => _waitState
        };
    }

    private string ResolveRecoveryMoveId()
    {
        string? nextMoveId = Creature.Monster?.NextMove?.Id;
        return string.IsNullOrWhiteSpace(nextMoveId)
            || nextMoveId is StunnedMoveId or FakeDeathHiddenMoveId or FakeDeathReviveMoveId
            ? WaitMoveId
            : nextMoveId;
    }

    private void ResetFakeDeathState(bool clearPartingTearsVictoryState = true)
    {
        _isFakeDead = false;
        _fakeDeathPlayerTurnStunsRemaining = 0;
        _fakeDeathRecoveryMoveId = WaitMoveId;
        if (clearPartingTearsVictoryState)
        {
            _isResolvingPartingTearsVictory = false;
        }
    }

    private bool IsDeadOrFakeDeadForPartingTears()
    {
        return _isFakeDead || _isResolvingPartingTearsVictory || Creature.IsDead;
    }

    private static IEnumerable<GalaxyFriend> GetFriends(CombatStateLike? combatState)
    {
        return combatState?.Enemies
            .Select(static creature => creature.Monster as GalaxyFriend)
            .Where(static friend => friend != null)
            .Cast<GalaxyFriend>()
            .ToArray() ?? [];
    }


    private static void StartNormalBackgroundTextLoop()
    {
        MoonTextService.StopRandomLoop(BackgroundTextOwner, FakeDeathBackgroundTextScope);
        MoonTextService.StartRandomLoop(
            BackgroundTextOwner,
            NormalBackgroundTextScope,
            NormalBackgroundTextLineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    private static void StartFakeDeathBackgroundTextLoop()
    {
        MoonTextService.StopRandomLoop(BackgroundTextOwner, NormalBackgroundTextScope);
        MoonTextService.StartRandomLoop(
            BackgroundTextOwner,
            FakeDeathBackgroundTextScope,
            FakeDeathBackgroundTextLineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    private static void StopAllGalaxyChildPresentation()
    {
        MoonTextService.StopRandomLoop(BackgroundTextOwner, NormalBackgroundTextScope);
        MoonTextService.StopRandomLoop(BackgroundTextOwner, FakeDeathBackgroundTextScope);
        GalaxyChildBackgroundController.Reset();
    }

    private static bool IsGalaxyChildEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is GalaxyFriend);
    }

    private void AddGalaxyChildPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room || !IsGalaxyChildEncounter(room))
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<GalaxyChildPageRelic>(
                room,
                player,
                GalaxyChildPageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<GalaxyChildPageRelic>().ToMutable(), player));
        }
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

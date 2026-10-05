using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Literature;

public sealed class LiteratureFloorBlackSwanBoss :
    LorMonsterModel,
    ILiberationPrimaryPhaseBoss
{
    public const int Phase = 5;
    public const int MaxLivingBrothers = 4;
    public const int DeadBrothersForSwanSong = 6;
    public const int ThirdBrotherPlating = 20;
    public const int FourthBrotherDamageIncreasePercent = 100;
    public const int FifthBrotherHealPercent = 25;

    public const string StruggleMoveId = "STRUGGLE";
    public const string VomitMoveId = "VOMIT";
    public const string CurlUpMoveId = "CURL_UP";
    public const string OldUmbrellaMoveId = "OLD_UMBRELLA";
    public const string VileRealityMoveId = "VILE_REALITY";
    public const string SwanSongMoveId = "SWAN_SONG";

    public const int StruggleHits = 2;
    public const int StruggleWeak = 2;
    public const int StruggleWeakTurns = 1;
    public const int VomitHits = 3;
    public const int CurlUpBlock = 16;
    public const int OldUmbrellaBlock = 99;
    public const int OldUmbrellaReflect = 1;
    public const int VileRealityBleed = 9;
    public const int VileRealityVulnerable = 6;
    public const int VileRealityVulnerableTurns = 3;
    public const int SwanSongConfusion = 1;

    public const string IdleTexturePath = LiteratureFloorAssets.BlackSwanMonsterRoot + "black_swan_idle.png";
    public const string HitTexturePath = LiteratureFloorAssets.BlackSwanMonsterRoot + "black_swan_hit.png";
    public const string SpecialTexturePath = LiteratureFloorAssets.BlackSwanMonsterRoot + "black_swan_special.png";
    public const string PierceTexturePath = LiteratureFloorAssets.BlackSwanMonsterRoot + "black_swan_pierce.png";
    public const string SlashOneTexturePath = LiteratureFloorAssets.BlackSwanMonsterRoot + "black_swan_slash_1.png";
    public const string SlashTwoTexturePath = LiteratureFloorAssets.BlackSwanMonsterRoot + "black_swan_slash_2.png";
    public const string GuardOneTexturePath = LiteratureFloorAssets.BlackSwanMonsterRoot + "black_swan_guard_1.png";
    public const string GuardTwoTexturePath = LiteratureFloorAssets.BlackSwanMonsterRoot + "black_swan_guard_2.png";

    public const string SlashUpSfxPath =
        LiteratureFloorAssets.BlackSwanSfxRoot + "black_swan_slash_up.ogg";
    public const string SlashDownSfxPath =
        LiteratureFloorAssets.BlackSwanSfxRoot + "black_swan_slash_down.ogg";
    public const string PierceSfxPath =
        LiteratureFloorAssets.BlackSwanSfxRoot + "black_swan_pierce.ogg";
    public const string GuardSfxPath =
        LiteratureFloorAssets.BlackSwanSfxRoot + "black_swan_guard.ogg";
    public const string ShoutSfxPath =
        LiteratureFloorAssets.BlackSwanSfxRoot + "black_swan_shout.ogg";

    private MoveState? _swanSongState;

    private static readonly string[] NormalMoveIds =
    [
        StruggleMoveId,
        VomitMoveId,
        CurlUpMoveId,
        OldUmbrellaMoveId,
        VileRealityMoveId
    ];

    private static readonly string[] AllCycleMoveIds =
    [
        .. NormalMoveIds,
        SwanSongMoveId
    ];

    public int CompletedMoveCycleMask { get; private set; }

    public int LiberationPhase => Phase;

    /// <summary>死亡动画时长；转阶段的假死返回 0，见 <see cref="LayeredBossSpine.DeathLength"/>。</summary>
    public override float DeathAnimLengthOverride => LayeredBossSpine.DeathLength(this);

    internal bool IsSwanSongQueued =>
        NextMove.StateId == SwanSongMoveId;

    internal bool IsSwanSongUnlocked =>
        Creature.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter
            {
                NextBlackSwanBrother: > 6
            }
        && Creature.CombatState.Enemies.All(static enemy =>
            !enemy.IsAlive
            || enemy.Monster
                is not LiteratureFloorBlackSwanBrotherBase);

    private int StruggleDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            6,
            4);

    private int VomitDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            3);

    private int CurlUpDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            12,
            10);

    private int SwanSongDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            24,
            19);

    internal static (int Min, int Max) DebugHpRange(bool toughEnemies) =>
        toughEnemies ? (250, 260) : (210, 220);

    internal static int DebugStruggleDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 4 : 3;

    internal static int DebugVomitDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 3 : 2;

    internal static int DebugCurlUpDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 10 : 9;

    internal static int DebugSwanSongDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 21 : 18;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            300,
            270);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            310,
            280);

    public override int DefaultChaoResistance => 260;

    public override bool ShouldDisappearFromDoom => false;

    public override bool HasDeathSfx => false;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => UniformNormalResistance();

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => UniformNormalResistance();

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                LiteratureFloorBlackSwanCreatureVisuals.ScenePath,
                IdleTexturePath,
                HitTexturePath,
                SpecialTexturePath,
                PierceTexturePath,
                SlashOneTexturePath,
                SlashTwoTexturePath,
                GuardOneTexturePath,
                GuardTwoTexturePath,
                SlashUpSfxPath,
                SlashDownSfxPath,
                PierceSfxPath,
                GuardSfxPath,
                ShoutSfxPath,
                LiteratureFloorAssets.LibraryPassiveGreenIcon,
                LiteratureFloorBlackSwanVanishingFamilyPower.CustomIconPath,
                LiteratureFloorAssets.HistoryFloorCorrosionPowerIcon
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
        LiteratureFloorLiberationBackgroundController.SetPhaseBackground(
            Phase);
        EncounterBgmController.RegisterMonster(Creature);

        if (Creature.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        await PowerCmdCompat.Ensure<HistoryFloorCorrosionPower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Ensure<
            LiteratureFloorBlackSwanNettleGarmentPassivePower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Ensure<
            LiteratureFloorBlackSwanProtectFamilyPassivePower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
        await PowerCmdCompat.Ensure<
            LiteratureFloorBlackSwanBrokenDreamPassivePower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
        if (Creature.GetPower<
                LiteratureFloorBlackSwanVanishingFamilyPower>() == null)
        {
            LiteratureFloorBlackSwanVanishingFamilyPower? vanishing =
                await PowerCmdCompat.Ensure<
                    LiteratureFloorBlackSwanVanishingFamilyPower>(
                    Creature,
                    1m,
                    Creature,
                    null,
                    silent: true);
            vanishing?.InitializeCounter();
        }
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

        if (Creature.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter)
        {
            await encounter.OnPhaseBossDeath(
                this,
                wasRemovalPrevented,
                deathAnimLength);
        }
    }

    public Task TriggerReviveAndEmpowerState() => Task.CompletedTask;

    public void ForceReviveAndEmpowerState()
    {
    }

    internal async Task OnPlayerSideTurnStart(
        CombatStateLike combatState)
    {
        if (Creature.IsDead
            || Creature.CombatState != combatState
            || combatState.Encounter
                is not LiteratureFloorLiberationEncounter
                {
                    CurrentPhase: Phase,
                    PhaseComplete: false,
                    TransitionPending: false
                } encounter)
        {
            return;
        }

        await encounter.AdvanceBlackSwanRoundAndTrySummon(combatState);

        LiteratureFloorBlackSwanBrotherBase[] livingBrothers =
            GetLivingBrothers(combatState);
        Creature.GetPower<
                LiteratureFloorBlackSwanVanishingFamilyPower>()
            ?.SynchronizeCounter(
                encounter.NextBlackSwanBrother - 1
                - livingBrothers.Length);
        foreach (LiteratureFloorBlackSwanBrotherBase brother
                 in livingBrothers)
        {
            await brother.ApplyRoundStartSupport(this);
        }

        if (livingBrothers.Length >= MaxLivingBrothers)
        {
            await TryQueueSwanSong();
        }
    }

    internal async Task OnBrotherDeath(int brotherNumber)
    {
        if (Creature.IsDead
            || Creature.CombatState?.Encounter
                is not LiteratureFloorLiberationEncounter
                {
                    CurrentPhase: Phase,
                    PhaseComplete: false
                })
        {
            return;
        }

        Creature.GetPower<
                LiteratureFloorBlackSwanVanishingFamilyPower>()
            ?.IncrementCounter();

        if (brotherNumber == 6
            && Creature is LibraryCreature
            {
                HasChaoResistance: true
            } libraryCreature)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(
                libraryCreature,
                0m);
            libraryCreature.HealthBar?.RefreshValues();
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var struggle = new MoveState(
            StruggleMoveId,
            StruggleMove,
            new CombinedAttackDebuffIntent(
                () => StruggleDamage,
                () => StruggleHits,
                "LITERATURE_FLOOR_BLACK_SWAN_STRUGGLE.description",
                IntentBadge.FromPower<LibraryWeakPower>(
                    StruggleWeak,
                    StruggleWeakTurns.ToString(),
                    StruggleWeak.ToString())));
        var vomit = new MoveState(
            VomitMoveId,
            VomitMove,
            new MultiAttackIntent(VomitDamage, VomitHits));
        var curlUp = new MoveState(
            CurlUpMoveId,
            CurlUpMove,
            new CombinedAttackDefendIntent(
                () => CurlUpDamage,
                () => 1,
                "LITERATURE_FLOOR_BLACK_SWAN_CURL_UP.description",
                CurlUpBlock));
        var oldUmbrella = new MoveState(
            OldUmbrellaMoveId,
            OldUmbrellaMove,
            new CombinedDefendBuffIntent(
                OldUmbrellaBlock,
                "LITERATURE_FLOOR_BLACK_SWAN_OLD_UMBRELLA.description",
                IntentBadge.FromPower<ReflectPower>(
                    OldUmbrellaReflect)));
        var vileReality = new MoveState(
            VileRealityMoveId,
            VileRealityMove,
            new DebuffIntent(strong: true));
        var swanSongIntent = new IndiscriminateAttackIntent(
            () => SwanSongDamage,
            () => 1,
            "LITERATURE_FLOOR_BLACK_SWAN_SWAN_SONG.description",
            IntentBadge.Confusion(SwanSongConfusion));
        _swanSongState = new MoveState(
            SwanSongMoveId,
            swanSongIntent.WithPreAttackBlockBreak(this, SwanSongMove),
            swanSongIntent)
        {
            MustPerformOnceBeforeTransitioning = true
        };

        var router = new DelegatingMonsterRouterState(
            "BLACK_SWAN_CYCLE_ROUTER",
            (_, rng) => ResolveCycleMoveId(rng));
        foreach (MoveState state in new[]
                 {
                     struggle,
                     vomit,
                     curlUp,
                     oldUmbrella,
                     vileReality
                 })
        {
            state.FollowUpState = router;
        }

        _swanSongState.FollowUpState = router;
        return new MonsterMoveStateMachine(
        [
            struggle,
            vomit,
            curlUp,
            oldUmbrella,
            vileReality,
            _swanSongState,
            router
        ], router);
    }

    private async Task StruggleMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment("SlashOne", StruggleDamage);
        await ExecuteAttackSegment("SlashTwo", StruggleDamage);
        foreach (Creature player in LivingPlayers())
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                player,
                StruggleWeak,
                StruggleWeakTurns,
                Creature,
                null);
        }

        CompleteMoveCycle(StruggleMoveId);
    }

    private async Task VomitMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment("Pierce", VomitDamage);
        await ExecuteAttackSegment("SlashOne", VomitDamage);
        await ExecuteAttackSegment("SlashTwo", VomitDamage);
        CompleteMoveCycle(VomitMoveId);
    }

    private async Task CurlUpMove(IReadOnlyList<Creature> targets)
    {
        await ExecuteAttackSegment("SlashOne", CurlUpDamage);
        LocalOggOneShotPlayer.Play(GuardSfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Guard",
            LiteratureFloorBlackSwanAnimationContract
                .GuardDurationSeconds);
        await CreatureCmd.GainBlock(
            Creature,
            CurlUpBlock,
            ValueProp.Move,
            null);
        CompleteMoveCycle(CurlUpMoveId);
    }

    private async Task OldUmbrellaMove(
        IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(GuardSfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Guard",
            LiteratureFloorBlackSwanAnimationContract
                .GuardDurationSeconds);
        await CreatureCmd.GainBlock(
            Creature,
            OldUmbrellaBlock,
            ValueProp.Move,
            null);
        await PowerCmdCompat.Apply<ReflectPower>(
            Creature,
            OldUmbrellaReflect,
            Creature,
            null);
        CompleteMoveCycle(OldUmbrellaMoveId);
    }

    private async Task VileRealityMove(
        IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(PierceSfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Pierce",
            LiteratureFloorBlackSwanAnimationContract
                .PierceDurationSeconds);
        foreach (Creature player in LivingPlayers())
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                player,
                VileRealityBleed,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                player,
                VileRealityVulnerable,
                VileRealityVulnerableTurns,
                Creature,
                null);
        }

        CompleteMoveCycle(VileRealityMoveId);
    }

    private async Task SwanSongMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(ShoutSfxPath, -1f);
        await DamageCmd.Attack(SwanSongDamage)
            .FromMonster(this)
            .WithAttackerAnim(
                "Special",
                LiteratureFloorBlackSwanAnimationContract
                    .SpecialDurationSeconds)
            .Execute(null);
        await PowerCmdCompat.ApplyDebuff<
            LibraryOfRuinaConfusionPower>(
            LivingPlayers(),
            SwanSongConfusion,
            Creature,
            null);
        CompleteMoveCycle(SwanSongMoveId);
    }

    private async Task<AttackCommand> ExecuteAttackSegment(
        string trigger,
        int damage)
    {
        float duration = trigger == "Pierce"
            ? LiteratureFloorBlackSwanAnimationContract
                .PierceDurationSeconds
            : LiteratureFloorBlackSwanAnimationContract
                .SlashDurationSeconds;
        string sfxPath = trigger switch
        {
            "Pierce" => PierceSfxPath,
            "SlashTwo" => SlashDownSfxPath,
            _ => SlashUpSfxPath
        };
        LocalOggOneShotPlayer.Play(sfxPath, -1.5f);
        return await DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(trigger, duration)
            .Execute(null);
    }

    private async Task<bool> TryQueueSwanSong()
    {
        if (_swanSongState == null
            || Creature.IsDead
            || Creature.IsStunned
            || IsSwanSongQueued
            || NextMove.StateId == MonsterModel.stunnedMoveId)
        {
            return false;
        }

        SetMoveImmediate(_swanSongState, forceTransition: true);
        if (CombatQueries.CreatureNodeOf(this) is { } node)
        {
            await node.RefreshIntents();
        }

        return true;
    }

    private string ResolveCycleMoveId(Rng rng)
    {
        string[] moveIds = IsSwanSongUnlocked
            ? AllCycleMoveIds
            : NormalMoveIds;
        int fullMask = (1 << moveIds.Length) - 1;
        CompletedMoveCycleMask &= fullMask;
        if (CompletedMoveCycleMask == fullMask)
        {
            CompletedMoveCycleMask = 0;
        }

        int[] eligibleIndices = Enumerable.Range(0, moveIds.Length)
            .Where(index =>
                (CompletedMoveCycleMask & (1 << index)) == 0)
            .ToArray();
        return moveIds[rng.NextItem(eligibleIndices)];
    }

    private void CompleteMoveCycle(string moveId)
    {
        int index = Array.IndexOf(AllCycleMoveIds, moveId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Unknown Black Swan cycle move " + moveId + ".");
        }

        CompletedMoveCycleMask |= 1 << index;
        int moveCount = IsSwanSongUnlocked
            ? AllCycleMoveIds.Length
            : NormalMoveIds.Length;
        int fullMask = (1 << moveCount) - 1;
        CompletedMoveCycleMask &= fullMask;
        if (CompletedMoveCycleMask == fullMask)
        {
            CompletedMoveCycleMask = 0;
        }
    }

    private LiteratureFloorBlackSwanBrotherBase[] GetLivingBrothers(
        CombatStateLike combatState) =>
        combatState.LivingEnemies()
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorBlackSwanBrotherBase>()
            .OrderBy(static brother => brother.BrotherNumber)
            .ToArray();

    private IEnumerable<Creature> LivingPlayers() =>
        Creature.CombatState?.LivingPlayerCreatures()
            .ToArray() ?? [];

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new CombinedAttackDebuffIntent(
            () => StruggleDamage,
            () => StruggleHits,
            "LITERATURE_FLOOR_BLACK_SWAN_STRUGGLE.description",
            IntentBadge.FromPower<LibraryWeakPower>(
                StruggleWeak,
                StruggleWeakTurns.ToString(),
                StruggleWeak.ToString()));
        yield return new MultiAttackIntent(VomitDamage, VomitHits);
        yield return new CombinedAttackDefendIntent(
            () => CurlUpDamage,
            () => 1,
            "LITERATURE_FLOOR_BLACK_SWAN_CURL_UP.description",
            CurlUpBlock);
        yield return new CombinedDefendBuffIntent(
            OldUmbrellaBlock,
            "LITERATURE_FLOOR_BLACK_SWAN_OLD_UMBRELLA.description",
            IntentBadge.FromPower<ReflectPower>(
                OldUmbrellaReflect));
        yield return new DebuffIntent(strong: true);
        yield return new IndiscriminateAttackIntent(
            () => SwanSongDamage,
            () => 1,
            "LITERATURE_FLOOR_BLACK_SWAN_SWAN_SONG.description",
            IntentBadge.Confusion(SwanSongConfusion));
    }

    private static LibraryCreatureResistanceData.Resistance
        UniformNormalResistance() =>
        new(LibraryResistanceLevel.Normal);
}

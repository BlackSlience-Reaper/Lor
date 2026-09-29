using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.cards.PriceOfSilence;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.PriceOfSilence;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.PriceOfSilence;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.PriceOfSilence;
using LibraryOfRuina.visuals.PriceOfSilence;
using MegaCrit.Sts2.Core.Combat;
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
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.monsters.PriceOfSilence;

public sealed class PriceOfSilence : LorMonsterModel
{
    internal const string InevitableDoomMoveId = "INEVITABLE_DOOM";
    internal const string UnknownMoveId = "UNKNOWN";
    private const string RouterStateId = "PRICE_OF_SILENCE_ROUTER";

    public const int CountdownTurns = 4;
    public const int RequiredDestroyedTraces = 2;
    public const int TickingAttackMinimumDamage = 14;
    public const int TickingGuardMinimumBlock = 33;

    internal const int InevitableDoomMinDamage = 13;
    internal const int InevitableDoomMaxDamage = 15;
    internal const int InevitableDoomHits = 3;

    internal const string TextureRoot = "res://images/monsters/price_of_silence/";
    internal const string IdleTexturePath = TextureRoot + "idle.png";
    internal const string SpecialTexturePath = TextureRoot + "special.png";
    internal const string BackgroundRoot = "res://images/backgrounds/price_of_silence_strong/";
    internal const string FilterTexturePath = BackgroundRoot + "filter.png";
    internal const string SfxRoot = "res://audio/sfx/price_of_silence/";
    internal const string MassAttackSfxPath = SfxRoot + "mass_attack.ogg";
    internal const string TraceDestroyedSfxPath = SfxRoot + "trace_destroyed.ogg";
    internal const string SilenceCardSfxPath = SfxRoot + "silence_card.ogg";
    internal const string AmbientSfxPath = SfxRoot + "ambient.ogg";

    private static readonly string PageRelicTitleLocKey = $"{ModelDb.GetId<PriceOfSilencePageRelic>().Entry}.title";

    internal static readonly string[] AdditionalAssetPaths =
    [
        FilterTexturePath,
        MassAttackSfxPath,
        TraceDestroyedSfxPath,
        SilenceCardSfxPath,
        AmbientSfxPath,
        "res://images/powers/price_of_silence_passive_power.png",
        "res://images/powers/price_of_silence_silence_power.png",
        "res://images/powers/accumulated_time_power.png",
        "res://images/powers/unstoppable_time_power.png",
        "res://images/powers/ticking_attack_power.png",
        "res://images/powers/ticking_guard_power.png"
    ];

    private Dictionary<string, MoveState> _statesById = [];
    private int _countdown = CountdownTurns;
    private int _destroyedTraces;
    private bool _pendingExposure;
    private bool _exposureActive;
    private bool _useDoomDuringExposure;
    private bool _staggerOnExposure;
    private int? _doomDamageRoll;
    private LocalOggLoopPlayer.LoopHandle? _ambientLoop;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
        _ambientLoop = null;
    }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 347, 240);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 350, 243);

    public override int DefaultChaoResistance => 180;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override IEnumerable<string> AssetPaths =>
        PriceOfSilenceCreatureVisuals
            .Profile.AssetPaths
            .Concat(AdditionalAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        _countdown = CountdownTurns;
        _destroyedTraces = 0;
        _pendingExposure = false;
        _exposureActive = false;
        _useDoomDuringExposure = false;
        _staggerOnExposure = false;
        _doomDamageRoll = null;
        _ambientLoop = LocalOggLoopPlayer.StartLoop(AmbientSfxPath, -9f);
        await PowerCmdCompat.Apply<UnstoppableTimePower>(Creature, 1m, Creature, null);
        //await PowerCmdCompat.Apply<PriceOfSilencePassivePower>(Creature, 1, Creature, null);
        await PowerCmdCompat.Apply<PriceOfSilenceEncounterTrackerPower>(Creature, 1, Creature, null);
        await RefreshCounters(silent: true);
        await ApplyUntargetable();
        ForceRefreshMoveState();
    }

    public override void BeforeRemovedFromRoom()
    {
        StopAmbient();
        base.BeforeRemovedFromRoom();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        StopAmbient();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player && Creature.IsAlive)
        {
            if (_exposureActive)
            {
                await ResetCycleAfterExposure(choiceContext);
            }

            if (_pendingExposure && ShouldBeginExposure())
            {
                await BeginExposure(choiceContext);
            }
        }

        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player
            && Creature.IsAlive
            && PriceOfSilenceEncounterHelper.IsPriceOfSilenceEncounter(combatState))
        {
            await GiveSilenceCards(combatState);
        }

        await base.AfterSideTurnStart(side, participants, combatState);
    }

    protected override async Task AfterSideTurnEndInternal(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || Creature.IsDead || _exposureActive || _pendingExposure)
        {
            return;
        }

        _countdown = Math.Max(0, _countdown - 1);
        if (_countdown > 0)
        {
            await RefreshCounters(silent: false);
            return;
        }

        _pendingExposure = true;
        _useDoomDuringExposure = _destroyedTraces < RequiredDestroyedTraces;
        _staggerOnExposure = !_useDoomDuringExposure;
        if (_useDoomDuringExposure)
        {
            EnsureDoomDamageRoll();
        }

        ResetCycleCounters();
        await RefreshCounters(silent: false);
        ForceRefreshMoveState();
    }

    public async Task NotifyTraceDestroyed()
    {
        if (Creature?.IsAlive != true)
        {
            return;
        }

        _destroyedTraces++;
        await RefreshCounters(silent: false);
        LocalOggOneShotPlayer.Play(TraceDestroyedSfxPath, -2f);
    }

    private async Task GiveSilenceCards(CombatStateLike combatState)
    {
        IReadOnlyList<Creature> livingPlayers = PriceOfSilenceEncounterHelper.LivingPlayers(combatState);
        if (livingPlayers.Count == 0)
        {
            return;
        }

        var targetPlayer = combatState.Players.FirstOrDefault(static p => p.Creature.GetPower<TimeTraceMarkedPlayerPower>() != null);
        if (targetPlayer != null)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<SilenceStatusCard>(
                targetPlayer.Creature,
                PileType.Hand,
                1,
                addedByPlayer: false,
                CardPilePosition.Top);
        }
        
    }

    public override Task AfterStun(Creature creature)
    {
        if (creature == Creature && _pendingExposure && _useDoomDuringExposure)
        {
            _useDoomDuringExposure = false;
            _staggerOnExposure = false;
        }

        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();
    
        MoveState unknown = Register(new MoveState(
            UnknownMoveId,
            _ => Task.CompletedTask,
            new UnknownIntent()));

        MoveState doom = Register(new MoveState(
            InevitableDoomMoveId,
            InevitableDoomMove,
            new IndiscriminateAttackIntent(
                () => GetDoomDamageRoll(),
                () => InevitableDoomHits,
                "PRICE_OF_SILENCE_INEVITABLE_DOOM.description")));

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng));
        unknown.FollowUpState = router;
        doom.FollowUpState = router;

        return new MonsterMoveStateMachine([unknown, doom, router], router);
    }

    internal string ResolvePlannedMoveId(Rng rng)
    {
        if ((_pendingExposure || _exposureActive) && _useDoomDuringExposure)
        {
            EnsureDoomDamageRoll();
            return InevitableDoomMoveId;
        }

        return UnknownMoveId;
    }

    private async Task InevitableDoomMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(MassAttackSfxPath, -1f);
        IReadOnlyList<Creature> actualTargets = PriceOfSilenceEncounterHelper.LivingPlayers(Creature.CombatState);
        if (actualTargets.Count == 0)
        {
            return;
        }

        int damage = GetDoomDamageRoll();
        await IndiscriminateAttackExecutor.Execute(
            this,
            damage,
            actualTargets,
            attack => attack
                .WithHitCount(InevitableDoomHits)
                .WithAttackerAnim("Special", 0.58f)
                .WithHitFx("vfx/vfx_attack_blunt"));

        _doomDamageRoll = null;
        _useDoomDuringExposure = false;
        _pendingExposure = true;
        _staggerOnExposure = false;
    }

    private async Task BeginExposure(PlayerChoiceContext choiceContext)
    {
        _pendingExposure = false;
        _exposureActive = true;
        _useDoomDuringExposure = false;
        await RemoveUntargetable();
        if (_staggerOnExposure && Creature is LibraryCreature libraryCreature)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, 0m);
        }

        _staggerOnExposure = false;
        ForceRefreshMoveState();
        await PowerCmdCompat.Apply<StrengthPower>(Creature, 1, Creature, null);
    }

    private bool ShouldBeginExposure() =>
        !_useDoomDuringExposure || Creature is LibraryCreature { IsChaoed: true };

    private async Task ResetCycleAfterExposure(PlayerChoiceContext choiceContext)
    {
        _exposureActive = false;
        _useDoomDuringExposure = false;
        _pendingExposure = false;
        _staggerOnExposure = false;
        _doomDamageRoll = null;

        await ApplyUntargetable();
        if (Creature is LibraryCreature libraryCreature && libraryCreature.HasChaoResistance)
        {
            libraryCreature.RestoreChaoOnNextOwnerTurn = false;
            libraryCreature.RestorePreStunResistance();
            await LibraryCreatureCmd.SetCurrentChaoValue(libraryCreature, libraryCreature.MaxChaoValue);
        }

        await RefreshCounters(silent: false);
        ForceRefreshMoveState();
    }

    private async Task ApplyUntargetable()
    {
        await PowerCmdCompat.Ensure<UntargetablePower>(Creature);
    }

    private async Task RemoveUntargetable()
    {
        PowerModel? untargetablePower = Creature.GetPower<UntargetablePower>();
        if (untargetablePower != null)
        {
            await PowerCmd.Remove(untargetablePower);
        }
    }

    private async Task RefreshCounters(bool silent)
    {
        await SetPowerAmount<UnstoppableTimePower>(GetVisibleUnstoppableTimeCounterAmount(), silent);
        await SetPowerAmount<AccumulatedTimePower>(GetVisibleTraceKillCounterAmount(), silent);
    }

    private async Task SetPowerAmount<TPower>(int amount, bool silent)
        where TPower : PowerModel
    {
        TPower? power = Creature.GetPower<TPower>();
        if (power == null)
        {
            await PowerCmdCompat.Apply<TPower>(Creature, amount, Creature, null, silent);
            return;
        }

        await PowerCmdCompat.ModifyAmount(power, amount - power.Amount, Creature, null, silent);
    }

    private int GetVisibleTraceKillCounterAmount() =>
        _destroyedTraces + 1;

    private void ResetCycleCounters()
    {
        _countdown = CountdownTurns;
        _destroyedTraces = 0;
    }

    private int GetVisibleUnstoppableTimeCounterAmount()
    {
        int visibleTurn = CountdownTurns - _countdown + 1;
        return Math.Min(CountdownTurns, Math.Max(1, visibleTurn));
    }

    private int GetDoomDamageRoll() =>
        GetOrRollDamage(ref _doomDamageRoll, InevitableDoomMinDamage, InevitableDoomMaxDamage);

    private int EnsureDoomDamageRoll() =>
        EnsureDamageRoll(ref _doomDamageRoll, InevitableDoomMinDamage, InevitableDoomMaxDamage);

    private void ForceRefreshMoveState()
    {
        if (!IsMutable || MoveStateMachine == null || _statesById.Count == 0)
        {
            return;
        }

        string moveId = ResolvePlannedMoveId(RunRng.MonsterAi);
        if (_statesById.TryGetValue(moveId, out MoveState? state))
        {
            SetMoveImmediate(state, forceTransition: true);
        }
    }

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is MoveState moveState)
            {
                foreach (AbstractIntent intent in moveState.Intents)
                {
                    yield return intent;
                }
            }
        }
    }

    private void AddPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !PriceOfSilenceEncounterHelper.IsPriceOfSilenceEncounter(deadCreature.CombatState))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<PriceOfSilencePageRelic>(room, PageRelicTitleLocKey);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            StopAmbient();
            AddPageRewardsFromDeathHook(creature);

            Creature[] traces = creature.CombatState?.Enemies
                .Where(static enemy => enemy.Monster is TimeTrace)
                .ToArray()
                ?? [];

            // 假死痕迹仍留在战场中；先解除死亡留场规则，再统一清理活着和假死的痕迹。
            foreach (Creature trace in traces)
            {
                await PowerCmdCompat.RemoveIfPresent<TimeTraceRestorationPower>(trace);
            }

            await CreatureCmd.Kill(traces, force: true);
        }
    }

    private void StopAmbient()
    {
        _ambientLoop?.Stop();
        _ambientLoop = null;
    }

}

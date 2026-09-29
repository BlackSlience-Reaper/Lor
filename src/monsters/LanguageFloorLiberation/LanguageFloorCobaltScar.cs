using LibraryLib.Models;
using System;
using LibraryOfRuina.helpers;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.LanguageFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.visuals.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

public enum LanguageFloorCobaltScarForm
{
    CobaltScar,
    BigBadWolf
}

public sealed class LanguageFloorCobaltScar :
    CounterIntentMonsterModel,
    ILiberationPrimaryPhaseBoss, LibraryOfRuina.helpers.IFinalHpLossClamp
{
    private const string NormalCompositeMoveId = "LANGUAGE_FLOOR_COBALT_COMPOSITE";
    private const string ShadowCompositeMoveId = "LANGUAGE_FLOOR_COBALT_SHADOW_COMPOSITE";
    private const string RouterMoveId = "LANGUAGE_FLOOR_COBALT_ROUTER";
    public const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";
    private const int IntentCount = 3;
    internal const int ShadowIntentCount = 2;
    private const int CardsSwallowedPerPlayer = 3;
    private const int CobaltMaxChao = 150;
    private const int BigWolfMaxChao = 250;
    private const int BindingAmount = 6;
    private const int BindingTurns = 1;
    private const int DoNotProvokeBlock = 16;
    private const int WolfComesStrength = 1;
    private const int HorrifyingClawsBleed = 2;
    private const int HorrifyingClawsBlock = 9;
    private const int BrutalFangsBlock = 13;
    private const int BrutalFangsStatLoss = 1;
    private const int BloodstainedHuntDrawLoss = 1;
    private const int BloodstainedHuntScarTotal = 3;
    private const int ShadowAssaultHealPercent = 6;
    private const int TransformHpPercent = 50;
    private const int ShadowThresholdPercent = 25;
    private const int ShadowEnemyTurns = 2;
    private const int SpecialAttackInterval = 2;
    private const float AttackDelay = 0.62f;
    private const float RoarDelay = 3.0f;

    private const string SfxRoot =
        "res://audio/sfx/language_floor_liberation/cobalt_scar/";
    private const string BiteSfx = SfxRoot + "wolf_bite.ogg";
    private const string ScratchSfx = SfxRoot + "wolf_scratch.ogg";
    private const string GuardSfx = SfxRoot + "wolf_guard.ogg";
    private const string TransformSfx = SfxRoot + "wolf_phase2.ogg";
    private const string ShadowSfx = SfxRoot + "wolf_fog_change.ogg";
    private const string RoarSfx = SfxRoot + "wolf_howl.ogg";
    private const string SpitSfx = SfxRoot + "wolf_eat_out.ogg";

    [SavedProperty]
    public int PlannedMoveOne { get; private set; } = -1;

    [SavedProperty]
    public int PlannedMoveTwo { get; private set; } = -1;

    [SavedProperty]
    public int PlannedMoveThree { get; private set; } = -1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public LanguageFloorCobaltScarForm Form { get; private set; }

    internal bool IsHealthBarLockActive =>
        Form != LanguageFloorCobaltScarForm.BigBadWolf
        && Creature.CurrentHp <= TransformHpThreshold(Creature.MaxHp);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool OpeningResolved { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool SwallowWindowActive { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PlayerTurnsSinceSwallow { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ForceInstinctNextTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TurnsUntilInstinct { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BigWolfEntryHp { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int AccumulatedDamage { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ShadowTurnsRemaining { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ShadowReleasePending { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ForceRoarNextTurn { get; private set; }

    [SavedProperty]
    public List<SerializableCard> SwallowedCards { get; private set; } = [];

    [SavedProperty]
    public int[] SwallowedOwnerIndexes { get; private set; } = [];

    [SavedProperty]
    public int[] ShadowCardsPlayedByPlayer { get; private set; } = [];

    private MoveState? _normalCompositeState;
    private MoveState? _shadowCompositeState;
    private MoveState? _reviveAndEmpowerState;
    private PlannedMoveController<LanguageFloorMoveKind>? _plan;
    private bool _resolvingSwallowedCards;

    // 普通与影子两个复合行动共用三个槽位，分别展示前 3、2 个。读槽位把非法值当作咳嗽，执行不会停在空槽位。
    private PlannedMoveController<LanguageFloorMoveKind> Plan => _plan ??= new(
        this,
        IntentCount,
        GetPlannedMove,
        SetPlannedMove,
        static (_, move) => CreateIntent(move),
        (LanguageFloorMoveKind)(-1));

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        SwallowedCards = [.. SwallowedCards];
        SwallowedOwnerIndexes = [.. SwallowedOwnerIndexes];
        ShadowCardsPlayedByPlayer = [.. ShadowCardsPlayedByPlayer];
        _normalCompositeState = null;
        _shadowCompositeState = null;
        _reviveAndEmpowerState = null;
        // 计划控制器的委托捕获的是被克隆的实例，克隆体必须用自己的。
        _plan = null;
    }

    public int LiberationPhase => 2;

    public override bool ShouldDisappearFromDoom => false;

    public override LocString Title =>
        new(
            "monsters",
            Form == LanguageFloorCobaltScarForm.BigBadWolf
                ? "LANGUAGE_FLOOR_COBALT_SCAR.bigBadWolfName"
                : "LANGUAGE_FLOOR_COBALT_SCAR.name");

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            297,
            290);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            300,
            293);

    public override int DefaultChaoResistance => CobaltMaxChao;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        Form == LanguageFloorCobaltScarForm.BigBadWolf
            ? new()
            {
                Slash = LibraryResistanceLevel.Resist,
                Pierce = LibraryResistanceLevel.Resist,
                Blunt = LibraryResistanceLevel.Endure
            }
            : new()
            {
                Slash = LibraryResistanceLevel.Resist,
                Pierce = LibraryResistanceLevel.Endure,
                Blunt = LibraryResistanceLevel.Normal
            };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData =>
        Form == LanguageFloorCobaltScarForm.BigBadWolf
            ? CreateUniformResistance(LibraryResistanceLevel.Resist)
            : new()
            {
                Slash = LibraryResistanceLevel.Endure,
                Pierce = LibraryResistanceLevel.Endure,
                Blunt = LibraryResistanceLevel.Normal
            };

    public override IEnumerable<string> AssetPaths =>
        LanguageFloorCobaltScarCreatureVisuals.Profile.AssetPaths
            .Concat(PowerAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Concat([BiteSfx, ScratchSfx, GuardSfx, TransformSfx, ShadowSfx, RoarSfx, SpitSfx])
            .Distinct();

    private static readonly string[] PowerAssetPaths =
    [
        "res://images/powers/language_floor_scar_power.png",
        "res://images/powers/language_floor_rip_open_claw_passive_power.png",
        "res://images/powers/language_floor_punish_evil_passive_power.png",
        "res://images/powers/language_floor_destined_big_bad_wolf_passive_power.png",
        "res://images/powers/language_floor_hide_in_darkness_passive_power.png",
        "res://images/powers/language_floor_shadow_ambush_passive_power.png",
        "res://images/powers/language_floor_exhaustion_passive_power.png",
        "res://images/powers/language_floor_shadow_wolf_power.png"
    ];

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (Creature.CombatState?.Encounter is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        PresentationGuard.Run(
            () => LanguageFloorLiberationBackgroundController.SetPhaseBackground(2),
            "LanguageFloorCobaltScar phase background");
        await EnsureFormPowers();
        EnsureShadowCardCounters();
        // ResolveOpeningSwallow below consumes the card-generation RNG and moves cards; it must run
        // on every client even if this client's visuals fail.
        await PresentationGuard.RunAsync(ApplyVisualState, "LanguageFloorCobaltScar form visuals");
        if (!OpeningResolved)
        {
            await ResolveOpeningSwallow();
        }
        else
        {
            Plan.RefreshIntents();
        }

        if (Creature.CombatState?.CurrentSide == CombatSide.Player)
        {
            PrepareCounterIntentsFromCurrentMove();
        }
    }

    public async Task TriggerReviveAndEmpowerState()
    {
        await LiberationPhaseBossMoves.TriggerHitAnimationIfVisible(Creature);
        ForceReviveAndEmpowerState();
    }

    public void ForceReviveAndEmpowerState()
    {
        LiberationPhaseBossMoves.ForceState(this, _reviveAndEmpowerState);
    }

    private async Task ReviveAndEmpowerMove(
        IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        await Cmd.CustomScaledWait(0.3f, 0.6f);
        if (Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseTransition(this);
        }
    }

    public override async Task BeforeDeath(Creature creature)
    {
        if (creature == Creature && SwallowedCards.Count > 0)
        {
            await ReturnSwallowedCards(increaseCost: true, applyHpLoss: false);
        }

        await base.BeforeDeath(creature);
    }

    public override Task BeforeStun(Creature creature)
    {
        if (creature == Creature && IsOpeningCounterPlan)
        {
            PlanTurn(RunRng.MonsterAi);
            ClearCounterIntentQueueAndRefresh(forceRefresh: true);
        }

        return base.BeforeStun(creature);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _normalCompositeState = Plan.CreateCompositeState(
            NormalCompositeMoveId,
            PerformCompositeMove);
        _shadowCompositeState = Plan.CreateCompositeState(
            ShadowCompositeMoveId,
            PerformCompositeMove,
            intentCount: ShadowIntentCount);
        _reviveAndEmpowerState = LiberationPhaseBossMoves.CreateState(
            ReviveAndEmpowerMoveId,
            ReviveAndEmpowerMove);
        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, rng) =>
            {
                PlanTurn(rng);
                return GetCurrentCompositeState().Id;
            });
        _normalCompositeState.FollowUpState = router;
        _shadowCompositeState.FollowUpState = router;
        _reviveAndEmpowerState.FollowUpState = router;
        MoveState initialState = HasPlannedTurn
            ? GetCurrentCompositeState()
            : _normalCompositeState;
        return new MonsterMoveStateMachine(
            [
                _reviveAndEmpowerState,
                _normalCompositeState,
                _shadowCompositeState,
                router
            ],
            HasPlannedTurn ? initialState : router);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == CombatSide.Player && Creature.IsAlive)
        {
            if (Form == LanguageFloorCobaltScarForm.CobaltScar
                && Creature.CurrentHp <= TransformHpThreshold(Creature.MaxHp))
            {
                if (SwallowedCards.Count > 0)
                {
                    await ReturnSwallowedCards(increaseCost: true, applyHpLoss: true);
                }

                await CreatureCmd.SetCurrentHp(
                    Creature,
                    TransformHpThreshold(Creature.MaxHp));
                await TransformToBigBadWolf();
            }
            else if (SwallowWindowActive)
            {
                PlayerTurnsSinceSwallow++;
            }

            if (ShadowReleasePending)
            {
                ShadowReleasePending = false;
                ShadowTurnsRemaining = 0;
                ForceRoarNextTurn = true;
                await PowerCmdCompat.RemoveIfPresent<
                    LanguageFloorShadowWolfPower>(Creature);
                await SetAllChaoResistances(
                    new BlockingPlayerChoiceContext(),
                    LibraryResistanceLevel.Fatal);
                await ApplyVisualState();
            }
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }

    protected override async Task AfterSideTurnEndInternal(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEndInternal(choiceContext, side, participants);
        if (side == CombatSide.Player)
        {
            if (Creature.IsAlive
                && Form == LanguageFloorCobaltScarForm.CobaltScar
                && SwallowWindowActive
                && PlayerTurnsSinceSwallow > 0)
            {
                await ConsumeSwallowedCards();
            }

            return;
        }

        if (side != CombatSide.Enemy
            || Creature.IsDead
            || Form != LanguageFloorCobaltScarForm.BigBadWolf)
        {
            return;
        }

        if (ShadowTurnsRemaining > 0)
        {
            ShadowTurnsRemaining--;
            if (ShadowTurnsRemaining == 0)
            {
                ShadowReleasePending = true;
            }
            else
            {
                await PowerCmdCompat.SetAmount<LanguageFloorShadowWolfPower>(
                    Creature,
                    ShadowTurnsRemaining,
                    Creature,
                    null);
            }

            return;
        }

        int threshold = GetShadowDamageThreshold();
        if (AccumulatedDamage >= threshold)
        {
            AccumulatedDamage = 0;
            ShadowTurnsRemaining = ShadowEnemyTurns;
            ResetShadowCardCounters();
            await PowerCmdCompat.Ensure<LanguageFloorShadowWolfPower>(
                Creature,
                ShadowEnemyTurns);
            PlanTurn(RunRng.MonsterAi);
            SetCompositeMoveAndRefresh();
            LocalOggOneShotPlayer.Play(ShadowSfx);
            await ApplyVisualState();
        }
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        await base.AfterDamageReceived(
            choiceContext,
            target,
            result,
            props,
            dealer,
            cardSource);
        if (target == Creature
            && Form == LanguageFloorCobaltScarForm.BigBadWolf
            && ShadowTurnsRemaining == 0
            && !ShadowReleasePending
            && result.UnblockedDamage > 0)
        {
            AccumulatedDamage += result.UnblockedDamage;
        }
    }

    public override Task AfterCurrentChaoValueChanged(
        Creature target,
        decimal amount,
        LibraryDamageType type)
    {
        if (target != Creature
            || amount >= 0m
            || _resolvingSwallowedCards
            || !SwallowWindowActive
            || SwallowedCards.Count == 0
            || target is not LibraryCreature { CurrentChaoValue: <= 0 })
        {
            return Task.CompletedTask;
        }

        return ReturnSwallowedCards(increaseCost: true, applyHpLoss: true);
    }

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Creature
            || amount <= 0m
            || Form == LanguageFloorCobaltScarForm.BigBadWolf)
        {
            return amount;
        }

        decimal maxLoss = Creature.CurrentHp - TransformHpThreshold(Creature.MaxHp);
        return maxLoss > 0m ? Math.Min(amount, maxLoss) : 0m;
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);
        if (creature == Creature
            && delta < 0m
            && Form == LanguageFloorCobaltScarForm.CobaltScar
            && Creature.CurrentHp < TransformHpThreshold(Creature.MaxHp))
        {
            await CreatureCmd.SetCurrentHp(
                Creature,
                TransformHpThreshold(Creature.MaxHp));
        }
    }

    public override bool ShouldDie(Creature creature)
    {
        if (creature != Creature
            || Form == LanguageFloorCobaltScarForm.BigBadWolf)
        {
            return true;
        }

        return Creature.CurrentHp > TransformHpThreshold(Creature.MaxHp)
            && !WouldBeKilledByDoom();
    }

    // Doom checks ShouldDie before Kill zeroes HP. The CobaltScar form is
    // under the 50% lock, so a lethal Doom must already count as prevented
    // and restore to the transform threshold instead of removing the node.
    private bool WouldBeKilledByDoom() =>
        Creature.GetPower<DoomPower>() is { } doom
        && Creature.CurrentHp <= doom.Amount;

    public override Task AfterPreventingDeath(Creature creature)
    {
        return creature == Creature
            && Form == LanguageFloorCobaltScarForm.CobaltScar
                ? CreatureCmd.SetCurrentHp(
                    Creature,
                    TransformHpThreshold(Creature.MaxHp))
                : Task.CompletedTask;
    }

    private async Task ResolveOpeningSwallow()
    {
        OpeningResolved = true;
        await SwallowCards();
        PlanOpeningCounterTurn();
        SetCompositeMoveAndRefresh();
    }

    private async Task SwallowCards()
    {
        ICombatState combatState = CombatState;
        List<SerializableCard> snapshots = [];
        List<int> ownerIndexes = [];
        Player[] players = combatState.Players
            .Where(static player => player.Creature.IsAlive)
            .OrderBy(static player => player.NetId)
            .ToArray();
        Player[] allPlayers = combatState.Players.ToArray();
        int candidateCount = players.Sum(player => CardPile
            .GetCards(player, PileType.Draw, PileType.Discard)
            .Count());
        Log.Info(
            $"[LanguageFloorSwallow] begin players={players.Length} candidates={candidateCount} "
            + $"cards_per_player={CardsSwallowedPerPlayer}");
        if (candidateCount > 0)
        {
            LocalOggOneShotPlayer.Play(BiteSfx);
        }

        int swallowedSequence = 0;
        foreach (Player player in players)
        {
            List<CardModel> candidates = CardPile
                .GetCards(player, PileType.Draw, PileType.Discard)
                .ToList();
            int swallowedForPlayer = 0;
            for (int count = 0;
                 count < CardsSwallowedPerPlayer && candidates.Count > 0;
                 count++)
            {
                CardModel selected =
                    RunRng.CombatCardGeneration.NextItem(candidates)
                    ?? throw new InvalidOperationException(
                        "Swallow candidate selection returned null.");
                candidates.Remove(selected);
                snapshots.Add(selected.ToSerializable());
                ownerIndexes.Add(Array.IndexOf(allPlayers, player));
                swallowedSequence++;
                PileType sourcePile = selected.Pile?.Type ?? PileType.None;
                CardPileAddResult exhaustResult = await CardPileCmd.Add(
                    selected,
                    PileType.Exhaust);
                if (!exhaustResult.success)
                {
                    throw new InvalidOperationException(
                        $"Failed to exhaust swallowed card {selected.Id.Entry}.");
                }

                await CardPileCmd.RemoveFromCombat(selected, skipVisuals: true);
                Log.Info(
                    $"[LanguageFloorSwallow] card_exhaust sequence={swallowedSequence} "
                    + $"card={selected.Id.Entry} source={sourcePile}");
                swallowedForPlayer++;
            }

            Log.Info(
                $"[LanguageFloorSwallow] player={player.NetId} candidates={candidates.Count + swallowedForPlayer} "
                + $"swallowed={swallowedForPlayer}");
        }

        SwallowedCards = snapshots;
        SwallowedOwnerIndexes = ownerIndexes.ToArray();
        SwallowWindowActive = SwallowedCards.Count > 0;
        PlayerTurnsSinceSwallow = 0;
        if (!SwallowWindowActive)
        {
            Log.Info("[LanguageFloorSwallow] complete swallowed=0 window_active=false");
            return;
        }

        int enhancedMaxChao = LanguageFloorPunishEvilPassivePower.ChaoResistanceIncrease;
        await SetMaxAndCurrentChao(enhancedMaxChao);
        await SetAllChaoResistances(
            new BlockingPlayerChoiceContext(),
            LibraryResistanceLevel.Fatal);
        Log.Info(
            $"[LanguageFloorSwallow] complete swallowed={SwallowedCards.Count} "
            + $"window_active={SwallowWindowActive} chao={enhancedMaxChao}");
    }

    private async Task ReturnSwallowedCards(bool increaseCost, bool applyHpLoss)
    {
        if (SwallowedCards.Count == 0 || _resolvingSwallowedCards)
        {
            await RestoreCobaltChaoAfterSwallow();
            return;
        }

        _resolvingSwallowedCards = true;
        try
        {
            LocalOggOneShotPlayer.Play(SpitSfx);
            ICombatState? combatState = Creature.CombatState;
            Player[] players = combatState?.Players.ToArray() ?? [];
            for (int index = 0; index < SwallowedCards.Count; index++)
            {
                int ownerIndex = index < SwallowedOwnerIndexes.Length
                    ? SwallowedOwnerIndexes[index]
                    : -1;
                if (ownerIndex < 0
                    || ownerIndex >= players.Length
                    || !players[ownerIndex].Creature.IsAlive)
                {
                    continue;
                }

                Player owner = players[ownerIndex];
                CardModel restored = CardModel.FromSerializable(SwallowedCards[index]);
                combatState!.AddCard(restored, owner);
                if (increaseCost && !restored.EnergyCost.CostsX)
                {
                    restored.EnergyCost.AddThisCombat(
                        LanguageFloorPunishEvilPassivePower.SpatCardCostIncrease);
                    restored.InvokeEnergyCostChanged();
                }

                await CardPileCmd.Add(
                    restored,
                    PileType.Hand,
                    CardPilePosition.Bottom,
                    this);
            }

            ClearSwallowedCardState();
            await RestoreCobaltChaoAfterSwallow();
            if (applyHpLoss && Creature.IsAlive)
            {
                int hpLoss = CalculateSpitHpLoss(Creature.MaxHp);
                await CreatureCmd.SetCurrentHp(
                    Creature,
                    Math.Max(
                        Form == LanguageFloorCobaltScarForm.CobaltScar
                            ? TransformHpThreshold(Creature.MaxHp)
                            : 0,
                        Creature.CurrentHp - hpLoss));
            }
        }
        finally
        {
            _resolvingSwallowedCards = false;
        }
    }

    internal static int CalculateSpitHpLoss(decimal maxHp) =>
        Math.Max(
            1,
            (int)Math.Ceiling(
                maxHp
                * LanguageFloorPunishEvilPassivePower.SpitHpLossPercent
                / 100m));

    private async Task ConsumeSwallowedCards()
    {
        ClearSwallowedCardState();
        await RestoreCobaltChaoAfterSwallow();
    }

    private void ClearSwallowedCardState()
    {
        SwallowedCards = [];
        SwallowedOwnerIndexes = [];
        SwallowWindowActive = false;
        PlayerTurnsSinceSwallow = 0;
    }

    private async Task RestoreCobaltChaoAfterSwallow()
    {
        if (Form != LanguageFloorCobaltScarForm.CobaltScar)
        {
            return;
        }

        await SetMaxAndCurrentChao(CobaltMaxChao);
        await SetCobaltBaseChaoResistances(new BlockingPlayerChoiceContext());
    }

    private async Task TransformToBigBadWolf()
    {
        if (Form == LanguageFloorCobaltScarForm.BigBadWolf)
        {
            return;
        }

        Form = LanguageFloorCobaltScarForm.BigBadWolf;
        BigWolfEntryHp = Creature.CurrentHp;
        AccumulatedDamage = 0;
        ForceInstinctNextTurn = true;
        TurnsUntilInstinct = 0;
        ShadowTurnsRemaining = 0;
        ShadowReleasePending = false;
        ForceRoarNextTurn = false;
        ResetShadowCardCounters();
        LocalOggOneShotPlayer.Play(TransformSfx);
        await SetMaxAndCurrentChao(BigWolfMaxChao);
        await SetAllChaoResistances(
            new BlockingPlayerChoiceContext(),
            LibraryResistanceLevel.Resist);
        await EnsureFormPowers();
        await ApplyVisualState();
    }

    private async Task EnsureFormPowers()
    {
        await PowerCmdCompat.Ensure<LanguageFloorRipOpenClawPassivePower>(
            Creature);
        if (Form == LanguageFloorCobaltScarForm.CobaltScar)
        {
            await PowerCmdCompat.Ensure<
                LanguageFloorPunishEvilPassivePower>(Creature);
            await PowerCmdCompat.Ensure<
                LanguageFloorDestinedBigBadWolfPassivePower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorHideInDarknessPassivePower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorShadowAmbushPassivePower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorExhaustionPassivePower>(Creature);
            return;
        }

        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorPunishEvilPassivePower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorDestinedBigBadWolfPassivePower>(Creature);
        await PowerCmdCompat.Ensure<
            LanguageFloorHideInDarknessPassivePower>(Creature);
        await PowerCmdCompat.Ensure<
            LanguageFloorShadowAmbushPassivePower>(Creature);
        await PowerCmdCompat.Ensure<
            LanguageFloorExhaustionPassivePower>(Creature);
    }

    internal bool IsShadowCardRestrictionActive =>
        Creature.IsAlive
        && Form == LanguageFloorCobaltScarForm.BigBadWolf
        && Creature.GetPower<LanguageFloorShadowWolfPower>() is { Amount: > 0 }
        && !ShadowReleasePending;

    internal int GetShadowCardsPlayed(Player player)
    {
        int playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0)
        {
            return 0;
        }

        EnsureShadowCardCounters();
        return ShadowCardsPlayedByPlayer[playerIndex];
    }

    internal void RecordShadowCardPlayed(Player player)
    {
        if (!IsShadowCardRestrictionActive)
        {
            return;
        }

        int playerIndex = GetPlayerIndex(player);
        if (playerIndex < 0)
        {
            return;
        }

        EnsureShadowCardCounters();
        ShadowCardsPlayedByPlayer[playerIndex]++;
    }

    internal void ResetShadowCardCounters()
    {
        int playerCount = Creature.CombatState?.Players.Count ?? 0;
        ShadowCardsPlayedByPlayer = new int[playerCount];
    }

    private void EnsureShadowCardCounters()
    {
        int playerCount = Creature.CombatState?.Players.Count ?? 0;
        if (ShadowCardsPlayedByPlayer.Length == playerCount)
        {
            return;
        }

        int[] resized = new int[playerCount];
        Array.Copy(
            ShadowCardsPlayedByPlayer,
            resized,
            Math.Min(ShadowCardsPlayedByPlayer.Length, resized.Length));
        ShadowCardsPlayedByPlayer = resized;
    }

    private int GetPlayerIndex(Player player)
    {
        IReadOnlyList<Player>? players = Creature.CombatState?.Players;
        if (players == null)
        {
            return -1;
        }

        for (int i = 0; i < players.Count; i++)
        {
            if (ReferenceEquals(players[i], player)
                || players[i].NetId == player.NetId)
            {
                return i;
            }
        }

        return -1;
    }

    private async Task ApplyVisualState()
    {
        if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals
            is LanguageFloorCobaltScarCreatureVisuals visuals)
        {
            visuals.SetForm(
                Form,
                ShadowTurnsRemaining > 0 || ShadowReleasePending);
        }

        await Task.CompletedTask;
    }

    // 开始前按当前形态的意图数对计划做快照再逐个执行，不在每招后检查 PhaseComplete。
    private Task PerformCompositeMove(IReadOnlyList<Creature> targets) =>
        Plan.PerformPlan(
            () => Creature.IsAlive,
            (_, move) => PerformMove(move),
            slotLimit: GetCurrentIntentCount(),
            readAllFirst: true);

    private async Task PerformMove(LanguageFloorMoveKind move)
    {
        switch (move)
        {
            case LanguageFloorMoveKind.CobaltDoNotProvoke:
                await CreatureCmd.GainBlock(
                    Creature,
                    DoNotProvokeBlock,
                    ValueProp.Move,
                    null);
                foreach (Creature player in GetLivingPlayers())
                {
                    await LibraryPowerCmd.Apply<LibraryBindingPower>(
                        player,
                        BindingAmount,
                        BindingTurns,
                        Creature,
                        null);
                }
                break;
            case LanguageFloorMoveKind.CobaltCough:
                await ExecuteTargetedHits(
                    GetMoveDamage(move),
                    GetMoveHitCount(move),
                    ["CobaltStrike", "CobaltSlash"]);
                break;
            case LanguageFloorMoveKind.CobaltSharpClaws:
                await ExecuteTargetedHits(
                    GetMoveDamage(move),
                    GetMoveHitCount(move),
                    ["CobaltSlash", "CobaltStrike"]);
                break;
            case LanguageFloorMoveKind.CobaltWolfComes:
                break;
            case LanguageFloorMoveKind.BigWolfHorrifyingClaws:
            {
                IReadOnlyList<DamageResult> results = await ExecuteTargetedHits(
                    GetMoveDamage(move),
                    GetMoveHitCount(move),
                    ["BigWolfSlash", "BigWolfStrike"]);
                Creature? target = results
                    .FirstOrDefault(static result => result.UnblockedDamage > 0)
                    ?.Receiver;
                if (target?.IsAlive == true)
                {
                    await PowerCmdCompat.Apply<LibraryBleedingPower>(
                        target,
                        HorrifyingClawsBleed,
                        Creature,
                        null);
                }

                await CreatureCmd.GainBlock(
                    Creature,
                    HorrifyingClawsBlock,
                    ValueProp.Move,
                    null);
                break;
            }
            case LanguageFloorMoveKind.BigWolfBrutalFangs:
                LocalOggOneShotPlayer.Play(GuardSfx);
                await CreatureCmd.TriggerAnim(Creature, "BigWolfGuard", 0.25f);
                await CreatureCmd.GainBlock(
                    Creature,
                    BrutalFangsBlock,
                    ValueProp.Move,
                    null);
                foreach (Creature player in GetLivingPlayers())
                {
                    await PowerCmdCompat.Apply<StrengthPower>(
                        player,
                        -BrutalFangsStatLoss,
                        Creature,
                        null);
                    await PowerCmdCompat.Apply<DexterityPower>(
                        player,
                        -BrutalFangsStatLoss,
                        Creature,
                        null);
                }
                break;
            case LanguageFloorMoveKind.BigWolfBloodstainedHunt:
            {
                Creature? plannedTarget = GetRandomPlayer();
                int existingScar = plannedTarget?
                    .GetPower<LanguageFloorScarPower>()?.Amount ?? 0;
                IReadOnlyList<DamageResult> results = await ExecuteTargetedHits(
                    GetMoveDamage(move),
                    GetMoveHitCount(move),
                    ["BigWolfStrike"],
                    plannedTarget);
                Creature? target = results
                    .FirstOrDefault(static result => result.UnblockedDamage > 0)
                    ?.Receiver;
                if (target?.IsAlive == true)
                {
                    await PowerCmdCompat.ApplyDebuff<LibraryOfRuinaDrawCardsNextTurnPower>(
                        target,
                        BloodstainedHuntDrawLoss,
                        Creature,
                        null);
                    int desiredTotal = existingScar + BloodstainedHuntScarTotal;
                    await PowerCmdCompat.SetAmount<LanguageFloorScarPower>(
                        target,
                        desiredTotal,
                        Creature,
                        null);
                }
                break;
            }
            case LanguageFloorMoveKind.BigWolfShadowAssault:
            {
                await ExecuteTargetedHits(
                    GetMoveDamage(move),
                    GetMoveHitCount(move),
                    ["ShadowAssault"],
                    GetRandomPlayer());
                await CreatureCmd.Heal(
                    Creature,
                    (int)Math.Ceiling(
                        Creature.MaxHp * ShadowAssaultHealPercent / 100m));
                break;
            }
            case LanguageFloorMoveKind.BigWolfUncontrollableInstinct:
                await ExecuteUncontrollableInstinct();
                break;
            case LanguageFloorMoveKind.BigWolfRoar:
                await ExecuteRoar();
                break;
        }
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteTargetedHits(
        int damage,
        int repeats,
        IReadOnlyList<string> animations,
        Creature? forcedTarget = null)
    {
        if (animations.Count == 0)
        {
            throw new ArgumentException(
                "At least one attack animation is required.",
                nameof(animations));
        }

        List<DamageResult> allResults = [];
        for (int repeat = 0; repeat < repeats && Creature.IsAlive; repeat++)
        {
            Creature? target = forcedTarget?.IsAlive == true
                ? forcedTarget
                : GetRandomPlayer();
            if (target == null)
            {
                break;
            }

            LocalOggOneShotPlayer.Play(ScratchSfx);
            using var forcedTargets =
                TargetedMonsterAttackHelper.ForceTargets(Creature, [target]);
            AttackCommand command = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim(
                    GetSegmentAnimation(animations, repeat),
                    AttackDelay)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
            allResults.AddRange(AttackCommandCompat.Results(command));
        }

        return allResults;
    }

    internal static string GetSegmentAnimation(
        IReadOnlyList<string> animations,
        int segment) =>
        animations[segment % animations.Count];

    private async Task ExecuteUncontrollableInstinct()
    {
        Creature? target = GetRandomPlayer();
        if (target == null)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(ScratchSfx);
        using var forcedTargets =
            TargetedMonsterAttackHelper.ForceTargets(Creature, [target]);
        await DamageCmd.Attack(GetMoveDamage(
                LanguageFloorMoveKind.BigWolfUncontrollableInstinct))
            .FromMonster(this)
            .WithHitCount(GetMoveHitCount(
                LanguageFloorMoveKind.BigWolfUncontrollableInstinct))
            .WithAttackerAnim("BigWolfS1", AttackDelay)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    private async Task ExecuteRoar()
    {
        IReadOnlyList<Creature> players = GetLivingPlayers();
        if (players.Count == 0)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(RoarSfx);
        using var forcedTargets =
            TargetedMonsterAttackHelper.ForceTargets(Creature, players);
        AttackCommand command = await DamageCmd.Attack(
                GetMoveDamage(LanguageFloorMoveKind.BigWolfRoar))
            .FromMonster(this)
            .WithHitCount(GetMoveHitCount(LanguageFloorMoveKind.BigWolfRoar))
            .WithAttackerAnim("BigWolfS2", RoarDelay)
            .WithHitFx("vfx/vfx_attack_blunt")
            .SpawningHitVfxOnEachCreature()
            .Execute(null);
        foreach (Creature target in players)
        {
            PowerModel[] positivePowers = target.Powers
                .Where(static power =>
                    power.IsVisible
                    && power.TypeForCurrentAmount == PowerType.Buff)
                .ToArray();
            if (positivePowers.Length > 0)
            {
                await PowerCmd.Remove(
                    RunRng.MonsterAi.NextItem(positivePowers));
            }
        }
    }

    private void PlanOpeningCounterTurn()
    {
        Plan.WriteSlots(IntentCount, static _ => LanguageFloorMoveKind.CobaltWolfComes);
        Plan.RefreshIntents();
    }

    // 按槽位顺序掷骰；影子形态两个槽位固定为影袭、第三个写空，咆哮与本能占第一个槽位时不消耗随机数。
    private void PlanTurn(Rng rng)
    {
        if (ShadowTurnsRemaining > 0)
        {
            Plan.WriteSlots(ShadowIntentCount, static _ => LanguageFloorMoveKind.BigWolfShadowAssault);
            Plan.RefreshIntents();
            return;
        }

        if (Form == LanguageFloorCobaltScarForm.CobaltScar)
        {
            Plan.WriteSlots(IntentCount, _ => NextCobaltMove(rng));
            Plan.RefreshIntents();
            return;
        }

        bool useInstinct = ForceInstinctNextTurn || TurnsUntilInstinct <= 0;
        bool useRoar = ForceRoarNextTurn;
        Plan.WriteSlots(
            IntentCount,
            slot => slot != 0
                ? NextBigWolfMove(rng)
                : useRoar
                    ? LanguageFloorMoveKind.BigWolfRoar
                    : useInstinct
                        ? LanguageFloorMoveKind.BigWolfUncontrollableInstinct
                        : NextBigWolfMove(rng));
        ForceRoarNextTurn = false;
        if (useRoar)
        {
            // Roar occupies the special slot; a due Instinct is postponed rather than consumed.
        }
        else if (useInstinct)
        {
            ForceInstinctNextTurn = false;
            TurnsUntilInstinct = SpecialAttackInterval - 1;
        }
        else if (TurnsUntilInstinct > 0)
        {
            TurnsUntilInstinct--;
        }

        Plan.RefreshIntents();
    }

    internal void RefreshAfterMoveRoll()
    {
        Creature.GetPower<LanguageFloorShadowAmbushPassivePower>()
            ?.RefreshCardLimit();
        ClearCounterIntentQueueAndRefresh(forceRefresh: true);
        PrepareCounterIntentsFromCurrentMove();
    }

    // 揭示之后要按新行动刷新影袭的卡牌上限并准备反击意图，这些不属于计划控制器。
    private void SetCompositeMoveAndRefresh()
    {
        if (_normalCompositeState == null || _shadowCompositeState == null)
        {
            return;
        }

        Plan.RefreshIntents();
        Plan.Reveal(GetCurrentCompositeState());
        Creature.GetPower<LanguageFloorShadowAmbushPassivePower>()
            ?.RefreshCardLimit();
        if (NCombatRoom.Instance?.GetCreatureNode(Creature) is { } node)
        {
            TaskHelper.RunSafely(node.RefreshIntents());
        }

        PrepareCounterIntentsFromCurrentMove();
    }

    private MoveState GetCurrentCompositeState() =>
        ShadowTurnsRemaining > 0
            ? _shadowCompositeState
                ?? throw new InvalidOperationException("Shadow move state is not initialized.")
            : _normalCompositeState
                ?? throw new InvalidOperationException("Normal move state is not initialized.");

    private int GetCurrentIntentCount() =>
        ShadowTurnsRemaining > 0 ? ShadowIntentCount : IntentCount;

    internal LanguageFloorMoveKind GetPlannedMove(int slot)
    {
        int value = slot switch
        {
            0 => PlannedMoveOne,
            1 => PlannedMoveTwo,
            _ => PlannedMoveThree
        };
        return Enum.IsDefined(typeof(LanguageFloorMoveKind), value)
            ? (LanguageFloorMoveKind)value
            : LanguageFloorMoveKind.CobaltCough;
    }

    private void SetPlannedMove(int slot, LanguageFloorMoveKind move)
    {
        switch (slot)
        {
            case 0:
                PlannedMoveOne = (int)move;
                break;
            case 1:
                PlannedMoveTwo = (int)move;
                break;
            default:
                PlannedMoveThree = (int)move;
                break;
        }
    }

    internal IReadOnlyList<LanguageFloorMoveKind> PlannedMoves =>
        Enumerable.Range(0, GetCurrentIntentCount())
            .Select(GetPlannedMove)
            .ToArray();

    internal int GetShadowDamageThreshold() =>
        Math.Max(
            1,
            (int)Math.Ceiling(
                Math.Max(1, BigWolfEntryHp)
                * ShadowThresholdPercent
                / 100m));

    internal static int TransformHpThreshold(int maxHp) =>
        Math.Max(
            1,
            (int)Math.Ceiling(maxHp * TransformHpPercent / 100m));

    internal static bool ReachesShadowThreshold(
        int entryHp,
        int damage) =>
        damage >= Math.Max(
            1,
            (int)Math.Ceiling(entryHp * ShadowThresholdPercent / 100m));

    internal void DebugSetState(
        LanguageFloorCobaltScarForm form,
        int bigWolfEntryHp,
        int accumulatedDamage,
        int shadowTurnsRemaining,
        params LanguageFloorMoveKind[] moves)
    {
        Form = form;
        BigWolfEntryHp = bigWolfEntryHp;
        AccumulatedDamage = accumulatedDamage;
        ShadowTurnsRemaining = shadowTurnsRemaining;
        PlannedMoveOne = moves.Length > 0 ? (int)moves[0] : -1;
        PlannedMoveTwo = moves.Length > 1 ? (int)moves[1] : -1;
        PlannedMoveThree = moves.Length > 2 ? (int)moves[2] : -1;
        Plan.RefreshIntents();
    }

    internal Task DebugTransformToBigBadWolf() =>
        TransformToBigBadWolf();

    internal Task DebugPerformMove(LanguageFloorMoveKind move) =>
        PerformMove(move);

    internal Task DebugReturnSwallowedCards(bool applyHpLoss) =>
        ReturnSwallowedCards(increaseCost: true, applyHpLoss);

    internal Task DebugConsumeSwallowedCards() =>
        ConsumeSwallowedCards();

    internal void DebugPlanTurn()
    {
        PlanTurn(RunRng.MonsterAi);
        SetCompositeMoveAndRefresh();
    }

    private bool HasPlannedTurn =>
        PlannedMoveOne >= 0
        && PlannedMoveTwo >= 0
        && (GetCurrentIntentCount() == 2 || PlannedMoveThree >= 0);

    private bool IsOpeningCounterPlan =>
        OpeningResolved
        && Form == LanguageFloorCobaltScarForm.CobaltScar
        && PlannedMoveOne == (int)LanguageFloorMoveKind.CobaltWolfComes
        && PlannedMoveTwo == (int)LanguageFloorMoveKind.CobaltWolfComes
        && PlannedMoveThree == (int)LanguageFloorMoveKind.CobaltWolfComes;

    private Creature? GetRandomPlayer()
    {
        IReadOnlyList<Creature> players = GetLivingPlayers();
        return players.Count == 0
            ? null
            : RunRng.MonsterAi.NextItem(players);
    }

    private IReadOnlyList<Creature> GetLivingPlayers() =>
        LanguageFloorLiberationCombatHelper.GetLivingPlayers(Creature);

    private async Task SetMaxAndCurrentChao(int amount)
    {
        if (Creature is LibraryCreature libraryCreature)
        {
            await MultiplayerScalingPatchHelper.SetMonsterBaseMaxAndCurrentChaoValue(
                libraryCreature,
                amount);
        }
    }

    private Task SetCobaltBaseChaoResistances(
        PlayerChoiceContext choiceContext)
    {
        return SetChaoResistances(
            choiceContext,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Endure,
            LibraryResistanceLevel.Normal);
    }

    private Task SetAllChaoResistances(
        PlayerChoiceContext choiceContext,
        LibraryResistanceLevel resistance)
    {
        return SetChaoResistances(
            choiceContext,
            resistance,
            resistance,
            resistance);
    }

    private async Task SetChaoResistances(
        PlayerChoiceContext choiceContext,
        LibraryResistanceLevel slash,
        LibraryResistanceLevel pierce,
        LibraryResistanceLevel blunt)
    {
        if (Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        await LibraryCreatureCmd.SetChaoResistance(
            choiceContext,
            libraryCreature,
            Creature,
            LibraryDamageType.Slash,
            slash);
        await LibraryCreatureCmd.SetChaoResistance(
            choiceContext,
            libraryCreature,
            Creature,
            LibraryDamageType.Pierce,
            pierce);
        await LibraryCreatureCmd.SetChaoResistance(
            choiceContext,
            libraryCreature,
            Creature,
            LibraryDamageType.Blunt,
            blunt);
    }

    private static LibraryCreatureResistanceData.Resistance CreateUniformResistance(
        LibraryResistanceLevel resistance) =>
        new()
        {
            Slash = resistance,
            Pierce = resistance,
            Blunt = resistance
        };

    private static LanguageFloorMoveKind NextCobaltMove(Rng rng)
    {
        LanguageFloorMoveKind[] candidates =
        [
            LanguageFloorMoveKind.CobaltDoNotProvoke,
            LanguageFloorMoveKind.CobaltCough,
            LanguageFloorMoveKind.CobaltSharpClaws
        ];
        return rng.NextItem(candidates);
    }

    private static LanguageFloorMoveKind NextBigWolfMove(Rng rng)
    {
        LanguageFloorMoveKind[] candidates =
        [
            LanguageFloorMoveKind.BigWolfHorrifyingClaws,
            LanguageFloorMoveKind.BigWolfBrutalFangs,
            LanguageFloorMoveKind.BigWolfBloodstainedHunt
        ];
        return rng.NextItem(candidates);
    }

    internal static int GetMoveDamage(LanguageFloorMoveKind move)
    {
        int normal = move switch
        {
            LanguageFloorMoveKind.CobaltCough => 3,
            LanguageFloorMoveKind.CobaltSharpClaws => 2,
            LanguageFloorMoveKind.CobaltWolfComes => 4,
            LanguageFloorMoveKind.BigWolfHorrifyingClaws => 3,
            LanguageFloorMoveKind.BigWolfBloodstainedHunt => 10,
            LanguageFloorMoveKind.BigWolfShadowAssault => 11,
            LanguageFloorMoveKind.BigWolfUncontrollableInstinct => 2,
            LanguageFloorMoveKind.BigWolfRoar => 14,
            _ => 0
        };
        int ascended = move switch
        {
            LanguageFloorMoveKind.CobaltCough => 4,
            LanguageFloorMoveKind.CobaltSharpClaws => 3,
            LanguageFloorMoveKind.CobaltWolfComes => 5,
            LanguageFloorMoveKind.BigWolfHorrifyingClaws => 4,
            LanguageFloorMoveKind.BigWolfBloodstainedHunt => 11,
            LanguageFloorMoveKind.BigWolfShadowAssault => 12,
            LanguageFloorMoveKind.BigWolfUncontrollableInstinct => 3,
            LanguageFloorMoveKind.BigWolfRoar => 16,
            _ => normal
        };
        return AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            ascended,
            normal);
    }

    internal static int GetMoveHitCount(LanguageFloorMoveKind move) =>
        move switch
        {
            LanguageFloorMoveKind.CobaltCough => 2,
            LanguageFloorMoveKind.CobaltSharpClaws => 3,
            LanguageFloorMoveKind.CobaltWolfComes => 2,
            LanguageFloorMoveKind.BigWolfHorrifyingClaws => 3,
            LanguageFloorMoveKind.BigWolfBloodstainedHunt => 1,
            LanguageFloorMoveKind.BigWolfShadowAssault => 1,
            LanguageFloorMoveKind.BigWolfUncontrollableInstinct => 5,
            LanguageFloorMoveKind.BigWolfRoar => 1,
            _ => 0
        };

    internal static AbstractIntent CreateIntent(LanguageFloorMoveKind move)
    {
        int damage = GetMoveDamage(move);
        int hitCount = GetMoveHitCount(move);
        return move switch
        {
            LanguageFloorMoveKind.CobaltDoNotProvoke =>
                new CombinedDefendDebuffIntent(
                    DoNotProvokeBlock,
                    "LANGUAGE_FLOOR_COBALT_DO_NOT_PROVOKE.description",
                    IntentBadge.Bind(BindingAmount)),
            LanguageFloorMoveKind.CobaltCough =>
                new MultiAttackIntent(damage, hitCount),
            LanguageFloorMoveKind.CobaltSharpClaws =>
                new BadgedAttackIntent(
                    damage,
                    hitCount,
                    "LANGUAGE_FLOOR_COBALT_SHARP_CLAWS.description",
                    IntentBadge.FromPower<LanguageFloorScarPower>(3)),
            LanguageFloorMoveKind.CobaltWolfComes =>
                new LanguageFloorWolfComesIntent(damage),
            LanguageFloorMoveKind.BigWolfHorrifyingClaws =>
                new CombinedAttackDefendIntent(
                    damage,
                    hitCount,
                    "LANGUAGE_FLOOR_BIG_WOLF_HORRIFYING_CLAWS.description",
                    HorrifyingClawsBlock,
                    IntentBadge.Bleed(HorrifyingClawsBleed)),
            LanguageFloorMoveKind.BigWolfBrutalFangs =>
                new CombinedDefendDebuffIntent(
                    BrutalFangsBlock,
                    "LANGUAGE_FLOOR_BIG_WOLF_BRUTAL_FANGS.description",
                    IntentBadge.Strength(-BrutalFangsStatLoss),
                    IntentBadge.FromPower<DexterityPower>(-BrutalFangsStatLoss)),
            LanguageFloorMoveKind.BigWolfBloodstainedHunt =>
                new BadgedAttackIntent(
                    damage,
                    hitCount,
                    "LANGUAGE_FLOOR_BIG_WOLF_BLOODSTAINED_HUNT.description",
                    IntentBadge.FromPower<LibraryOfRuinaDrawCardsNextTurnPower>(
                        BloodstainedHuntDrawLoss),
                    IntentBadge.FromPower<LanguageFloorScarPower>(
                        BloodstainedHuntScarTotal)),
            LanguageFloorMoveKind.BigWolfShadowAssault =>
                new BadgedAttackIntent(
                    damage,
                    hitCount,
                    "LANGUAGE_FLOOR_BIG_WOLF_SHADOW_ASSAULT.description",
                    IntentBadge.Heal()),
            LanguageFloorMoveKind.BigWolfUncontrollableInstinct =>
                new MultiAttackIntent(damage, hitCount),
            LanguageFloorMoveKind.BigWolfRoar =>
                new IndiscriminateAttackIntent(
                    damage,
                    hitCount,
                    "LANGUAGE_FLOOR_COBALT_ROAR.description",
                    static owner =>
                        LanguageFloorLiberationCombatHelper.GetLivingPlayers(owner)),
            _ => throw new ArgumentOutOfRangeException(nameof(move), move, null)
        };
    }

    private static IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        foreach (LanguageFloorMoveKind move in Enum.GetValues<LanguageFloorMoveKind>()
                     .Where(static move => move >= LanguageFloorMoveKind.CobaltDoNotProvoke))
        {
            yield return CreateIntent(move);
        }
    }

    private sealed class LanguageFloorWolfComesIntent
        : AttackIntent,
            ICounterIntent,
            ICounterIntentQueueMultiplicity,
            ICombinedIntentVisual,
            ICombinedIntentHoverIcon
    {
        private readonly int _damage;
        private int _counterAnimationIndex;

        public LanguageFloorWolfComesIntent(int damage)
        {
            _damage = damage;
            DamageCalc = () => _damage;
        }

        public override int Repeats =>
            GetMoveHitCount(LanguageFloorMoveKind.CobaltWolfComes);

        public int CounterQueueCount => Repeats;

        protected override string IntentPrefix =>
            "LANGUAGE_FLOOR_COBALT_WOLF_COMES";

        public string HoverIconPath =>
            CombinedIntentAnimData.GetHoverIconPath(
                CombinedIntentAnimData.CounterAttackBuff);

        public string HoverIntentPrefix => IntentPrefix;

        public override IEnumerable<string> AssetPaths =>
            CombinedIntentAnimData.GetAssetPaths(
                    CombinedIntentAnimData.CounterAttackBuff)
                .Append(HoverIconPath);

        public string CounterAnimationFramePath =>
            CombinedIntentAnimData.GetIconPath(
                CombinedIntentAnimData.CounterAttackBuff,
                tier: 1);

        public string CounterAnimation =>
            CombinedIntentAnimData.GetAnimationKey(
                CombinedIntentAnimData.CounterAttackBuff,
                tier: 1);

        public string GetCombinedAnimation(
            IEnumerable<Creature> targets,
            Creature owner)
        {
            int tier = CombinedIntentAnimData.GetAttackTier(
                GetTotalDamage(targets, owner));
            return CombinedIntentAnimData.GetAttackAnimationKey(
                CombinedIntentAnimData.CounterAttackBuff,
                tier);
        }

        public override Texture2D GetTexture(
            IEnumerable<Creature> targets,
            Creature owner)
        {
            int tier = CombinedIntentAnimData.GetAttackTier(
                GetTotalDamage(targets, owner));
            return PreloadManager.Cache.GetTexture2D(
                CombinedIntentAnimData.GetIconPath(
                    CombinedIntentAnimData.CounterAttackBuff,
                    tier));
        }

        public override string GetAnimation(
            IEnumerable<Creature> targets,
            Creature owner) =>
            IntentAnimData.buff;

        public override int GetTotalDamage(
            IEnumerable<Creature> targets,
            Creature owner) =>
            GetSingleDamage(targets, owner) * Repeats;

        public override LocString GetIntentLabel(
            IEnumerable<Creature> targets,
            Creature owner)
        {
            LocString label = new("intents", "FORMAT_DAMAGE_MULTI");
            label.Add("Damage", GetSingleDamage(targets, owner));
            label.Add("Repeat", Repeats);
            return label;
        }

        protected override LocString GetIntentDescription(
            IEnumerable<Creature> targets,
            Creature owner)
        {
            LocString desc = new(
                "intents",
                "LANGUAGE_FLOOR_COBALT_WOLF_COMES.description");
            desc.Add("Damage", GetSingleDamage(targets, owner));
            desc.Add("Repeat", Repeats);
            desc.Add("Strength", WolfComesStrength);
            return desc;
        }

        public async Task PerformCounterIntent(
            PlayerChoiceContext choiceContext,
            Creature owner,
            Creature counterTarget)
        {
            if (owner.IsDead || owner.Monster == null || counterTarget.IsDead)
            {
                return;
            }

            using var forcedTargets =
                TargetedMonsterAttackHelper.ForceTargets(owner, [counterTarget]);
            string animation = (_counterAnimationIndex++ & 1) == 0
                ? "CobaltSlash"
                : "CobaltStrike";
            await DamageCmd.Attack(_damage)
                .FromMonster(owner.Monster)
                .WithAttackerAnim(animation, AttackDelay)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            await PowerCmdCompat.Apply<StrengthPower>(
                owner,
                WolfComesStrength,
                owner,
                null);
        }
    }

}

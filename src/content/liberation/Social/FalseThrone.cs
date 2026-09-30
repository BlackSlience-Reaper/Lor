using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Social;

public enum FalseThroneMove
{
    InitialSequence,
    OverflowingLight,
    Insolence,
    AllSilent,
    Manners,
    FriendlyGreeting,
    BigMistake,
    FunIsOver,
    UnknownTrial,
    StunTrial,
    HomeOverflowingLightInsolence,
    HomeOverflowingLightAllSilent,
    HomeOverflowingLightManners,
    HomeInsolenceAllSilent,
    HomeInsolenceManners,
    HomeAllSilentManners
}

internal readonly record struct FalseThroneHomeMovePlan(
    FalseThroneMove Sequence,
    FalseThroneMove First,
    FalseThroneMove Second,
    string StateId);

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyMaxEnergy))]
[HarmonyAfter("LibraryOfRuinaLib")]
[HarmonyPriority(Priority.Last)]
[LibraryPatch(Reason = "樵夫试炼的能量上限是最终上限，要排在 LibraryOfRuinaLib 只有后缀的情感能量加成之后，FalseThrone 自身的 ModifyMaxEnergy 覆写排不到那里；只在本模组社会层解放遭遇的樵夫试炼中封顶。")]
internal static class FalseThroneWoodsmanMaxEnergyPatch
{
    private static void Postfix(Player player, ref decimal __result)
    {
        __result = FalseThrone.CapWoodsmanTrialMaxEnergy(player.Creature.CombatState?.Encounter, __result);
    }
}

public sealed class FalseThrone :
    LorMonsterModel,
    ISocialFloorMagicalPowderTarget, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    public const int NormalHp = 888;
    public const int ToughHp = 999;
    public const int ChaoResistance = 250;
    public const int OverflowingLightLowDamage = 6;
    public const int OverflowingLightHighDamage = 8;
    public const int OverflowingLightHits = 2;
    public const int OverflowingLightBleedPerHit = 6;
    internal const string OverflowingLightHitVfx =
        "vfx/vfx_attack_slash";
    public const int InsolenceLowDamage = 9;
    public const int InsolenceHighDamage = 10;
    public const int InsolenceBind = 13;
    public const int InsolenceBindTurns = 5;
    public const int AllSilentLowDamage = 13;
    public const int AllSilentHighDamage = 15;
    public const int AllSilentStrengthLoss = 5;
    public const int AllSilentDexterityLoss = 5;
    public const int MannersLowDamage = 8;
    public const int MannersHighDamage = 10;
    public const int MannersHits = 3;
    public const int MannersNormalWounds = 3;
    public const int MannersToughWounds = 4;
    public const int FriendlyGreetingLowDamage = 26;
    public const int FriendlyGreetingHighDamage = 29;
    public const int BigMistakeLowDamage = 29;
    public const int BigMistakeHighDamage = 33;
    public const int BigMistakeBleed = 9;
    public const int FunIsOverLowDamage = 45;
    public const int FunIsOverHighDamage = 50;
    public const int FunIsOverWeak = 999;
    public const int FunIsOverDisarm = 10;
    public const int FunIsOverDebuffTurns = 1;

    private const string RouterMoveId = "FALSE_THRONE_ROUTER";
    private Dictionary<FalseThroneMove, MoveState> _moves = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _moves = [];
    }

    internal static IReadOnlyList<FalseThroneHomeMovePlan> HomeMovePlans { get; } =
    [
        new(
            FalseThroneMove.HomeOverflowingLightInsolence,
            FalseThroneMove.OverflowingLight,
            FalseThroneMove.Insolence,
            "FALSE_THRONE_HOME_OVERFLOWING_LIGHT_INSOLENCE"),
        new(
            FalseThroneMove.HomeOverflowingLightAllSilent,
            FalseThroneMove.OverflowingLight,
            FalseThroneMove.AllSilent,
            "FALSE_THRONE_HOME_OVERFLOWING_LIGHT_ALL_SILENT"),
        new(
            FalseThroneMove.HomeOverflowingLightManners,
            FalseThroneMove.OverflowingLight,
            FalseThroneMove.Manners,
            "FALSE_THRONE_HOME_OVERFLOWING_LIGHT_MANNERS"),
        new(
            FalseThroneMove.HomeInsolenceAllSilent,
            FalseThroneMove.Insolence,
            FalseThroneMove.AllSilent,
            "FALSE_THRONE_HOME_INSOLENCE_ALL_SILENT"),
        new(
            FalseThroneMove.HomeInsolenceManners,
            FalseThroneMove.Insolence,
            FalseThroneMove.Manners,
            "FALSE_THRONE_HOME_INSOLENCE_MANNERS"),
        new(
            FalseThroneMove.HomeAllSilentManners,
            FalseThroneMove.AllSilent,
            FalseThroneMove.Manners,
            "FALSE_THRONE_HOME_ALL_SILENT_MANNERS")
    ];

    public bool UsesToughValues =>
        AscensionHelper.HasAscension(AscensionLevel.ToughEnemies);

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(
        AscensionLevel.ToughEnemies,
        ToughHp,
        NormalHp);

    public override int MaxInitialHp => MinInitialHp;

    public override int DefaultChaoResistance => ChaoResistance;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
    {
        Blunt = LibraryResistanceLevel.Immune,
        Pierce = LibraryResistanceLevel.Immune,
        Slash = LibraryResistanceLevel.Immune,
    };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => NormalResistance();

    public override bool ShouldDisappearFromDoom =>
        Encounter?.Trial == SocialFloorTrial.Rage;

    public override IEnumerable<string> AssetPaths =>
        FalseThroneCreatureVisuals
            .Profile.AssetPaths
            .Concat(EnumerateIntentAssets().SelectMany(static intent =>
                intent.AssetPaths))
            .Distinct();

    private SocialFloorLiberationEncounter? Encounter =>
        Creature?.CombatState?.Encounter as SocialFloorLiberationEncounter;

    internal bool IsHealthBarLockActive =>
        Encounter != null && Encounter.Trial != SocialFloorTrial.Rage;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (Encounter is { } encounter)
        {
            await encounter.InitializeCombat(
                new ThrowingPlayerChoiceContext(),
                this);
            if (encounter.Transformed)
            {
                await CreatureCmd.TriggerAnim(
                    Creature,
                    "Transformed",
                    0.01f);
            }
        }
        ForcePlannedMove(
            Encounter?.PlannedMove ?? FalseThroneMove.InitialSequence);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (Creature.IsAlive && Encounter is { } encounter)
        {
            if (TurnParticipants.IsRoundPlayerTurn(side))
            {
                await encounter.OnBeforePlayerTurn(this, combatState);
            }
            else if (side == CombatSide.Enemy)
            {
                await encounter.OnBeforeEnemyTurn(
                    choiceContext,
                    this,
                    combatState);
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
        if (side == CombatSide.Enemy
            && Creature.IsAlive
            && Creature.CombatState is { } combatState
            && Encounter is { } encounter)
        {
            await encounter.OnAfterEnemyTurn(
                choiceContext,
                this,
                combatState);
        }
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        await base.AfterDeath(
            choiceContext,
            creature,
            wasRemovalPrevented,
            deathAnimLength);
        if (!wasRemovalPrevented && Encounter is { } encounter)
        {
            await encounter.NotifyCreatureDeath(creature);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _moves.Clear();

        Register(
            FalseThroneMove.InitialSequence,
            new MoveState(
                "FALSE_THRONE_INITIAL_SEQUENCE",
                InitialSequence,
                new IndiscriminateAttackIntent(
                    () => FriendlyGreetingDamage,
                     () => 1,
                     "FALSE_THRONE_FRIENDLY_GREETING.description",
                     ResolveLivingPlayers),
                CreateOverflowingLightIntent(),
                CreateInsolenceIntent()));

        Register(
            FalseThroneMove.OverflowingLight,
            new MoveState(
                "FALSE_THRONE_OVERFLOWING_LIGHT",
                OverflowingLight,
                CreateOverflowingLightIntent()));

        Register(
            FalseThroneMove.Insolence,
            new MoveState(
                "FALSE_THRONE_INSOLENCE",
                Insolence,
                CreateInsolenceIntent()));

        Register(
            FalseThroneMove.AllSilent,
            new MoveState(
                "FALSE_THRONE_ALL_SILENT",
                AllSilent,
                CreateAllSilentIntent()));

        Register(
            FalseThroneMove.Manners,
            new MoveState(
                "FALSE_THRONE_MANNERS",
                Manners,
                CreateMannersIntents()));

        foreach (FalseThroneHomeMovePlan plan in HomeMovePlans)
        {
            FalseThroneHomeMovePlan capturedPlan = plan;
            Register(
                capturedPlan.Sequence,
                new MoveState(
                    capturedPlan.StateId,
                    targets => HomeSequence(
                        targets,
                        capturedPlan.First,
                        capturedPlan.Second),
                    CreateNormalIntents(capturedPlan.First)
                        .Concat(CreateNormalIntents(capturedPlan.Second))
                        .ToArray()));
        }

        Register(
            FalseThroneMove.FriendlyGreeting,
            new MoveState(
                "FALSE_THRONE_FRIENDLY_GREETING",
                FriendlyGreeting,
                new IndiscriminateAttackIntent(
                    () => FriendlyGreetingDamage,
                    () => 1,
                    "FALSE_THRONE_FRIENDLY_GREETING.description",
                    ResolveLivingPlayers)));

        Register(
            FalseThroneMove.BigMistake,
            new MoveState(
                "FALSE_THRONE_BIG_MISTAKE",
                BigMistake,
                new CombinedAttackDebuffIntent(
                    () => BigMistakeDamage,
                    () => 1,
                    "FALSE_THRONE_BIG_MISTAKE.description",
                    IndiscriminateAttackIntent.CreateGroupAttackBadge(),
                    IntentBadge.FromPower<LibraryBleedingPower>(
                        () => BigMistakeBleedAmount))));

        Register(
            FalseThroneMove.FunIsOver,
            new MoveState(
                "FALSE_THRONE_FUN_IS_OVER",
                FunIsOver,
                new CombinedAttackDebuffIntent(
                    () => FunIsOverDamage,
                    () => 1,
                    "FALSE_THRONE_FUN_IS_OVER.description",
                    IndiscriminateAttackIntent.CreateGroupAttackBadge(),
                    IntentBadge.FromPower<LibraryWeakPower>(
                        () => FunIsOverWeakAmount))));

        Register(
            FalseThroneMove.UnknownTrial,
            new MoveState(
                "FALSE_THRONE_UNKNOWN_TRIAL",
                _ => Task.CompletedTask,
                new UnknownIntent()));

        Register(
            FalseThroneMove.StunTrial,
            new MoveState(
                "FALSE_THRONE_STUN_TRIAL",
                _ => Task.CompletedTask,
                new StunIntent()));

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, _) => ResolveMoveId(
                Encounter?.PlannedMove
                ?? FalseThroneMove.InitialSequence));
        foreach (MoveState move in _moves.Values)
        {
            move.FollowUpState = router;
        }

        return new MonsterMoveStateMachine(
            [.._moves.Values, router],
            router);
    }

    public void ForcePlannedMove(FalseThroneMove move)
    {
        if (!IsMutable || !_moves.TryGetValue(move, out MoveState? state))
        {
            return;
        }

        SetMoveImmediate(state, forceTransition: true);
        _ = RefreshIntents();
    }

    internal FalseThroneMove RollHomeMove() =>
        RollHomeMove(RunRng.MonsterAi);

    internal static FalseThroneMove RollHomeMove(Rng rng) =>
        HomeMovePlans[rng.NextInt(HomeMovePlans.Count)].Sequence;

    internal async Task SyncTrialPowers()
    {
        foreach (PowerModel power in Creature.Powers
                     .Where(static power =>
                         power is FalseThroneTrialPowerBase)
                     .ToArray())
        {
            bool keep = power is FalseThroneWizardsTrialPower
                || Encounter?.Trial switch
                {
                    SocialFloorTrial.Woodsman =>
                        power is FalseThroneShowYourWarmHeartPower
                            or FalseThroneEmptyChestPower,
                    SocialFloorTrial.Scarecrow =>
                        power is FalseThroneShowYourWisdomPower
                            or FalseThroneInsignificantWisdomPower,
                    SocialFloorTrial.Lion =>
                        power is FalseThroneShowYourCouragePower
                            or FalseThroneTrulyCowardPower,
                    SocialFloorTrial.Home =>
                        power is FalseThroneWhatCanYouDoPower,
                    SocialFloorTrial.Rage => power is FalseThroneRagePower,
                    _ => false
                };
            if (!keep)
            {
                await PowerCmd.Remove(power);
            }
        }

        await PowerCmdCompat.Ensure<FalseThroneWizardsTrialPower>(Creature);
        switch (Encounter?.Trial)
        {
            case SocialFloorTrial.Woodsman:
                await PowerCmdCompat.Ensure<
                    FalseThroneShowYourWarmHeartPower>(Creature);
                await PowerCmdCompat.Ensure<
                    FalseThroneEmptyChestPower>(Creature);
                break;
            case SocialFloorTrial.Scarecrow:
                await PowerCmdCompat.Ensure<
                    FalseThroneShowYourWisdomPower>(Creature);
                await PowerCmdCompat.Ensure<
                    FalseThroneInsignificantWisdomPower>(Creature);
                break;
            case SocialFloorTrial.Lion:
                await PowerCmdCompat.Ensure<
                    FalseThroneShowYourCouragePower>(Creature);
                await PowerCmdCompat.Ensure<
                    FalseThroneTrulyCowardPower>(Creature);
                break;
            case SocialFloorTrial.Home:
                await PowerCmdCompat.Ensure<
                    FalseThroneWhatCanYouDoPower>(Creature);
                break;
            case SocialFloorTrial.Rage:
                await PowerCmdCompat.Ensure<FalseThroneRagePower>(Creature);
                break;
        }
    }

    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        CapWoodsmanTrialMaxEnergy(Encounter, amount);

    // 樵夫试炼：能量上限降为 1，每摧毁一个翡翠水晶解除 1 点。覆写与 FalseThroneWoodsmanMaxEnergyPatch 共用。
    internal static decimal CapWoodsmanTrialMaxEnergy(EncounterModel? encounter, decimal amount) =>
        encounter is SocialFloorLiberationEncounter { Trial: SocialFloorTrial.Woodsman } socialFloor
            ? Math.Min(amount, 1m + socialFloor.DestroyedCrystalCount)
            : amount;

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return target == Creature
            && Encounter?.Trial != SocialFloorTrial.Rage
                ? 0m
                : amount;
    }

    public override decimal ModifyChaoDamageCap(
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        return target == Creature && !CanReceiveChaoDamage
                ? 0m
                : decimal.MaxValue;
    }

    internal bool CanReceiveChaoDamage =>
        Encounter?.Trial is SocialFloorTrial.Home or SocialFloorTrial.Rage;

    internal async Task OpenHomeChaoGate()
    {
        if (Encounter?.Trial != SocialFloorTrial.Home
            || Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        // A phase boundary may inherit the pre-home stun snapshot. Explicitly
        // reopen the resistance state and ensure Home begins with a damageable
        // Chao pool instead of a stale zero/stunned value.
        libraryCreature.RestorePreStunResistance();
        if (libraryCreature.CurrentChaoValue <= 0
            && libraryCreature.MaxChaoValue > 0)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(
                libraryCreature,
                libraryCreature.MaxChaoValue);
        }
    }

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Creature
            || amount <= 0m
            || canonicalPower is not DoomPower
            || Encounter?.Trial == SocialFloorTrial.Rage)
        {
            return false;
        }

        modifiedAmount = 0m;
        return true;
    }

    public override bool ShouldDie(Creature creature) =>
        creature != Creature
        || Encounter?.Trial == SocialFloorTrial.Rage;

    public override Task AfterPreventingDeath(Creature creature) =>
        creature == Creature
        && Encounter?.Trial != SocialFloorTrial.Rage
            ? CreatureCmd.SetCurrentHp(Creature, Creature.MaxHp)
            : Task.CompletedTask;

    public override Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta)
    {
        return HandlePositiveHpDamage(creature, delta);
    }

    public override Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta,
        LibraryDamageType type)
    {
        return HandlePositiveHpDamage(creature, delta);
    }

    public override async Task AfterCurrentChaoValueChanged(
        Creature target,
        decimal amount,
        LibraryDamageType type)
    {
        if (target != Creature || amount >= 0m)
        {
            return;
        }

        if (Encounter is { Trial: SocialFloorTrial.Home } encounter
            && Creature is LibraryCreature libraryCreature
            && libraryCreature.CurrentChaoValue <= 0m)
        {
            encounter.QueueRageTrial();
        }

        if (Encounter is { Transformed: true } transformed
            && transformed.TryMarkFinalStrikeTriggered())
        {
            await ExecuteFinalStrike();
        }
    }

    public async Task TransformFromMagicalPowder(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Encounter is not { Trial: SocialFloorTrial.Rage } encounter
            || encounter.Transformed
            || cardPlay.Target != Creature)
        {
            return;
        }

        encounter.MarkTransformed();
        await CreatureCmd.TriggerAnim(Creature, "Transform", 0.8f);
    }

    private async Task InitialSequence(IReadOnlyList<Creature> targets)
    {
        await FriendlyGreeting(targets);
        if (!Creature.IsAlive) return;
        await OverflowingLight(targets);
        if (!Creature.IsAlive) return;
        await Insolence(targets);
    }

    private async Task HomeSequence(
        IReadOnlyList<Creature> targets,
        FalseThroneMove first,
        FalseThroneMove second)
    {
        await ExecuteNormalMove(first, targets);
        if (!Creature.IsAlive) return;
        await ExecuteNormalMove(second, targets);
    }

    private async Task OverflowingLight(IReadOnlyList<Creature> targets)
    {
        AttackCommand? attack = await ExecuteGroupAttack(
            OverflowingLightDamage,
            OverflowingLightHits,
            "OverflowingLight",
            OverflowingLightHitVfx);
        if (attack == null)
        {
            return;
        }

        foreach (DamageResult result in AttackCommandCompat.Results(attack))
        {
            if (result.Receiver.IsAlive)
            {
                await PowerCmdCompat.Apply<LibraryBleedingPower>(
                    result.Receiver,
                    OverflowingLightBleedAmount,
                    Creature,
                    null);
            }
        }
    }

    private async Task Insolence(IReadOnlyList<Creature> targets)
    {
        AttackCommand? attack = await ExecuteGroupAttack(
            InsolenceDamage,
            1,
            "Insolence",
            "vfx/vfx_attack_blunt");
        if (attack == null)
        {
            return;
        }

        foreach (Creature player in AttackCommandCompat.Results(attack)
                     .Select(static result => result.Receiver)
                     .Where(static player => player.IsAlive)
                     .Distinct())
        {
            await LibraryPowerCmd.Apply<LibraryBindingPower>(
                player,
                InsolenceBindAmount,
                InsolenceBindDuration,
                Creature,
                null);
        }
    }

    private async Task AllSilent(IReadOnlyList<Creature> targets)
    {
        await ExecuteGroupAttack(
            AllSilentDamage,
            1,
            "AllSilent",
            "vfx/vfx_attack_blunt");

        foreach (Creature player in ResolveLivingPlayers(Creature))
        {
            await PowerCmdCompat.Apply<StrengthPower>(
                player,
                AllSilentStrengthDelta,
                Creature,
                null);
            await PowerCmdCompat.Apply<DexterityPower>(
                player,
                AllSilentDexterityDelta,
                Creature,
                null);
        }
    }

    private async Task Manners(IReadOnlyList<Creature> targets)
    {
        await ExecuteGroupAttack(
            MannersDamage,
            MannersHits,
            "Manners",
            "vfx/vfx_attack_blunt");

        if (Creature.CombatState is not { } combatState)
        {
            return;
        }

        foreach (Creature player in ResolveLivingPlayers(Creature))
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Wound>(
                player,
                PileType.Discard,
                MannersWounds,
                addedByPlayer: false);
        }
    }

    private async Task FriendlyGreeting(IReadOnlyList<Creature> targets)
    {
        await ExecuteGroupAttack(
            FriendlyGreetingDamage,
            1,
            "FriendlyGreeting",
            "vfx/vfx_attack_blunt");
    }

    private async Task BigMistake(IReadOnlyList<Creature> targets)
    {
        AttackCommand? attack = await ExecuteGroupAttack(
            BigMistakeDamage,
            1,
            "BigMistake",
            "vfx/vfx_attack_blunt");
        if (attack == null)
        {
            return;
        }

        foreach (Creature player in AttackCommandCompat.Results(attack)
                     .Select(static result => result.Receiver)
                     .Where(static player => player.IsAlive)
                     .Distinct())
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(
                player,
                BigMistakeBleedAmount,
                Creature,
                null);
        }
    }

    private async Task FunIsOver(IReadOnlyList<Creature> targets)
    {
        AttackCommand? attack = await ExecuteGroupAttack(
            FunIsOverDamage,
            1,
            "FunIsOver",
            "vfx/vfx_attack_blunt");
        if (attack == null)
        {
            return;
        }

        foreach (Creature player in AttackCommandCompat.Results(attack)
                     .Select(static result => result.Receiver)
                     .Where(static player => player.IsAlive)
                     .Distinct())
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                player,
                FunIsOverWeakAmount,
                FunIsOverDebuffDuration,
                Creature,
                null);
        }
    }

    private Task<AttackCommand?> ExecuteGroupAttack(
        int damage,
        int hits,
        string animation,
        string hitFx)
    {
        return IndiscriminateAttackExecutor.Execute(
            this,
            damage,
            ResolveLivingPlayers(Creature),
            attack => attack
                .WithHitCount(hits)
                .WithAttackerAnim(animation, 0.64f)
                .WithHitFx(hitFx)
                .SpawningHitVfxOnEachCreature());
    }

    private async Task HandlePositiveHpDamage(
        Creature creature,
        decimal delta)
    {
        if (creature != Creature || delta >= 0m)
        {
            return;
        }

        if (Encounter is { Transformed: true } encounter
            && encounter.TryMarkFinalStrikeTriggered())
        {
            await ExecuteFinalStrike();
        }
    }

    private async Task ExecuteFinalStrike()
    {
        if (Creature.CurrentHp > 0)
        {
            await CreatureCmd.Damage(
                new ThrowingPlayerChoiceContext(),
                Creature,
                Creature.MaxHp,
                ValueProp.Unblockable | ValueProp.Unpowered,
                null,
                null);
        }

        if (Creature.IsAlive)
        {
            await CreatureCmd.Kill(Creature, force: true);
        }
    }

    private static IReadOnlyList<Creature> ResolveLivingPlayers(
        Creature owner) =>
        owner.CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .OrderBy(static player => player.CombatId)
            .ToArray()
        ?? [];

    private int OverflowingLightDamage => AscensionValue(
        OverflowingLightLowDamage,
        OverflowingLightHighDamage);

    private int InsolenceDamage => AscensionValue(
        InsolenceLowDamage,
        InsolenceHighDamage);

    private int AllSilentDamage => AscensionValue(
        AllSilentLowDamage,
        AllSilentHighDamage);

    private int MannersDamage => AscensionValue(
        MannersLowDamage,
        MannersHighDamage);

    private int MannersWounds => AscensionValue(
        MannersNormalWounds,
        MannersToughWounds);

    private int FriendlyGreetingDamage => AscensionValue(
        FriendlyGreetingLowDamage,
        FriendlyGreetingHighDamage);

    private int BigMistakeDamage => AscensionValue(
        BigMistakeLowDamage,
        BigMistakeHighDamage);

    private int FunIsOverDamage => AscensionValue(
        FunIsOverLowDamage,
        FunIsOverHighDamage);

    internal int OverflowingLightBleedAmount => OverflowingLightBleedPerHit;

    internal int InsolenceBindAmount => InsolenceBind;

    internal int InsolenceBindDuration => InsolenceBindTurns;

    internal int AllSilentStrengthDelta => -AllSilentStrengthLoss;

    internal int AllSilentDexterityDelta => -AllSilentDexterityLoss;

    internal int BigMistakeBleedAmount => BigMistakeBleed;

    internal int FunIsOverWeakAmount => FunIsOverWeak;

    internal int FunIsOverDisarmAmount => FunIsOverDisarm;

    internal int FunIsOverDebuffDuration => FunIsOverDebuffTurns;

    private static int AscensionValue(int low, int high) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            high,
            low);

    private void Register(FalseThroneMove move, MoveState state) =>
        _moves[move] = state;

    private CombinedAttackDebuffIntent
        CreateOverflowingLightIntent() => new(
            () => OverflowingLightDamage,
            () => OverflowingLightHits,
            "FALSE_THRONE_OVERFLOWING_LIGHT.description",
            IntentBadge.FromPower<LibraryBleedingPower>(
                () => OverflowingLightBleedAmount));

    private CombinedAttackDebuffIntent CreateInsolenceIntent() => new(
        () => InsolenceDamage,
        () => 1,
        "FALSE_THRONE_INSOLENCE.description",
        IntentBadge.FromPower<LibraryBindingPower>(
            () => InsolenceBindAmount));

    private CombinedAttackDebuffIntent CreateAllSilentIntent() => new(
        () => AllSilentDamage,
        () => 1,
        "FALSE_THRONE_ALL_SILENT.description",
        IntentBadge.FromPower<StrengthPower>(() => AllSilentStrengthDelta),
        IntentBadge.FromPower<DexterityPower>(() => AllSilentDexterityDelta));

    private AbstractIntent[] CreateMannersIntents() =>
    [
        new MultiAttackIntent(MannersDamage, MannersHits),
        new TargetedDetailedStatusCardIntent<Wound>(
            MannersWounds,
            PileType.Discard,
            "FALSE_THRONE_MANNERS.description",
            ResolveLivingPlayers)
    ];

    private AbstractIntent[] CreateNormalIntents(FalseThroneMove move) =>
        move switch
        {
            FalseThroneMove.OverflowingLight =>
                [CreateOverflowingLightIntent()],
            FalseThroneMove.Insolence => [CreateInsolenceIntent()],
            FalseThroneMove.AllSilent => [CreateAllSilentIntent()],
            FalseThroneMove.Manners => CreateMannersIntents(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(move),
                move,
                "Not a False Throne normal intent.")
        };

    private Task ExecuteNormalMove(
        FalseThroneMove move,
        IReadOnlyList<Creature> targets) => move switch
    {
        FalseThroneMove.OverflowingLight => OverflowingLight(targets),
        FalseThroneMove.Insolence => Insolence(targets),
        FalseThroneMove.AllSilent => AllSilent(targets),
        FalseThroneMove.Manners => Manners(targets),
        _ => throw new ArgumentOutOfRangeException(
            nameof(move),
            move,
            "Not a False Throne normal move.")
    };

    private string ResolveMoveId(FalseThroneMove move) =>
        _moves.TryGetValue(move, out MoveState? state)
            ? state.Id
            : _moves[FalseThroneMove.InitialSequence].Id;

    private Task RefreshIntents() =>
        NCombatRoom.Instance?.GetCreatureNode(Creature)?.RefreshIntents()
        ?? Task.CompletedTask;

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        return stateMachine.States.Values
            .OfType<MoveState>()
            .SelectMany(static move => move.Intents)
            .ToArray();
    }

    private static LibraryCreatureResistanceData.Resistance
        NormalResistance() => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };
}

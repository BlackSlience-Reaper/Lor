using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.Nosferatu;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Language;

internal enum LanguageFloorDipsiaMove
{
    GracefulRest,
    ElegantDinner,
    Thirst,
    CrimsonMeal,
    BloodFeast,
    OminousAura,
    ColdClaws,
    ViolentGesture,
    UnbearableThirst,
    ExtremeBloodthirst
}

public sealed class LanguageFloorDipsia :
    LiberationPhaseBossMonster
{
    public const int MaxHp = 350;
    public const int MaxChaoResistance = 120;
    public const int TransformHpPercent = 50;
    public const int HydrophobiaBloodThreshold = 4;
    public const int HydrophobiaStrong = 3;

    public const int GracefulRestBlock = 25;
    public const int GracefulRestStrength = 2;
    public const int ElegantDinnerBlood = 1;
    public const int ThirstStrengthLoss = 1;
    public const int ThirstDexterityLoss = 1;
    public const int ThirstWounds = 3;
    public const int CrimsonMealBlock = 12;
    public const int CrimsonMealProtection = 2;
    public const int CrimsonMealEndurance = 2;
    public const int OminousAuraBlock = 19;
    public const int OminousAuraWeak = 1;
    public const int ColdClawsHits = 2;
    public const int ColdClawsHealPercent = 20;
    public const int ViolentGestureHits = 5;
    public const int UnbearableThirstConfusion = 4;

    public const string GracefulRestMoveId = "GRACEFUL_REST";
    public const string ElegantDinnerMoveId = "ELEGANT_DINNER";
    public const string ThirstMoveId = "THIRST";
    public const string CrimsonMealMoveId = "CRIMSON_MEAL";
    public const string BloodFeastMoveId = "BLOOD_FEAST";
    public const string OminousAuraMoveId = "OMINOUS_AURA";
    public const string ColdClawsMoveId = "COLD_CLAWS";
    public const string ViolentGestureMoveId = "VIOLENT_GESTURE";
    public const string UnbearableThirstMoveId = "UNBEARABLE_THIRST";
    public const string ExtremeBloodthirstMoveId = "EXTREME_BLOODTHIRST";
    private const string RouterMoveId = "LANGUAGE_FLOOR_DIPSIA_ROUTER";

    public const string TextureRoot =
        LanguageFloorAssets.LiberationDipsiaMonsterRoot;
    public const string IdleTexturePath = TextureRoot + "dipsia_idle.png";
    public const string GroupBreakTexturePath =
        TextureRoot + "dipsia_group_break.png";
    public const string GroupAttackTexturePath =
        TextureRoot + "dipsia_group_attack.png";
    public const string FireTexturePath = TextureRoot + "dipsia_fire.png";
    public const string StrikeTexturePath = TextureRoot + "dipsia_strike.png";
    public const string SlashTexturePath = TextureRoot + "dipsia_slash.png";
    public const string HitTexturePath = TextureRoot + "dipsia_hit.png";
    public const string EvadeTexturePath = TextureRoot + "dipsia_evade.png";

    private const string NormalAttackSfxPath =
        LanguageFloorAssets.NosferatuSfxRoot + "nosferatu_transform_strike.ogg";
    private const string SlashAttackSfxPath =
        LanguageFloorAssets.NosferatuSfxRoot + "nosferatu_transform_slash.ogg";
    private const string NormalGroupAttackSfxPath =
        LanguageFloorAssets.NosferatuSfxRoot + "nosferatu_strong_attack.ogg";
    private const string TransformedGroupAttackSfxPath =
        LanguageFloorAssets.NosferatuSfxRoot + "nosferatu_transform_strong_attack_effect.ogg";
    private const string CastSfxPath = LanguageFloorAssets.NosferatuSfxRoot + "nosferatu_evade.ogg";
    private const string TransformSfxPath = LanguageFloorAssets.NosferatuSfxRoot + "nosferatu_transform.ogg";

    private const float NormalAttackSegmentSeconds = 0.45f;
    public const float GroupBreakSegmentSeconds = 0.55f;
    private const float GroupAttackSegmentSeconds = 0.55f;
    public const float GroupBlockBreakPauseSeconds = 1f;

    private static readonly LanguageFloorDipsiaMove[] NormalMoves =
    [
        LanguageFloorDipsiaMove.GracefulRest,
        LanguageFloorDipsiaMove.ElegantDinner,
        LanguageFloorDipsiaMove.Thirst,
        LanguageFloorDipsiaMove.CrimsonMeal,
        LanguageFloorDipsiaMove.BloodFeast
    ];

    private static readonly LanguageFloorDipsiaMove[] TransformedMoves =
    [
        LanguageFloorDipsiaMove.OminousAura,
        LanguageFloorDipsiaMove.ColdClaws,
        LanguageFloorDipsiaMove.ViolentGesture,
        LanguageFloorDipsiaMove.UnbearableThirst,
        LanguageFloorDipsiaMove.ExtremeBloodthirst
    ];

    public static readonly string[] AssetPathsStatic =
        LanguageFloorDipsiaCreatureVisuals.Profile.AssetPaths
            .Concat(
            [
                NormalAttackSfxPath,
                SlashAttackSfxPath,
                NormalGroupAttackSfxPath,
                TransformedGroupAttackSfxPath,
                CastSfxPath,
                TransformSfxPath,
                LanguageFloorAssets.NosferatuBloodPowerIcon,
                LanguageFloorAssets.NosferatuHydrophobiaPassivePowerIcon,
                LanguageFloorAssets.NosferatuTransformPowerIcon,
                LanguageFloorAssets.NosferatuFlowingBloodPowerIcon
            ])
            .ToArray();

    public bool IsTransformed { get; private set; }

    public bool TransformPending { get; private set; }

    // 半血触发标记：一旦锁血触发就置 true，本场战斗内不再复位，
    // BeforeSideTurnStart 依赖它执行变身，避免 pending 状态丢失后永远锁血。
    public bool TransformTriggered { get; private set; }

    public int PreviousMove { get; private set; } = -1;

    public int LastHydrophobiaRound { get; private set; } = -1;

    internal int GroupBreakTriggerCount { get; private set; }

    internal int GroupAttackTriggerCount { get; private set; }

    internal bool LastGroupAttackBrokeBlock { get; private set; }

    internal IReadOnlyList<uint?> LastAttackTargetCombatIds { get; private set; } =
        [];

    internal IReadOnlyList<string> LastAttackAnimationTriggers { get; private set; } = [];


    public override int LiberationPhase => 4;

    /// <summary>死亡动画时长；转阶段的假死返回 0，见 <see cref="LayeredBossSpine.DeathLength"/>。</summary>
    public override float DeathAnimLengthOverride => LayeredBossSpine.DeathLength(this);

    protected override bool ReviveTriggerPlaysHitAnimation => true;

    public override bool ShouldDisappearFromDoom => false;

    public decimal TransformHpThreshold =>
        Math.Max(1m, Math.Ceiling(Creature.MaxHp * TransformHpPercent / 100m));

    public override int MinInitialHp => MaxHp;

    public override int MaxInitialHp => MaxHp;

    public override int DefaultChaoResistance => MaxChaoResistance;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Endure,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Endure,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure
        };

    public override IEnumerable<string> AssetPaths =>
        AssetPathsStatic
            .Concat(LanguageFloorBloodBat.SharedAssetPaths)
            .Concat(EnumerateIntentAssets().SelectMany(static intent =>
                intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        if (Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }

        await PowerCmdCompat.Ensure<LanguageFloorDipsiaHydrophobiaPassivePower>(
            Creature);
        if (IsTransformed)
        {
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorDipsiaTransformPower>(Creature);
            await PowerCmdCompat.Ensure<NosferatuFlowingBloodPower>(Creature);
        }
        else
        {
            await PowerCmdCompat.Ensure<LanguageFloorDipsiaTransformPower>(
                Creature);
            await PowerCmdCompat.RemoveIfPresent<NosferatuFlowingBloodPower>(
                Creature);
        }

        LanguageFloorLiberationBackgroundController.SetPhaseBackground(4);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (TurnParticipants.IsRoundPlayerTurn(side) && Creature.IsAlive)
        {
            if (!IsTransformed
                && !TransformPending
                && Creature.CurrentHp <= TransformHpThreshold)
            {
                await QueueTransformAndClampHp();
            }

            if (TransformPending || TransformTriggered)
            {
                await Transform();
            }

            if (IsTransformed)
            {
                await NosferatuBloodPower.Change(
                    Creature,
                    -1,
                    Creature);
            }

            int round = Creature.CombatState?.RoundNumber ?? int.MinValue;
            if (LastHydrophobiaRound != round
                && NosferatuBloodPower.GetStacks(Creature)
                    <= HydrophobiaBloodThreshold)
            {
                LastHydrophobiaRound = round;
                await LibraryPowerCmd.Apply<LibraryStrongPower>(
                    new ThrowingPlayerChoiceContext(),
                    Creature,
                    HydrophobiaStrong,
                    0,
                    IsPermanent: false,
                    Creature,
                    null);
            }
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }

    internal async Task QueueTransformAndClampHp()
    {
        if (IsTransformed)
        {
            return;
        }

        TransformPending = true;
        TransformTriggered = true;
        if (Creature.CurrentHp != TransformHpThreshold)
        {
            await CreatureCmd.SetCurrentHp(
                Creature,
                TransformHpThreshold);
        }
    }

    internal string DebugChooseMove(Rng rng) =>
        ResolvePlannedMoveId(rng);

    internal bool DebugCanUseGracefulRest() =>
        !HasLivingPhaseBloodBat();

    internal IReadOnlyList<string> DebugEligibleMoveIds() =>
        GetEligibleMoves()
            .Select(MoveId)
            .ToArray();

    internal AbstractIntent DebugGetPrimaryIntent(
        LanguageFloorDipsiaMove move)
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        return ((MoveState)stateMachine.States[MoveId(move)])
            .Intents
            .Single();
    }

    internal Task DebugPerformMove(LanguageFloorDipsiaMove move) =>
        move switch
        {
            LanguageFloorDipsiaMove.GracefulRest =>
                GracefulRestMove([]),
            LanguageFloorDipsiaMove.ElegantDinner =>
                ElegantDinnerMove([]),
            LanguageFloorDipsiaMove.Thirst => ThirstMove([]),
            LanguageFloorDipsiaMove.CrimsonMeal => CrimsonMealMove([]),
            LanguageFloorDipsiaMove.BloodFeast => BloodFeastMove([]),
            LanguageFloorDipsiaMove.OminousAura => OminousAuraMove([]),
            LanguageFloorDipsiaMove.ColdClaws => ColdClawsMove([]),
            LanguageFloorDipsiaMove.ViolentGesture =>
                ViolentGestureMove([]),
            LanguageFloorDipsiaMove.UnbearableThirst =>
                UnbearableThirstMove([]),
            _ => ExtremeBloodthirstMove([])
        };

    internal static int DebugGetMoveDamage(
        LanguageFloorDipsiaMove move,
        bool deadlyEnemies)
    {
        return move switch
        {
            LanguageFloorDipsiaMove.ElegantDinner =>
                deadlyEnemies ? 20 : 18,
            LanguageFloorDipsiaMove.Thirst =>
                deadlyEnemies ? 13 : 11,
            LanguageFloorDipsiaMove.BloodFeast =>
                deadlyEnemies ? 25 : 24,
            LanguageFloorDipsiaMove.ColdClaws =>
                deadlyEnemies ? 8 : 7,
            LanguageFloorDipsiaMove.ViolentGesture =>
                deadlyEnemies ? 3 : 2,
            LanguageFloorDipsiaMove.ExtremeBloodthirst =>
                deadlyEnemies ? 34 : 33,
            _ => 0
        };
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var gracefulRest = new MoveState(
            GracefulRestMoveId,
            GracefulRestMove,
            new CombinedDefendBuffIntent(
                GracefulRestBlock,
                "LANGUAGE_FLOOR_DIPSIA_GRACEFUL_REST.description",
                IntentBadge.Strength(GracefulRestStrength)),
            new SummonIntent());
        var elegantDinner = new MoveState(
            ElegantDinnerMoveId,
            ElegantDinnerMove,
            new CombinedAttackBuffIntent(
                () => ElegantDinnerDamage,
                () => 1,
                "LANGUAGE_FLOOR_DIPSIA_ELEGANT_DINNER.description",
                IntentBadge.FromPower<NosferatuBloodPower>(
                    ElegantDinnerBlood)));
        var thirst = new MoveState(
            ThirstMoveId,
            ThirstMove,
            new CombinedAttackDebuffIntent(
                () => ThirstDamage,
                () => 1,
                "LANGUAGE_FLOOR_DIPSIA_THIRST.description",
                IntentBadge.FromPower<StrengthPower>(-ThirstStrengthLoss),
                IntentBadge.FromPower<DexterityPower>(-ThirstDexterityLoss),
                IntentBadge.StatusCard<Wound>(ThirstWounds)));
        var crimsonMeal = new MoveState(
            CrimsonMealMoveId,
            CrimsonMealMove,
            new CombinedDefendBuffIntent(
                CrimsonMealBlock,
                "LANGUAGE_FLOOR_DIPSIA_CRIMSON_MEAL.description",
                IntentBadge.FromPower<LibraryProtectionPower>(
                    CrimsonMealProtection),
                IntentBadge.Guard(CrimsonMealEndurance)));
        var bloodFeast = new MoveState(
            BloodFeastMoveId,
            BloodFeastMove,
            new IndiscriminateAttackIntent(
                () => BloodFeastDamage,
                () => 1,
                "LANGUAGE_FLOOR_DIPSIA_BLOOD_FEAST.description",
                ResolveLivingPlayers));

        var ominousAura = new MoveState(
            OminousAuraMoveId,
            OminousAuraMove,
            new CombinedDefendDebuffIntent(
                OminousAuraBlock,
                "LANGUAGE_FLOOR_DIPSIA_OMINOUS_AURA.description",
                IntentBadge.FromPower<LibraryWeakPower>(
                    OminousAuraWeak)));
        var coldClaws = new MoveState(
            ColdClawsMoveId,
            ColdClawsMove,
            new CombinedAttackBuffIntent(
                () => ColdClawsDamage,
                () => ColdClawsHits,
                "LANGUAGE_FLOOR_DIPSIA_COLD_CLAWS.description",
                IntentBadge.Heal(ColdClawsHealPercent)));
        var violentGesture = new MoveState(
            ViolentGestureMoveId,
            ViolentGestureMove,
            new MultiAttackIntent(
                ViolentGestureDamage,
                ViolentGestureHits));
        var unbearableThirst = new MoveState(
            UnbearableThirstMoveId,
            UnbearableThirstMove,
            new BadgedDebuffIntent(
                IntentBadge.FromPower<LibraryOfRuinaConfusionPower>(
                    UnbearableThirstConfusion),
                UnbearableThirstConfusion,
                "LANGUAGE_FLOOR_DIPSIA_UNBEARABLE_THIRST.description"));
        var extremeBloodthirst = new MoveState(
            ExtremeBloodthirstMoveId,
            ExtremeBloodthirstMove,
            new IndiscriminateAttackIntent(
                () => ExtremeBloodthirstDamage,
                () => 1,
                "LANGUAGE_FLOOR_DIPSIA_EXTREME_BLOODTHIRST.description",
                ResolveLivingPlayers));
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, _) => ResolvePlannedMoveId(RunRng.MonsterAi));
        foreach (MoveState state in new[]
                 {
                     gracefulRest,
                     elegantDinner,
                     thirst,
                     crimsonMeal,
                     bloodFeast,
                     ominousAura,
                     coldClaws,
                     violentGesture,
                     unbearableThirst,
                     extremeBloodthirst,
                     reviveAndEmpower
                 })
        {
            state.FollowUpState = router;
        }

        return new MonsterMoveStateMachine(
            [
                gracefulRest,
                elegantDinner,
                thirst,
                crimsonMeal,
                bloodFeast,
                ominousAura,
                coldClaws,
                violentGesture,
                unbearableThirst,
                extremeBloodthirst,
                reviveAndEmpower,
                router
            ],
            router);
    }

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    private int ElegantDinnerDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            20,
            18);

    private int ThirstDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            13,
            11);

    private int BloodFeastDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            25,
            24);

    private int ColdClawsDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            8,
            7);

    private int ViolentGestureDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            2);

    private int ExtremeBloodthirstDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            34,
            33);

    private async Task GracefulRestMove(IReadOnlyList<Creature> targets)
    {
        PlaySfx(CastSfxPath);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Cast",
            NormalAttackSegmentSeconds);

        if (Creature.CombatState is not { } combatState)
        {
            return;
        }

        foreach (Creature staleBat in combatState.Enemies
                     .Where(static enemy =>
                         enemy.Monster is LanguageFloorBloodBat)
                     .ToArray())
        {
            await LiberationPhaseCleanup.RemoveTransitionCreature(
                staleBat,
                combatState);
        }

        Creature leftBat = await SpawnPhaseBloodBat(
            combatState,
            LanguageFloorLiberationEncounter.DipsiaLeftBatSlot);
        Creature rightBat = await SpawnPhaseBloodBat(
            combatState,
            LanguageFloorLiberationEncounter.DipsiaRightBatSlot);

        await CreatureCmd.GainBlock(
            Creature,
            GracefulRestBlock,
            ValueProp.Move,
            null);
        foreach (Creature enemy in LivingEnemies())
        {
            await PowerCmdCompat.Apply<StrengthPower>(
                enemy,
                GracefulRestStrength,
                Creature,
                null);
        }

        leftBat.PrepareForNextTurn(combatState.PlayerCreatures);
        rightBat.PrepareForNextTurn(combatState.PlayerCreatures);
        combatState.SortEnemiesBySlotName();
    }

    private async Task ElegantDinnerMove(IReadOnlyList<Creature> targets)
    {
        PlaySfx(NormalAttackSfxPath);
        DipsiaAttackOutcome attack = await ExecutePlayerAttackDetailed(
            ElegantDinnerDamage,
            hits: 1,
            "AttackFire");
        if (attack.AnyUnblocked)
        {
            await NosferatuBloodPower.Change(
                Creature,
                ElegantDinnerBlood,
                Creature);
        }
    }

    private async Task ThirstMove(IReadOnlyList<Creature> targets)
    {
        PlaySfx(NormalAttackSfxPath);
        IReadOnlyList<Creature> players = LivingPlayers();
        await ExecutePlayerAttackDetailed(
            ThirstDamage,
            hits: 1,
            "AttackFire",
            players);

        foreach (Creature player in players)
        {
            await PowerCmdCompat.Apply<StrengthPower>(
                player,
                -ThirstStrengthLoss,
                Creature,
                null);
            await PowerCmdCompat.Apply<DexterityPower>(
                player,
                -ThirstDexterityLoss,
                Creature,
                null);
            await CardPileCmdCompat.AddToCombatAndPreview<Wound>(
                player,
                PileType.Discard,
                ThirstWounds,
                addedByPlayer: false);
        }
    }

    private async Task CrimsonMealMove(IReadOnlyList<Creature> targets)
    {
        PlaySfx(CastSfxPath);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Cast",
            NormalAttackSegmentSeconds);
        foreach (Creature enemy in LivingEnemies())
        {
            await CreatureCmd.GainBlock(
                enemy,
                CrimsonMealBlock,
                ValueProp.Move,
                null);
            await LibraryPowerCmd.Apply<LibraryProtectionPower>(
                    enemy,
                    CrimsonMealProtection,
                    turns: -1,
                    Creature,
                    null);
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                    enemy,
                    CrimsonMealEndurance,
                    turns: -1,
                    Creature,
                    null);
        }
    }

    private async Task BloodFeastMove(IReadOnlyList<Creature> targets)
    {
        PlaySfx(NormalGroupAttackSfxPath);
        await ExecuteGroupAttackDetailed(BloodFeastDamage);
        int blood = NosferatuBloodPower.GetStacks(Creature);
        if (blood > 0)
        {
            await NosferatuBloodPower.Change(
                Creature,
                -blood,
                Creature);
        }
    }

    private async Task OminousAuraMove(IReadOnlyList<Creature> targets)
    {
        PlaySfx(CastSfxPath);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Cast",
            NormalAttackSegmentSeconds);
        await CreatureCmd.GainBlock(
            Creature,
            OminousAuraBlock,
            ValueProp.Move,
            null);
        foreach (Creature player in LivingPlayers())
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                player,
                OminousAuraWeak,
                turns: -1,
                Creature,
                null);
        }
    }

    private async Task ColdClawsMove(IReadOnlyList<Creature> targets)
    {
        PlaySfx(NormalAttackSfxPath);
        await ExecutePlayerAttackDetailed(
            ColdClawsDamage,
            ColdClawsHits,
            "AttackStrike",
            alternatingAnimation: "AttackSlash");
        int heal = Math.Max(
            1,
            (int)Math.Ceiling(
                Creature.MaxHp * ColdClawsHealPercent / 100m));
        await CreatureCmd.Heal(
            Creature,
            MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(
                Creature,
                heal));
    }

    private async Task ViolentGestureMove(IReadOnlyList<Creature> targets)
    {
        PlaySfx(SlashAttackSfxPath);
        await ExecutePlayerAttackDetailed(
            ViolentGestureDamage,
            ViolentGestureHits,
            "AttackSlash",
            alternatingAnimation: "AttackStrike");
    }

    private async Task UnbearableThirstMove(
        IReadOnlyList<Creature> targets)
    {
        PlaySfx(CastSfxPath);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Cast",
            NormalAttackSegmentSeconds);
        await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(
            LivingPlayers(),
            UnbearableThirstConfusion,
            Creature,
            null);
    }

    private async Task ExtremeBloodthirstMove(
        IReadOnlyList<Creature> targets)
    {
        PlaySfx(TransformedGroupAttackSfxPath);
        await ExecuteGroupAttackDetailed(ExtremeBloodthirstDamage);
    }

    private async Task Transform()
    {
        if (IsTransformed
            || (!TransformPending && !TransformTriggered)
            || Creature.IsDead)
        {
            return;
        }

        TransformPending = false;
        IsTransformed = true;
        PreviousMove = -1;
        PlaySfx(TransformSfxPath);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Cast",
            GroupBreakSegmentSeconds);

        if (Creature.CombatState is { } combatState)
        {
            foreach (Creature bat in combatState.Enemies
                         .Where(static enemy =>
                             enemy.Monster is LanguageFloorBloodBat)
                         .ToArray())
            {
                if (bat.IsAlive)
                {
                    await CreatureCmd.Kill(bat, force: true);
                }

                if (combatState.Enemies.Contains(bat))
                {
                    await LiberationPhaseCleanup.RemoveTransitionCreature(
                        bat,
                        combatState);
                }
            }
        }

        await NosferatuBloodPower.Change(Creature, 5, Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorDipsiaTransformPower>(Creature);
        await PowerCmdCompat.Ensure<NosferatuFlowingBloodPower>(Creature);

        ResetStateMachine();
        SetUpForCombat();
        if (Creature.CombatState is { } currentCombatState)
        {
            Creature.PrepareForNextTurn(currentCombatState.PlayerCreatures);
        }
    }

    private string ResolvePlannedMoveId(Rng rng)
    {
        LanguageFloorDipsiaMove[] source = GetEligibleMoves();
        LanguageFloorDipsiaMove[] eligible = source
            .Where(move => (int)move != PreviousMove)
            .ToArray();
        if (eligible.Length == 0)
        {
            eligible = source;
        }

        LanguageFloorDipsiaMove selected = rng.NextItem(eligible);
        PreviousMove = (int)selected;
        return MoveId(selected);
    }

    private LanguageFloorDipsiaMove[] GetEligibleMoves()
    {
        LanguageFloorDipsiaMove[] source =
            IsTransformed ? TransformedMoves : NormalMoves;
        return source
            .Where(move =>
                move != LanguageFloorDipsiaMove.GracefulRest
                || !HasLivingPhaseBloodBat())
            .ToArray();
    }

    private async Task<DipsiaAttackOutcome> ExecutePlayerAttackDetailed(
        int damage,
        int hits,
        string firstAnimation,
        IReadOnlyList<Creature>? fixedTargets = null,
        string? alternatingAnimation = null)
    {
        IReadOnlyList<Creature> players = fixedTargets ?? LivingPlayers();
        players = CombatTargets.DeterministicLiving(
            players.Where(static player => player.IsPlayer));
        LastAttackTargetCombatIds = players
            .Select(static player => player.CombatId)
            .ToArray();

        var results = new List<DamageResult>();
        var animationTriggers = new List<string>(hits);
        for (int hit = 0;
             hit < hits && Creature.IsAlive && players.Any(static p => p.IsAlive);
             hit++)
        {
            IReadOnlyList<Creature> liveTargets = players
                .Where(static player => player.IsAlive)
                .ToArray();
            string animation = alternatingAnimation != null && hit % 2 == 1
                ? alternatingAnimation
                : firstAnimation;
            animationTriggers.Add(animation);
            await CreatureCmd.TriggerAnim(
                Creature,
                animation,
                NormalAttackSegmentSeconds);
            results.AddRange(await CreatureCmdCompat.Damage(
                new ThrowingPlayerChoiceContext(),
                liveTargets,
                damage,
                ValueProp.Move,
                Creature,
                null));
        }

        LastAttackAnimationTriggers = animationTriggers;
        return new DipsiaAttackOutcome(players, results);
    }

    private async Task<DipsiaAttackOutcome> ExecuteGroupAttackDetailed(
        int damage)
    {
        IReadOnlyList<Creature> players = LivingPlayers();
        LastAttackTargetCombatIds = players
            .Select(static player => player.CombatId)
            .ToArray();
        if (players.Count == 0)
        {
            return new DipsiaAttackOutcome(players, []);
        }

        GroupBreakTriggerCount++;
        await CreatureCmd.TriggerAnim(
            Creature,
            "GroupBreak",
            GroupBreakSegmentSeconds);
        LastGroupAttackBrokeBlock =
            await IndiscriminateAttackBlockBreaker.BreakBlockBeforeDamage(
                this,
                damage,
                players);
        if (LastGroupAttackBrokeBlock)
        {
            await Cmd.CustomScaledWait(
                GroupBlockBreakPauseSeconds,
                GroupBlockBreakPauseSeconds);
        }

        GroupAttackTriggerCount++;
        await CreatureCmd.TriggerAnim(
            Creature,
            "GroupAttack",
            GroupAttackSegmentSeconds);
        IReadOnlyList<DamageResult> results =
            (await CreatureCmdCompat.Damage(
                new ThrowingPlayerChoiceContext(),
                players.Where(static player => player.IsAlive).ToArray(),
                damage,
                ValueProp.Move,
                Creature,
                null))
            .ToArray();
        return new DipsiaAttackOutcome(players, results);
    }

    private async Task<Creature> SpawnPhaseBloodBat(
        CombatStateLike combatState,
        string slot)
    {
        return await CreatureCmd.Add(
            ModelDb.Monster<LanguageFloorBloodBat>().ToMutable(),
            combatState,
            CombatSide.Enemy,
            slot);
    }

    private bool HasLivingPhaseBloodBat() =>
        Creature.CombatState?.Enemies.Any(static enemy =>
            enemy.IsAlive
            && enemy.Monster is LanguageFloorBloodBat)
        ?? false;

    private IReadOnlyList<Creature> LivingPlayers() =>
        ResolveLivingPlayers(Creature);

    private static IReadOnlyList<Creature> ResolveLivingPlayers(
        Creature owner) =>
        CombatTargets.DeterministicLiving(
            owner.CombatState?.PlayerCreatures);

    private IReadOnlyList<Creature> LivingEnemies() =>
        CombatTargets.DeterministicLiving(
            Creature.CombatState?.Enemies);

    private static void PlaySfx(string path)
    {
        LocalOggOneShotPlayer.Play(path, -2f);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is not MoveState moveState)
            {
                continue;
            }

            foreach (AbstractIntent intent in moveState.Intents)
            {
                yield return intent;
            }
        }
    }

    private static string MoveId(LanguageFloorDipsiaMove move) => move switch
    {
        LanguageFloorDipsiaMove.GracefulRest => GracefulRestMoveId,
        LanguageFloorDipsiaMove.ElegantDinner => ElegantDinnerMoveId,
        LanguageFloorDipsiaMove.Thirst => ThirstMoveId,
        LanguageFloorDipsiaMove.CrimsonMeal => CrimsonMealMoveId,
        LanguageFloorDipsiaMove.BloodFeast => BloodFeastMoveId,
        LanguageFloorDipsiaMove.OminousAura => OminousAuraMoveId,
        LanguageFloorDipsiaMove.ColdClaws => ColdClawsMoveId,
        LanguageFloorDipsiaMove.ViolentGesture => ViolentGestureMoveId,
        LanguageFloorDipsiaMove.UnbearableThirst =>
            UnbearableThirstMoveId,
        _ => ExtremeBloodthirstMoveId
    };

    private sealed record DipsiaAttackOutcome(
        IReadOnlyList<Creature> Targets,
        IReadOnlyList<DamageResult> Results)
    {
        public bool AnyUnblocked =>
            Results.Any(static result => result.UnblockedDamage > 0);
    }
}

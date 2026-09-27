using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.cards.RedMist;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.QueenBee;
using LibraryOfRuina.powers.RedMist;
using LibraryOfRuina.ui;
using LibraryOfRuina.visuals.RedMist;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Kali;

public sealed class Kali : SpecialGuestMonsterBase, IEnemyCardRuntimeOwner, ITargetedMonsterAttackProvider, LibraryOfRuina.helpers.IFinalHpLossClamp
{
    public const int EgoHpThreshold = 300;
    public const int DefaultChaoMax = 270;
    public const int EgoReturnTurns = 2;

    private static readonly LibraryDamageType[] ResistanceRotationTypes =
        [LibraryDamageType.Slash, LibraryDamageType.Pierce, LibraryDamageType.Blunt];
    public const int EgoBaseBuffAmount = 2;
    public const int BloodMistMaxStacks = 5;
    public const int MinimumDirectDamagePerRound = 8;
    public const int ChaoLossPercent = 60;
    public const int InitialKaliIntentCapacity = 1;
    public const int MaxReceptionRoundIntentCapacityBonus = 3;
    public const int EmotionIntentCapacityBonusLevel = 4;
    public const int OpeningPlanFirstTurnIntentCount = 1;
    public const int OpeningPlanSecondTurnIntentCount = 2;

    public const int VerticalSplitLowAscensionDamage = 2;
    public const int VerticalSplitHighAscensionDamage = 4;

    public static int VerticalSplitDamage => GetAttackDamageByAscension(
        VerticalSplitLowAscensionDamage,
        VerticalSplitHighAscensionDamage);

    public const int VerticalSplitHits = 2;
    public const int ThrustLowAscensionDamage = 4;
    public const int ThrustHighAscensionDamage = 5;

    public static int ThrustDamage => GetAttackDamageByAscension(
        ThrustLowAscensionDamage,
        ThrustHighAscensionDamage);

    public const int ThrustHits = 3;
    public const int HorizontalSlashLowAscensionDamage = 2;
    public const int HorizontalSlashHighAscensionDamage = 3;

    public static int HorizontalSlashDamage => GetAttackDamageByAscension(
        HorizontalSlashLowAscensionDamage,
        HorizontalSlashHighAscensionDamage);

    public const int HorizontalSlashHits = 2;
    public const int HorizontalSlashBleed = 4;
    public const int BloodMistLowAscensionDamage = 9;
    public const int BloodMistHighAscensionDamage = 10;

    public static int BloodMistDamage => GetAttackDamageByAscension(
        BloodMistLowAscensionDamage,
        BloodMistHighAscensionDamage);

    public const int BattleWillLowAscensionDamage = 11;
    public const int BattleWillHighAscensionDamage = 12;

    public static int BattleWillDamage => GetAttackDamageByAscension(
        BattleWillLowAscensionDamage,
        BattleWillHighAscensionDamage);

    public const int FocusBreathBlock = 12;
    public const int FocusBreathStrong = 3;
    public const int FocusBreathStrongDurationTurns = 1;
    public const int FieldOfCorpsesLowAscensionDamage = 12;
    public const int FieldOfCorpsesHighAscensionDamage = 14;

    public static int FieldOfCorpsesDamage => GetAttackDamageByAscension(
        FieldOfCorpsesLowAscensionDamage,
        FieldOfCorpsesHighAscensionDamage);

    public const int FieldOfCorpsesBleed = 6;
    public const int RepeatUnblockedDamageThreshold = 8;

    private static int GetAttackDamageByAscension(int lowAscension, int highAscension) =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, highAscension, lowAscension);

    public const string Root = "res://images/monsters/red_mist/";
    public const string IdleTexturePath = Root + "kali_idle.png";
    public const string EgoIdleTexturePath = Root + "red_mist_ego_idle.png";
    public const string EgoBgmPath = "res://audio/bgm/red_mist/red_mist_ego.ogg";
    public const string RedMistSlashSfxPath = "res://audio/sfx/wrath_servant/attack_slash.ogg";
    public const string RedMistPierceSfxPath = "res://audio/sfx/wrath_servant/attack_thrust.ogg";
    public const string RedMistBluntSfxPath = "res://audio/sfx/wrath_servant/attack_strike.ogg";

    public const string SlashHitVfx = "vfx/vfx_attack_slash";
    public const string PierceHitVfx = "vfx/vfx_dramatic_stab";
    public const string BluntHitVfx = "vfx/vfx_attack_blunt";

    private static readonly string[] BasicAttackAnimations =
        ["AttackBlunt", "AttackPierce", "AttackSlash"];

    private static readonly string[] RandomPlanCardIds =
    [
        RedMistVerticalSplitCardId,
        RedMistThrustCardId,
        RedMistHorizontalSlashCardId,
        RedMistFocusBreathCardId,
        RedMistBattleWillCardId,
        RedMistBloodMistCardId,
        RedMistFieldOfCorpsesCardId
    ];

    private const string RedMistVerticalSplitCardId = "RED_MIST_VERTICAL_SPLIT_EGO_CARD";
    private const string RedMistThrustCardId = "RED_MIST_THRUST_EGO_CARD";
    private const string RedMistHorizontalSlashCardId = "RED_MIST_HORIZONTAL_SLASH_EGO_CARD";
    private const string RedMistFocusBreathCardId = "RED_MIST_FOCUS_BREATH_EGO_CARD";
    private const string RedMistBattleWillCardId = "RED_MIST_BATTLE_WILL_EGO_CARD";
    private const string RedMistBloodMistCardId = "RED_MIST_BLOOD_MIST_EGO_CARD";
    private const string RedMistFieldOfCorpsesCardId = "RED_MIST_FIELD_OF_CORPSES_EGO_CARD";

    public static IReadOnlySet<string> FirstGroupCardIds { get; } = new HashSet<string>
    {
        RedMistVerticalSplitCardId,
        RedMistThrustCardId,
        RedMistHorizontalSlashCardId
    };

    public static IReadOnlySet<string> SecondGroupCardIds { get; } = new HashSet<string>
    {
        RedMistBloodMistCardId,
        RedMistBattleWillCardId,
        RedMistFocusBreathCardId
    };

    public static string FieldOfCorpsesCardId => RedMistFieldOfCorpsesCardId;

    public static IReadOnlyList<string> ManifestationRequiredCardIds { get; } =
        [RedMistBloodMistCardId, RedMistFieldOfCorpsesCardId];

    private EnemyCardRuntime? _enemyCards;
    private Dictionary<string, EnemyCardSpec>? _enemyCardSpecs;

    [SavedProperty]
    public bool EgoTriggered { get; private set; }

    [SavedProperty]
    public bool EgoActive { get; private set; }

    [SavedProperty]
    public bool EgoManifestationPending { get; private set; }

    [SavedProperty]
    public int EgoReturnCountdown { get; private set; }

    [SavedProperty]
    public int PersistedBloodMistStacks { get; private set; }

    [SavedProperty]
    public int PersistedEnemyCardPlanNumber { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PersistedQueuedExtraCardIds { get; private set; } = string.Empty;

    [SavedProperty]
    public bool EgoThresholdTurnLockConsumed { get; private set; }

    [SavedProperty]
    public int EgoThresholdTurnLockRound { get; private set; } = -1;

    [SavedProperty]
    public int EgoThresholdTurnLockSide { get; private set; } = -1;

    private int _redMistStrongContribution;
    private int _redMistEnduranceContribution;
    private decimal _directUnblockedDamageThisEnemyTurn;
    private bool _evaluatingRoundDamage;
    private bool _isExecutingEnemyCardSequence;
    private bool _pendingEgoBuffRefresh;
    private bool _forceManifestationCardsInNextPlan;
    private DamageResult? _lastProcessedDamageResult;

    public EnemyCardRuntime? EnemyCards => _enemyCards;

    public bool IsEgoActive => EgoActive;

    public int BloodMistStacks => PersistedBloodMistStacks;

    public int EnemyCardPlanNumber => PersistedEnemyCardPlanNumber;

    public decimal ScaledEgoHpThreshold => ResolveScaledEgoHpThreshold(Creature);

    internal bool IsHealthBarLockActive => IsEgoThresholdTurnLockActive();

    public int ScaledMinimumDirectDamagePerRound =>
        ResolveMinimumDirectDamagePerRound(Creature.CombatState, this);

    public static decimal ResolveScaledEgoHpThreshold(Creature? creature) =>
        creature == null ? EgoHpThreshold : Math.Floor(creature.MaxHp * 0.5m);

    public static int ResolveMinimumDirectDamagePerRound(
        CombatStateLike? combatState,
        MonsterModel? monster) =>
        (int)Math.Ceiling(MultiplayerScalingPatchHelper.ScaleHpAmount(
            combatState,
            monster,
            MinimumDirectDamagePerRound));

    protected override int InitialIntentCapacity => InitialKaliIntentCapacity;

    internal static int ResolveReceptionRoundIntentCapacity(
        int roundNumber,
        int emotionLevel)
    {
        int roundBonus = Math.Clamp(
            roundNumber - 1,
            0,
            MaxReceptionRoundIntentCapacityBonus);
        int emotionBonus = emotionLevel >= EmotionIntentCapacityBonusLevel ? 1 : 0;
        return Math.Min(
            StoredIntentSlots,
            InitialKaliIntentCapacity + roundBonus + emotionBonus);
    }

    internal static bool ShouldTickEgoReturnCountdown(CombatSide side) =>
        side == CombatSide.Player;

    internal static int ResolveEgoReturnCountdownAfterPlayerTurnStart(
        int currentCountdown) =>
        Math.Max(0, currentCountdown - 1);

    public override int DefaultChaoResistance => DefaultChaoMax;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 692, 585);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 700, 589);

    public override IEnumerable<string> AssetPaths =>
        StaticAssetPaths
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public static IEnumerable<string> StaticAssetPaths =>
        new[] { KaliCreatureVisuals.ScenePath }
        .Concat(
        [
        EgoBgmPath,
        "res://images/powers/library_strong_power.png",
        "res://images/powers/library_endurance_power.png",
        "res://images/powers/kali_power.png",
        "res://images/powers/red_mist_strongest_one_power.png",
        "res://images/powers/red_mist_ego_power.png",
        "res://images/powers/red_mist_blood_mist_power.png",
        RedMistSlashSfxPath,
        RedMistPierceSfxPath,
        RedMistBluntSfxPath
        ]);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        KaliCreatureVisuals.SetEgoState(Creature, EgoActive);

        _enemyCards = CreateEnemyCardRuntime();
        _enemyCards.RefreshDefaultPlan();

        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<RedMistVerticalSplitEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<RedMistThrustEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<RedMistHorizontalSlashEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<RedMistBloodMistEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<RedMistBattleWillEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<RedMistFocusBreathEgoCard>());
        SaveManager.Instance.MarkCardAsSeen(ModelDb.Card<RedMistFieldOfCorpsesEgoCard>());

        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Ensure<KaliPower>(Creature, 1m, Creature, null);
        await PowerCmdCompat.Ensure<RedMistStrongestOnePower>(Creature, 1m, Creature, null);
        if (EgoActive)
        {
            await PowerCmdCompat.Ensure<RedMistEgoPower>(Creature, 1m, Creature, null);
            if (PersistedBloodMistStacks > 0)
            {
                await PowerCmdCompat.SetAmount<RedMistBloodMistPower>(
                    Creature,
                    PersistedBloodMistStacks,
                    Creature,
                    null);
            }

            int restoredContribution = EgoBaseBuffAmount + PersistedBloodMistStacks;
            _redMistStrongContribution = restoredContribution;
            _redMistEnduranceContribution = restoredContribution;
            EncounterBgmController.ForceCurrentEncounterTrack(EgoBgmPath, "RedMistEgoBGM");
        }
        Log.Info("[LibraryOfRuina.RedMist] Kali opening powers: "
                 + string.Join(",", Creature.Powers.Select(static power => power.Id.Entry)));

        if (NCombatRoom.Instance?.GetCreatureNode(Creature) is { } node)
        {
            await node.RefreshIntents();
        }
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _enemyCards = null;
        _enemyCardSpecs = null;
        PersistedEnemyCardPlanNumber = 0;
        PersistedQueuedExtraCardIds = string.Empty;
        _forceManifestationCardsInNextPlan = false;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var cardSequence = new MoveState(
            "RED_MIST_CARD_SEQUENCE",
            ExecuteEnemyCardPlan,
            CreatePreviewCardIntents());

        cardSequence.FollowUpState = cardSequence;
        return new MonsterMoveStateMachine([cardSequence], cardSequence);
    }

    private async Task ExecuteEnemyCardPlan(IReadOnlyList<Creature> targets)
    {
        EnemyCardRuntime runtime = _enemyCards ??= CreateEnemyCardRuntime();
        if (runtime.CurrentPlan.Count == 0 || runtime.CurrentPlanIndex >= runtime.CurrentPlan.Count)
        {
            runtime.RefreshDefaultPlan();
        }

        _pendingEgoBuffRefresh = false;
        _isExecutingEnemyCardSequence = true;
        try
        {
            await runtime.PlayPlannedHandLikeDownfall(targets);
        }
        finally
        {
            _isExecutingEnemyCardSequence = false;
            ClearStoredIntentPlan();
            await ApplyPendingPostSequenceBuffs();
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);

        if (side == CombatSide.Enemy && !Creature.IsDead)
        {
            _directUnblockedDamageThisEnemyTurn = 0m;
        }

        if (side == CombatSide.Player && !Creature.IsDead)
        {
            IntentCapacity = ResolveReceptionRoundIntentCapacity(
                combatState.RoundNumber,
                EmotionLevel);
            if (ShouldTickEgoReturnCountdown(side))
            {
                await TickEgoReturnCountdown();
            }
            await ResolvePendingEgoManifestation();
            await RandomizeTurnStartResistances(choiceContext);
            EnemyCardRuntime runtime = _enemyCards ??= CreateEnemyCardRuntime();
            if (runtime.CurrentPlan.Count == 0 || runtime.CurrentPlanIndex >= runtime.CurrentPlan.Count)
            {
                runtime.RefreshDefaultPlan();
            }
            await ClearNegativePowers(choiceContext);
            await PowerCmdCompat.Apply<BufferPower>(Creature, 1m, Creature, null);
        }
    }

    protected override async Task AfterSideTurnEndInternal(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && !Creature.IsDead && EgoActive && !_evaluatingRoundDamage)
        {
            _evaluatingRoundDamage = true;
            try
            {
                if (_directUnblockedDamageThisEnemyTurn < ScaledMinimumDirectDamagePerRound)
                {
                    await LoseCurrentChaoPercent();
                }
            }
            finally
            {
                _evaluatingRoundDamage = false;
            }
        }
    }

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal num,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return ClampHpLossForFirstEgoTrigger(target, num);
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        QueueFirstEgoManifestation(creature);
        return Task.CompletedTask;
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta, LibraryDamageType type)
    {
        QueueFirstEgoManifestation(creature);
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult results,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        await TrackUnblockedPlayerDamage(dealer, results, props, target);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult results,
        ValueProp props,
        Creature target,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        await TrackUnblockedPlayerDamage(dealer, results, props, target);
    }

    private decimal ClampHpLossForFirstEgoTrigger(Creature target, decimal num)
    {
        if (target != Creature || num <= 0m)
        {
            return num;
        }

        decimal threshold = ScaledEgoHpThreshold;
        bool lockActive = IsEgoThresholdTurnLockActive();
        decimal resolved = ResolveThresholdTurnLockedHpLoss(
            Creature.CurrentHp,
            num,
            threshold,
            EgoThresholdTurnLockConsumed,
            lockActive,
            out bool activateLock);
        if (activateLock)
        {
            ActivateEgoThresholdTurnLock();
        }

        return resolved;
    }

    private bool IsEgoThresholdTurnLockActive()
    {
        CombatStateLike? combatState = Creature.CombatState;
        return combatState != null
               && EgoThresholdTurnLockRound == combatState.RoundNumber
               && EgoThresholdTurnLockSide == (int)combatState.CurrentSide;
    }

    private void ActivateEgoThresholdTurnLock()
    {
        EgoThresholdTurnLockConsumed = true;
        CombatStateLike? combatState = Creature.CombatState;
        EgoThresholdTurnLockRound = combatState?.RoundNumber ?? -1;
        EgoThresholdTurnLockSide = combatState == null
            ? -1
            : (int)combatState.CurrentSide;
    }

    internal static decimal ResolveThresholdTurnLockedHpLoss(
        decimal currentHp,
        decimal requestedLoss,
        decimal threshold,
        bool lockConsumed,
        bool lockActive,
        out bool activateLock)
    {
        activateLock = false;
        if (requestedLoss <= 0m)
        {
            return requestedLoss;
        }

        decimal hpAfter = currentHp - requestedLoss;
        if (!lockActive && !lockConsumed && hpAfter < threshold)
        {
            activateLock = true;
            lockActive = true;
        }

        return lockActive && hpAfter < threshold
            ? Math.Max(0m, currentHp - threshold)
            : requestedLoss;
    }

    private void QueueFirstEgoManifestation(Creature creature)
    {
        if (creature != Creature
            || !ShouldQueueFirstEgoManifestation(
                EgoTriggered,
                EgoManifestationPending,
                Creature.CurrentHp,
                ScaledEgoHpThreshold))
        {
            return;
        }

        EgoManifestationPending = true;
    }

    internal static bool ShouldQueueFirstEgoManifestation(
        bool egoTriggered,
        bool manifestationPending,
        decimal currentHp,
        decimal threshold) =>
        !egoTriggered && !manifestationPending && currentHp <= threshold;

    private async Task ResolvePendingEgoManifestation()
    {
        if (!EgoManifestationPending || EgoTriggered || Creature.IsDead)
        {
            return;
        }

        await TriggerEgoManifestation(firstTrigger: true);
    }

    /// <summary>
    /// At each player-turn start one random attack type becomes Normal and
    /// every other attack resistance becomes Endure, for both the physical and
    /// chao resistance grids.
    /// </summary>
    private async Task RandomizeTurnStartResistances(PlayerChoiceContext choiceContext)
    {
        if (Creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        LibraryDamageType normalType = ResolvePlanRng().NextItem(ResistanceRotationTypes);
        foreach (LibraryDamageType type in ResistanceRotationTypes)
        {
            LibraryResistanceLevel level = type == normalType
                ? LibraryResistanceLevel.Normal
                : LibraryResistanceLevel.Endure;
            await LibraryCreatureCmd.SetPhysicalResistance(
                choiceContext, libraryCreature, Creature, type, level);
            await LibraryCreatureCmd.SetChaoResistance(
                choiceContext, libraryCreature, Creature, type, level);
        }
    }

    private async Task TrackUnblockedPlayerDamage(
        Creature? dealer,
        DamageResult results,
        ValueProp props,
        Creature target)
    {
        if (dealer != Creature || target.Side != CombatSide.Player || results.UnblockedDamage <= 0m)
        {
            return;
        }

        if (ReferenceEquals(_lastProcessedDamageResult, results))
        {
            return;
        }

        _lastProcessedDamageResult = results;
        if (props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered))
        {
            _directUnblockedDamageThisEnemyTurn += results.UnblockedDamage;
        }

        if (EgoActive)
        {
            await GainBloodMistStack();
        }
    }

    public override async Task AfterCurrentChaoValueChanged(Creature target, decimal amount, LibraryDamageType type)
    {
        if (target == Creature && EgoActive && amount < 0m && target is LibraryCreature { CurrentChaoValue: <= 0 })
        {
            await DismissEgo();
        }
    }

    public override async Task AfterStun(Creature creature)
    {
        if (creature != Creature)
        {
            return;
        }

        if (EgoActive)
        {
            await DismissEgo();
        }

        // EGO dismissal mutates powers after StunInternal's first UI refresh.
        // Refresh once more so Fatal resistances and the native stun intent win visually.
        if (creature is LibraryCreature libraryCreature)
        {
            libraryCreature.HealthBar?.RefreshValues();
        }

        if (NCombatRoom.Instance?.GetCreatureNode(creature) is { } creatureNode)
        {
            await creatureNode.RefreshIntents();
        }
    }

    private async Task TriggerEgoManifestation(bool firstTrigger)
    {
        if (Creature.IsDead)
        {
            return;
        }

        if (firstTrigger)
        {
            EgoManifestationPending = false;
            EgoTriggered = true;
            decimal threshold = ScaledEgoHpThreshold;
            if (Creature.CurrentHp < threshold)
            {
                await CreatureCmd.SetCurrentHp(Creature, threshold);
            }
        }

        EgoReturnCountdown = 0;
        EgoActive = true;
        _forceManifestationCardsInNextPlan = true;
        ClearStoredIntentPlan();
        _enemyCards?.RefreshDefaultPlan();

        if (Creature is LibraryCreature lc)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(lc, lc.MaxChaoValue);
        }

        await PowerCmdCompat.Ensure<RedMistEgoPower>(
            Creature,
            1m,
            Creature,
            null);

        if (PersistedBloodMistStacks > 0)
        {
            RedMistBloodMistPower? bloodMistPower = Creature.GetPower<RedMistBloodMistPower>();
            if (bloodMistPower == null)
            {
                await PowerCmdCompat.Apply<RedMistBloodMistPower>(Creature, PersistedBloodMistStacks, Creature, null);
            }
            else
            {
                bloodMistPower.SetAmount(PersistedBloodMistStacks, silent: true);
            }
        }

        await RefreshRedMistStrongEnduranceContributions();
        KaliCreatureVisuals.SetEgoState(Creature, active: true);
        EncounterBgmController.ForceCurrentEncounterTrack(EgoBgmPath, "RedMistEgoBGM");
        NCombatRoom.Instance?.GetCreatureNode(Creature)?.RefreshIntents();
    }

    private async Task DismissEgo()
    {
        if (!EgoActive)
        {
            return;
        }

        EgoActive = false;
        EgoReturnCountdown = EgoReturnTurns;
        _forceManifestationCardsInNextPlan = false;
        await RefreshRedMistStrongEnduranceContributions();

        PowerModel? egoPower = Creature.GetPower<RedMistEgoPower>();
        if (egoPower != null)
        {
            await PowerCmd.Remove(egoPower);
        }

        PowerModel? bloodMistPower = Creature.GetPower<RedMistBloodMistPower>();
        if (bloodMistPower != null)
        {
            await PowerCmd.Remove(bloodMistPower);
        }

        KaliCreatureVisuals.SetEgoState(Creature, active: false);
    }

    private async Task TickEgoReturnCountdown()
    {
        if (EgoActive || !EgoTriggered || EgoReturnCountdown <= 0)
        {
            return;
        }

        EgoReturnCountdown = ResolveEgoReturnCountdownAfterPlayerTurnStart(
            EgoReturnCountdown);
        if (EgoReturnCountdown <= 0)
        {
            await TriggerEgoManifestation(firstTrigger: false);
        }
    }

    private async Task GainBloodMistStack()
    {
        if (PersistedBloodMistStacks >= BloodMistMaxStacks)
        {
            return;
        }

        PersistedBloodMistStacks++;
        await PowerCmdCompat.SetAmount<RedMistBloodMistPower>(Creature, PersistedBloodMistStacks, Creature, null);
        await QueueOrApplyEgoBuffRefresh();
    }

    private async Task QueueOrApplyEgoBuffRefresh()
    {
        if (_isExecutingEnemyCardSequence)
        {
            _pendingEgoBuffRefresh = true;
            return;
        }

        await RefreshRedMistStrongEnduranceContributions();
    }

    private async Task ApplyPendingPostSequenceBuffs()
    {
        bool changed = _pendingEgoBuffRefresh;
        _pendingEgoBuffRefresh = false;

        if (changed)
        {
            await RefreshRedMistStrongEnduranceContributions();
        }
    }

    private async Task RefreshRedMistStrongEnduranceContributions()
    {
        int egoBuffAmount = EgoActive ? EgoBaseBuffAmount + PersistedBloodMistStacks : 0;
        await SetRedMistStrongContribution(egoBuffAmount);
        await SetRedMistEnduranceContribution(egoBuffAmount);
        Log.Info(
            "[LibraryOfRuina.RedMist] Strong/Endurance refreshed: strong=" +
            _redMistStrongContribution +
            " endurance=" +
            _redMistEnduranceContribution +
            " bloodMist=" +
            PersistedBloodMistStacks);
    }

    private async Task SetRedMistStrongContribution(int targetContribution)
    {
        int clamped = Math.Max(0, targetContribution);
        int delta = clamped - _redMistStrongContribution;
        if (delta == 0)
        {
            return;
        }

        LibraryStrongPower? power = Creature.GetPower<LibraryStrongPower>();
        if (delta > 0 && power == null)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                delta,
                0,
                true,
                Creature,
                null);
        }
        else if (power != null)
        {
            await PowerCmdCompat.ModifyAmount(power, delta, Creature, null);
        }

        _redMistStrongContribution = clamped;
    }

    private async Task SetRedMistEnduranceContribution(int targetContribution)
    {
        int clamped = Math.Max(0, targetContribution);
        int delta = clamped - _redMistEnduranceContribution;
        if (delta == 0)
        {
            return;
        }

        LibraryEndurancePower? power = Creature.GetPower<LibraryEndurancePower>();
        if (delta > 0 && power == null)
        {
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                delta,
                0,
                true,
                Creature,
                null);
        }
        else if (power != null)
        {
            await PowerCmdCompat.ModifyAmount(power, delta, Creature, null);
        }

        _redMistEnduranceContribution = clamped;
    }

    private async Task ClearNegativePowers(PlayerChoiceContext choiceContext)
    {
        foreach (PowerModel power in Creature.Powers.ToArray())
        {
            if (power.TypeForCurrentAmount == PowerType.Debuff || power.Type == PowerType.Debuff)
            {
                await PowerCmd.Remove(power);
            }
        }
    }

    private async Task LoseCurrentChaoPercent()
    {
        if (Creature is not LibraryCreature lc || lc.CurrentChaoValue <= 0)
        {
            return;
        }

        decimal loss = Math.Ceiling(lc.CurrentChaoValue * ChaoLossPercent / 100m);
        await LibraryCreatureCmd.SetCurrentChaoValue(lc, Math.Max(0m, lc.CurrentChaoValue - loss));
    }

    private EnemyCardRuntime CreateEnemyCardRuntime()
    {
        var runtime = new EnemyCardRuntime(this, 0, GetCurrentPlan);
        runtime.InitializeDeck(AllEnemyCardSpecs());
        return runtime;
    }

    private IReadOnlyList<EnemyCardSpec> GetCurrentPlan()
    {
        Dictionary<string, EnemyCardSpec> specs = EnemyCardSpecs;
        if (HasStoredIntentPlan)
        {
            EnemyCardSpec[] restored = Enumerable.Range(0, StoredIntentSlots)
                .Select(GetStoredIntent)
                .TakeWhile(static move => move >= 0)
                .Select(StoredMoveToCardId)
                .Where(static cardId => cardId != null)
                .Select(cardId => specs[cardId!])
                .ToArray();
            if (restored.Length > 0)
            {
                return restored;
            }

            ClearStoredIntentPlan();
        }

        PersistedEnemyCardPlanNumber++;
        int cardLimit = ResolvePlanCardLimit(
            PersistedEnemyCardPlanNumber,
            IntentCapacity);
        bool forceManifestationCards = _forceManifestationCardsInNextPlan;
        _forceManifestationCardsInNextPlan = false;
        IReadOnlyList<string> planCardIds = BuildPlanCardIds(
            cardLimit,
            EgoActive,
            forceManifestationCards,
            GetQueuedExtraCardIds(),
            PickOne);
        PersistedQueuedExtraCardIds = string.Empty;
        EnemyCardSpec[] plan = planCardIds
            .Select(cardId => specs[cardId])
            .ToArray();
        SaveStoredPlan(plan);
        return plan;
    }

    internal static IReadOnlyList<string> BuildPlanCardIds(
        int cardLimit,
        bool egoActive,
        bool forceManifestationCards,
        IReadOnlyList<string> queuedCardIds,
        Func<IReadOnlyList<string>, string> pickOne)
    {
        int resolvedLimit = Math.Clamp(cardLimit, 1, StoredIntentSlots);
        if (forceManifestationCards)
        {
            resolvedLimit = Math.Max(
                resolvedLimit,
                ManifestationRequiredCardIds.Count);
        }

        var plan = new List<string>(resolvedLimit);
        if (forceManifestationCards)
        {
            plan.AddRange(ManifestationRequiredCardIds.Take(resolvedLimit));
        }

        foreach (string cardId in queuedCardIds)
        {
            if (plan.Count >= resolvedLimit)
            {
                break;
            }

            if (RandomPlanCardIds.Contains(cardId, StringComparer.Ordinal))
            {
                plan.Add(cardId);
            }
        }

        IReadOnlyList<string> randomPool = egoActive
            ? RandomPlanCardIds
            : RandomPlanCardIds
                .Where(cardId => cardId != RedMistFieldOfCorpsesCardId)
                .ToArray();
        while (plan.Count < resolvedLimit)
        {
            string[] unusedCardIds = randomPool
                .Where(cardId => !plan.Contains(cardId, StringComparer.Ordinal))
                .ToArray();
            IReadOnlyList<string> candidates = unusedCardIds.Length > 0
                ? unusedCardIds
                : randomPool;
            plan.Add(pickOne(candidates));
        }

        return plan;
    }

    private void SaveStoredPlan(IReadOnlyList<EnemyCardSpec> plan)
    {
        ClearStoredIntentPlan();
        int count = Math.Min(plan.Count, StoredIntentSlots);
        for (int slot = 0; slot < count; slot++)
        {
            SetStoredIntent(slot, CardIdToStoredMove(plan[slot].Id));
        }
    }

    private IReadOnlyList<string> GetQueuedExtraCardIds() =>
        PersistedQueuedExtraCardIds.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void QueueExtraCardId(string cardId)
    {
        PersistedQueuedExtraCardIds = string.IsNullOrEmpty(PersistedQueuedExtraCardIds)
            ? cardId
            : PersistedQueuedExtraCardIds + "\n" + cardId;
    }

    private static int CardIdToStoredMove(string cardId) => cardId switch
    {
        RedMistVerticalSplitCardId => 0,
        RedMistThrustCardId => 1,
        RedMistHorizontalSlashCardId => 2,
        RedMistFocusBreathCardId => 3,
        RedMistBattleWillCardId => 4,
        RedMistBloodMistCardId => 5,
        RedMistFieldOfCorpsesCardId => 6,
        _ => -1,
    };

    private static string? StoredMoveToCardId(int move) => move switch
    {
        0 => RedMistVerticalSplitCardId,
        1 => RedMistThrustCardId,
        2 => RedMistHorizontalSlashCardId,
        3 => RedMistFocusBreathCardId,
        4 => RedMistBattleWillCardId,
        5 => RedMistBloodMistCardId,
        6 => RedMistFieldOfCorpsesCardId,
        _ => null,
    };

    private static int GetPlanCardLimit(int planNumber)
    {
        return planNumber switch
        {
            1 => OpeningPlanFirstTurnIntentCount,
            2 => OpeningPlanSecondTurnIntentCount,
            _ => int.MaxValue
        };
    }

    internal static int ResolvePlanCardLimit(
        int planNumber,
        int intentCapacity) =>
        Math.Min(
            GetPlanCardLimit(planNumber),
            Math.Clamp(intentCapacity, 1, StoredIntentSlots));

    private IReadOnlyList<EnemyCardSpec> AllEnemyCardSpecs()
    {
        return EnemyCardSpecs.Values.ToArray();
    }

    private Dictionary<string, EnemyCardSpec> EnemyCardSpecs =>
        _enemyCardSpecs ??= CreateEnemyCardSpecs();

    private Dictionary<string, EnemyCardSpec> CreateEnemyCardSpecs()
    {
        return new Dictionary<string, EnemyCardSpec>
        {
            [RedMistVerticalSplitCardId] = new(
                RedMistVerticalSplitCardId,
                CreateVerticalSplitDisplayCard,
                cost: 2,
                priority: 20,
                execute: (_, targets) => PlayVerticalSplit(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => VerticalSplitDamage, () => VerticalSplitHits)),
            [RedMistThrustCardId] = new(
                RedMistThrustCardId,
                CreateThrustDisplayCard,
                cost: 2,
                priority: 20,
                execute: (_, targets) => PlayThrust(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => ThrustDamage, () => ThrustHits)),
            [RedMistHorizontalSlashCardId] = new(
                RedMistHorizontalSlashCardId,
                CreateHorizontalSlashDisplayCard,
                cost: 2,
                priority: 30,
                execute: (_, targets) => PlayHorizontalSlash(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => HorizontalSlashDamage, () => HorizontalSlashHits)),
            [RedMistFocusBreathCardId] = new(
                RedMistFocusBreathCardId,
                CreateFocusBreathDisplayCard,
                cost: 2,
                priority: 70,
                execute: (_, _) => PlayFocusBreath(),
                createIntent: spec => EnemyCardCombinedIntentFactory.DefendBuff(spec)),
            [RedMistBattleWillCardId] = new(
                RedMistBattleWillCardId,
                CreateBattleWillDisplayCard,
                cost: 3,
                priority: 50,
                execute: (_, targets) => PlayBattleWill(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => BattleWillDamage)),
            [RedMistBloodMistCardId] = new(
                RedMistBloodMistCardId,
                CreateBloodMistDisplayCard,
                cost: 5,
                priority: 40,
                execute: (_, targets) => PlayBloodMist(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => BloodMistDamage)),
            [RedMistFieldOfCorpsesCardId] = new(
                RedMistFieldOfCorpsesCardId,
                CreateFieldOfCorpsesDisplayCard,
                cost: 6,
                priority: 50,
                execute: (_, targets) => PlayFieldOfCorpses(targets),
                createIntent: spec => new EnemyCardAttackIntent(spec, () => FieldOfCorpsesDamage))
        };
    }

    private AbstractIntent[] CreatePreviewCardIntents()
    {
        Dictionary<string, EnemyCardSpec> specs = EnemyCardSpecs;
        return EnemyCardSpec.CreateIntentSequence(
        [
            specs[RedMistVerticalSplitCardId],
            specs[RedMistThrustCardId],
            specs[RedMistBloodMistCardId]
        ]).ToArray();
    }

    private string PickOne(IReadOnlyList<string> cardIds)
    {
        return ResolvePlanRng().NextItem(cardIds)
               ?? cardIds[0];
    }

    private Rng ResolvePlanRng()
    {
        return Creature?.CombatState?.RunState.Rng.MonsterAi ?? Rng;
    }

    private void QueueRepeatIfThreshold(IReadOnlyList<DamageResult> results, string cardId)
    {
        decimal unblockedDamage = results.Sum(static result => result.UnblockedDamage);
        if (unblockedDamage >= RepeatUnblockedDamageThreshold)
        {
            QueueExtraCardId(cardId);
        }
    }

    private async Task PlayVerticalSplit(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> results = await ExecuteMoveAttack(
            GetLivingTargets(targets),
            VerticalSplitDamage,
            VerticalSplitHits,
            SlashHitVfx,
            "AttackSlash");
        QueueRepeatIfThreshold(results, RedMistVerticalSplitCardId);
    }

    private async Task PlayThrust(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> results = await ExecuteMoveAttack(
            GetLivingTargets(targets),
            ThrustDamage,
            ThrustHits,
            PierceHitVfx,
            "AttackPierce");
        QueueRepeatIfThreshold(results, RedMistThrustCardId);
    }

    private async Task PlayHorizontalSlash(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> results = await ExecuteMoveAttack(
            GetLivingTargets(targets),
            HorizontalSlashDamage,
            HorizontalSlashHits,
            SlashHitVfx,
            "AttackSlash");
        IReadOnlyList<Creature> bleedTargets = results
            .Where(static result => result.Receiver.IsPlayer && result.UnblockedDamage > 0m)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
        if (bleedTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(
                bleedTargets,
                HorizontalSlashBleed,
                Creature,
                null);
        }

        QueueRepeatIfThreshold(results, RedMistHorizontalSlashCardId);
    }

    private async Task PlayBloodMist(IReadOnlyList<Creature> targets)
    {
        await ExecuteMoveAttack(
            GetLivingTargets(targets),
            BloodMistDamage,
            1,
            SlashHitVfx,
            "BloodMist",
            onUnblockedHit: target => ExhaustRandomDrawPileCard(new ThrowingPlayerChoiceContext(), this, target));
    }

    private async Task PlayBattleWill(IReadOnlyList<Creature> targets)
    {
        HashSet<Creature> repeatedTargets = [];
        await ExecuteMoveAttack(
            GetLivingTargets(targets),
            BattleWillDamage,
            1,
            BluntHitVfx,
            "AttackBlunt",
            onUnblockedHit: async hitTarget =>
            {
                if (!repeatedTargets.Add(hitTarget))
                {
                    return;
                }

                await ExecuteMoveAttack([hitTarget], BattleWillDamage, 1, BluntHitVfx, "AttackBlunt");
            });
    }

    private async Task PlayFocusBreath()
    {
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.4f);
        await CreatureCmd.GainBlock(Creature, FocusBreathBlock, ValueProp.Move, null);
        await LibraryPowerCmd.Apply<QueenBeeNextTurnStrongPower>(
            Creature,
            FocusBreathStrong,
            Creature,
            null);
    }

    private async Task PlayFieldOfCorpses(IReadOnlyList<Creature> targets)
    {
        await ExecuteMoveAttack(
            GetLivingTargets(targets),
            FieldOfCorpsesDamage,
            1,
            SlashHitVfx,
            "FieldOfCorpses",
            onUnblockedHit: target => PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                target,
                FieldOfCorpsesBleed,
                Creature,
                null),
            attackerAnimDelaySeconds: KaliCreatureVisuals.FieldOfCorpsesAnimationSeconds);
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteMoveAttack(
        IReadOnlyList<Creature> targets,
        int damage,
        int hits,
        string hitVfx,
        string anim,
        Func<Creature, Task>? onUnblockedHit = null,
        float? attackerAnimDelaySeconds = null)
    {
        IReadOnlyList<Creature> livingTargets = GetLivingTargets(targets);
        if (livingTargets.Count == 0 || !CanContinueMove)
        {
            return [];
        }

        var results = new List<DamageResult>();
        int hitCount = Math.Max(1, hits);
        IReadOnlyList<string> attackAnimations = ResolveAttackAnimationSequence(
            hitCount,
            anim,
            GetDeterministicAttackAnimationSeed(hitCount, anim));
        bool continuousAttack = hitCount > 1;
        if (continuousAttack)
        {
            KaliCreatureVisuals.BeginAttackChain(Creature);
        }

        try
        {
            for (int i = 0; i < hitCount; i++)
            {
                if (!CanContinueMove)
                {
                    return results;
                }

                string segmentAnim = attackAnimations[i];
                string segmentHitVfx = hitCount > 1
                    ? ResolveAttackSegmentVfx(segmentAnim)
                    : hitVfx;
                AttackCommand command;
                bool deferAttackSfxUntilSettlement =
                    RequiresCompletedAnimationBeforeSettlement(segmentAnim);
                if (!deferAttackSfxUntilSettlement)
                {
                    PlayAttackSegmentSfx(segmentAnim);
                }

                bool animationCompletedBeforeSettlement =
                    await PlayAttackerAnimationBeforeSettlement(
                        Creature,
                        segmentAnim);
                if (deferAttackSfxUntilSettlement)
                {
                    PlayAttackSegmentSfx(segmentAnim);
                }

                using (TargetedMonsterAttackHelper.ForceTargets(Creature, livingTargets))
                {
                    if (!CanContinueMove)
                    {
                        return results;
                    }

                    AttackCommand attack = DamageCmd.Attack(damage)
                        .FromMonster(this)
                        .WithHitCount(1)
                        .WithHitFx(segmentHitVfx);
                    if (animationCompletedBeforeSettlement)
                    {
                        attack.WithNoAttackerAnim();
                    }
                    else
                    {
                        attack.WithAttackerAnim(
                            segmentAnim,
                            attackerAnimDelaySeconds
                            ?? ResolveAttackerAnimationDelaySeconds(segmentAnim));
                    }

                    command = await attack.Execute(null);
                }

                IReadOnlyList<DamageResult> hitResults = AttackCommandCompat.Results(command);
                RecordDirectAttackDamageDealt(hitResults);
                foreach (DamageResult result in hitResults)
                {
                    results.Add(result);
                }

                if (!CanContinueMove)
                {
                    return results;
                }

                foreach (DamageResult result in hitResults)
                {
                    if (onUnblockedHit != null && result.UnblockedDamage > 0m)
                    {
                        await onUnblockedHit(result.Receiver);
                        if (!CanContinueMove)
                        {
                            return results;
                        }
                    }
                }

                if (i + 1 < hitCount)
                {
                    if (!CanContinueMove)
                    {
                        return results;
                    }

                    await Cmd.CustomScaledWait(0.04f, 0.08f);
                }
            }
        }
        finally
        {
            if (continuousAttack)
            {
                KaliCreatureVisuals.EndAttackChain(Creature);
            }
        }

        return results;
    }

    private static IReadOnlyList<Creature> GetLivingTargets(IEnumerable<Creature> targets)
    {
        return targets
            .Where(static target => target is { IsAlive: true })
            .Distinct()
            .ToArray();
    }

    private bool CanContinueMove => Creature is { IsDead: false, CombatState: not null };

    public bool UsesTargetedAttackContract(Creature owner)
    {
        return false;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        return owner.CombatState?
            .GetOpponentsOf(owner)
            .Where(static target => target.IsAlive)
            .ToArray()
            ?? [];
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        IReadOnlyList<Creature> targets = GetTargetedAttackTargets(owner);
        return targets.Count switch
        {
            0 => "Unknown Target",
            1 => targets[0].Name,
            _ => "All Players"
        };
    }

    public static async Task<IReadOnlyList<DamageResult>> ExecuteCardAttack(
        PlayerChoiceContext choiceContext,
        CardModel card,
        Creature target,
        int damage,
        int hits,
        string hitVfx,
        string anim,
        Func<Creature, Task>? onUnblockedHit = null)
    {
        var results = new List<DamageResult>();
        int hitCount = Math.Max(1, hits);
        IReadOnlyList<string> attackAnimations = ResolveAttackAnimationSequence(
            hitCount,
            anim,
            GetDeterministicAttackAnimationSeed(hitCount, anim));
        for (int i = 0; i < hitCount; i++)
        {
            string segmentAnim = attackAnimations[i];
            string segmentHitVfx = hitCount > 1
                ? ResolveAttackSegmentVfx(segmentAnim)
                : hitVfx;
            bool deferAttackSfxUntilSettlement =
                RequiresCompletedAnimationBeforeSettlement(segmentAnim);
            if (!deferAttackSfxUntilSettlement)
            {
                PlayAttackSegmentSfx(segmentAnim);
            }

            bool animationCompletedBeforeSettlement =
                await PlayAttackerAnimationBeforeSettlement(
                    card.Owner.Creature,
                    segmentAnim);
            if (deferAttackSfxUntilSettlement)
            {
                PlayAttackSegmentSfx(segmentAnim);
            }

            AttackCommand attack = DamageCmd.Attack(damage)
                .FromCard(card, null)
                .Targeting(target)
                .WithHitCount(1)
                .WithHitFx(segmentHitVfx);
            if (animationCompletedBeforeSettlement)
            {
                attack.WithNoAttackerAnim();
            }
            else
            {
                attack.WithAttackerAnim(
                    segmentAnim,
                    ResolveAttackerAnimationDelaySeconds(segmentAnim));
            }

            AttackCommand command = await attack.Execute(choiceContext);

            foreach (DamageResult result in AttackCommandCompat.Results(command))
            {
                results.Add(result);
                if (onUnblockedHit != null && result.UnblockedDamage > 0m)
                {
                    await onUnblockedHit(result.Receiver);
                }
            }

            if (i + 1 < hitCount)
            {
                await Cmd.CustomScaledWait(0.04f, 0.08f);
            }
        }

        return results;
    }

    internal static float ResolveAttackerAnimationDelaySeconds(string anim) => anim switch
    {
        "BloodMist" => 0f,
        _ => AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds
    };

    internal static bool RequiresCompletedAnimationBeforeSettlement(string anim) =>
        string.Equals(anim, "BloodMist", StringComparison.Ordinal);

    internal static float ResolvePreSettlementAnimationWaitSeconds(string anim) =>
        RequiresCompletedAnimationBeforeSettlement(anim)
            ? KaliCreatureVisuals.BloodMistAnimationSeconds
            : 0f;

    private static async Task<bool> PlayAttackerAnimationBeforeSettlement(
        Creature attacker,
        string anim)
    {
        float waitSeconds = ResolvePreSettlementAnimationWaitSeconds(anim);
        if (waitSeconds <= 0f)
        {
            return false;
        }

        await CreatureCmd.TriggerAnim(attacker, anim, 0f);
        await Cmd.Wait(waitSeconds);
        return true;
    }

    private static void PlayAttackSegmentSfx(string anim)
    {
        LocalOggOneShotPlayer.Play(ResolveAttackSegmentSfx(anim), -2f);
    }

    private static string ResolveAttackSegmentSfx(string anim) => anim switch
    {
        "AttackPierce" => RedMistPierceSfxPath,
        "AttackBlunt" => RedMistBluntSfxPath,
        _ => RedMistSlashSfxPath
    };

    private static string ResolveAttackSegmentVfx(string anim) => anim switch
    {
        "AttackPierce" => PierceHitVfx,
        "AttackBlunt" => BluntHitVfx,
        _ => SlashHitVfx
    };

    private static int GetDeterministicAttackAnimationSeed(
        int hitCount,
        string fallbackAnimation)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + hitCount;
            foreach (char character in fallbackAnimation)
            {
                hash = hash * 31 + character;
            }

            return hash;
        }
    }

    internal static IReadOnlyList<string> ResolveAttackAnimationSequence(
        int hits,
        string fallbackAnimation,
        int seed)
    {
        int hitCount = Math.Max(1, hits);
        if (hitCount == 1)
        {
            return [fallbackAnimation];
        }

        var random = new Random(seed);
        var result = new List<string>(hitCount);
        string? previous = null;
        while (result.Count < hitCount)
        {
            string[] bag = BasicAttackAnimations.ToArray();
            for (int i = bag.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (bag[i], bag[swapIndex]) = (bag[swapIndex], bag[i]);
            }

            if (previous != null && bag[0] == previous)
            {
                int swapIndex = random.Next(1, bag.Length);
                (bag[0], bag[swapIndex]) = (bag[swapIndex], bag[0]);
            }

            foreach (string animation in bag)
            {
                if (result.Count >= hitCount)
                {
                    break;
                }

                result.Add(animation);
                previous = animation;
            }
        }

        return result;
    }

    public static async Task ExhaustRandomDrawPileCard(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature target)
    {
        Player? targetPlayer = target.Player;
        if (targetPlayer == null)
        {
            return;
        }

        IReadOnlyList<CardModel> drawCards = PileType.Draw.GetPile(targetPlayer).Cards;
        if (drawCards.Count == 0)
        {
            return;
        }

        CardModel? card = owner.RunState.Rng.CombatCardSelection.NextItem(drawCards);
        if (card == null)
        {
            return;
        }

        await CardCmd.Exhaust(choiceContext, card);
    }

    public static async Task ExhaustRandomDrawPileCard(
        PlayerChoiceContext choiceContext,
        MonsterModel owner,
        Creature target)
    {
        Player? targetPlayer = target.Player;
        if (targetPlayer == null)
        {
            return;
        }

        IReadOnlyList<CardModel> drawCards = PileType.Draw.GetPile(targetPlayer).Cards;
        if (drawCards.Count == 0)
        {
            return;
        }

        CardModel? card = owner.Creature?.CombatState?.RunState.Rng.CombatCardSelection.NextItem(drawCards);
        if (card == null)
        {
            return;
        }

        await CardCmd.Exhaust(choiceContext, card);
    }

    private static CardModel CreateVerticalSplitDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistVerticalSplitEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(VerticalSplitDamage); });
    }

    private static CardModel CreateThrustDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistThrustEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(ThrustDamage); });
    }

    private static CardModel CreateHorizontalSlashDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistHorizontalSlashEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(HorizontalSlashDamage); });
    }

    private static CardModel CreateBloodMistDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistBloodMistEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(BloodMistDamage); });
    }

    private static CardModel CreateBattleWillDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistBattleWillEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(BattleWillDamage); });
    }

    private static CardModel CreateFocusBreathDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistFocusBreathEgoCard>(
            card => { card.UpgradePreview(); });
    }

    private static CardModel CreateFieldOfCorpsesDisplayCard()
    {
        return EnemyCardSpec.CreateDisplayCardModel<RedMistFieldOfCorpsesEgoCard>(
            card => { card.UpgradePreview(); card.SetPreviewDamage(FieldOfCorpsesDamage); });
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        foreach (EnemyCardSpec spec in AllEnemyCardSpecs())
        {
            yield return spec.CreateIntentInstance();
        }
    }

}

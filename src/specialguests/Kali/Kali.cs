using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.cards.RedMist;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.RedMist;
using LibraryOfRuina.ui;
using LibraryOfRuina.visuals.RedMist;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Kali;

/// <summary>
/// 卡莉（红雾）。按职责分在几个 partial 文件里：本文件是常量、战斗状态与回合钩子；
/// <c>Kali.Ego.cs</c> 是 E.G.O. 的显现、解除、再显现与血雾层数；<c>Kali.CardPlan.cs</c> 是敌方卡牌计划
/// （计划存在基类的五个槽位里）；<c>Kali.Moves.cs</c> 是各张卡的执行与卡牌共用的攻击演出。
/// 纯规则方法（<c>BuildPlanCardIds</c>、<c>ResolvePlanCardLimit</c> 等）被卡牌与验证套件按 <c>Kali.X</c> 引用，留在本类型上。
/// </summary>
public sealed partial class Kali : SpecialGuestMonsterBase, IEnemyCardRuntimeOwner, ITargetedMonsterAttackProvider, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
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

    public bool EgoTriggered { get; private set; }

    public bool EgoActive { get; private set; }

    public bool EgoManifestationPending { get; private set; }

    public int EgoReturnCountdown { get; private set; }

    public int PersistedBloodMistStacks { get; private set; }

    public int PersistedEnemyCardPlanNumber { get; private set; }

    public string PersistedQueuedExtraCardIds { get; private set; } = string.Empty;

    public bool EgoThresholdTurnLockConsumed { get; private set; }

    public int EgoThresholdTurnLockRound { get; private set; } = -1;

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

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        foreach (EnemyCardSpec spec in AllEnemyCardSpecs())
        {
            yield return spec.CreateIntentInstance();
        }
    }

}

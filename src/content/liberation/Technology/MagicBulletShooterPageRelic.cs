using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed class MagicBulletShooterPageRelic : ModalPageRelic<MagicBulletShooterPageMode>
{
    internal const int CommissionDamagePercent = 50;
    internal const int CommissionGoldPerKill = 80;
    internal const int SeventhBulletStrong = 7;
    internal const int SeventhBulletAttackInterval = 7;
    internal const int SeventhBulletPlayerDamageReductionPercent = 80;
    internal const int BlackFlameDamageTakenPercent = 10;
    internal const int BlackFlameTurnInterval = 2;

    private HashSet<Creature> _pendingCommissionKills = [];
    private CardModel? _seventhAttackCard;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override string PackedIconPath => TechnologyFloorAssets.LiberationEncounterRunHistoryIcon;

    protected override string PackedIconOutlinePath =>
        TechnologyFloorAssets.LiberationEncounterOutlineRunHistoryIcon;

    protected override string BigIconPath => TechnologyFloorAssets.LiberationEncounterRunHistoryIcon;

    public override bool ShowCounter =>
        CombatManager.Instance.IsInProgress
        && Mode is MagicBulletShooterPageMode.SeventhBullet
            or MagicBulletShooterPageMode.BlackFlame;

    public override int DisplayAmount => Mode switch
    {
        MagicBulletShooterPageMode.SeventhBullet => AttackCardProgressThisCombat,
        MagicBulletShooterPageMode.BlackFlame =>
            BlackFlameRoundProgressThisCombat,
        _ => 0
    };

    public override bool IsAllowed(IRunState runState)
    {
        _ = runState;
        return false;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)MagicBulletShooterPageMode.None),
        new DynamicVar("CommissionDamagePercent", CommissionDamagePercent),
        new GoldVar(CommissionGoldPerKill),
        new PowerVar<LibraryStrongPower>("Strong", SeventhBulletStrong),
        new DynamicVar("AttackInterval", SeventhBulletAttackInterval),
        new DynamicVar("PlayerDamageReductionPercent", SeventhBulletPlayerDamageReductionPercent),
        new DynamicVar("DamageTakenPercent", BlackFlameDamageTakenPercent),
        new DynamicVar("BlackFlameTurnInterval", BlackFlameTurnInterval)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        MagicBulletShooterPageMode.Commission =>
        [
            HoverTipFactory.FromPower<MagicBulletCommissionTargetPower>()
        ],
        MagicBulletShooterPageMode.SeventhBullet =>
        [
            HoverTipFactory.FromPower<LibraryStrongPower>()
        ],
        MagicBulletShooterPageMode.BlackFlame =>
        [
            HoverTipFactory.FromPower<MagicBulletBlackFlameResistancePower>()
        ],
        _ => []
    };

    [SavedProperty]
    public MagicBulletShooterPageMode Mode { get; private set; }

    protected override MagicBulletShooterPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshIconOnModeChange => false;

    protected override bool RefreshUiBeforeModeChoice => true;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CommissionKillsThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool CommissionMarkedThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int AttackCardProgressThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BlackFlameRoundProgressThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BlackFlameActiveThisRound { get; private set; }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pendingCommissionKills = [];
        _seventhAttackCard = null;
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        _seventhAttackCard = null;
        _pendingCommissionKills.Clear();
        UpdateModeUiState();
        if (room is CombatRoom
            && Mode == MagicBulletShooterPageMode.BlackFlame
            && IsBlackFlameRoundActive(Owner.Creature.CombatState))
        {
            await ApplyBlackFlameToAllEnemies();
        }
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetTransientCombatState();
        UpdateModeUiState();

        if (Mode == MagicBulletShooterPageMode.SeventhBullet)
        {
            Flash();
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                Owner.Creature,
                SeventhBulletStrong,
                turns: -1,
                Owner.Creature,
                null);
            return;
        }

    }

    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (Mode == MagicBulletShooterPageMode.BlackFlame
            && IsBlackFlameRoundActive(creature.CombatState))
        {
            await ApplyBlackFlamePower(creature);
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        _ = choiceContext;
        _ = participants;
        if (Mode != MagicBulletShooterPageMode.BlackFlame
            || side != Owner.Creature.Side)
        {
            return;
        }

        int roundNumber = Math.Max(1, combatState.RoundNumber);
        BlackFlameRoundProgressThisCombat =
            (roundNumber - 1) % BlackFlameTurnInterval + 1;
        BlackFlameActiveThisRound =
            BlackFlameRoundProgressThisCombat == BlackFlameTurnInterval;
        UpdateModeUiState();

        if (BlackFlameActiveThisRound)
        {
            Flash();
            await ApplyBlackFlameToAllEnemies();
        }
        else
        {
            await RemoveBlackFlameFromAllEnemies();
        }
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Mode != MagicBulletShooterPageMode.SeventhBullet
            || cardPlay.Player != Owner
            || cardPlay.Card.Type != CardType.Attack)
        {
            return Task.CompletedTask;
        }

        _seventhAttackCard = null;
        AttackCardProgressThisCombat++;
        if (AttackCardProgressThisCombat >= SeventhBulletAttackInterval)
        {
            AttackCardProgressThisCombat = SeventhBulletAttackInterval;
            _seventhAttackCard = cardPlay.Card;
            Flash();
        }

        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        _ = choiceContext;
        if (ReferenceEquals(_seventhAttackCard, cardPlay.Card))
        {
            _seventhAttackCard = null;
            AttackCardProgressThisCombat = 0;
            UpdateModeUiState();
        }

        return Task.CompletedTask;
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource) =>
        HandleCommissionDamage(
            choiceContext,
            dealer,
            result,
            props,
            target,
            cardSource);

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        _ = type;
        return HandleCommissionDamage(
            choiceContext,
            dealer,
            result,
            props,
            target,
            cardSource);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        _ = choiceContext;
        _ = deathAnimLength;
        if (!_pendingCommissionKills.Remove(creature))
        {
            return Task.CompletedTask;
        }

        if (!wasRemovalPrevented)
        {
            CommissionKillsThisCombat++;
            Flash();
        }

        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        _ = room;
        if (Mode != MagicBulletShooterPageMode.Commission
            || CommissionKillsThisCombat <= 0)
        {
            return;
        }

        int gold = CommissionKillsThisCombat * CommissionGoldPerKill;
        Flash();
        foreach (Player player in Owner.RunState.Players.OrderBy(
            static player => player.NetId))
        {
            await PlayerCmd.GainGold(gold, player);
        }

        CommissionKillsThisCombat = 0;
        UpdateModeUiState();
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        _ = room;
        await RemoveBlackFlameFromAllEnemies();
        _seventhAttackCard = null;
        _pendingCommissionKills.Clear();
        BlackFlameActiveThisRound = false;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        _ = cardSource;
        _ = cardPlay;
        if (Mode == MagicBulletShooterPageMode.SeventhBullet
            && dealer == Owner.Creature
            && target is { IsPlayer: true })
        {
            return 1m - SeventhBulletPlayerDamageReductionPercent / 100m;
        }

        return ShouldIncreaseBlackFlameDamageTaken(
            target,
            amount,
            props,
            dealer)
                ? 1m + BlackFlameDamageTakenPercent / 100m
                : 1m;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        _ = cardSource;
        _ = cardPlay;
        _ = type;
        return ShouldIncreaseBlackFlameDamageTaken(
            target,
            amount,
            props,
            dealer)
                ? 1m + BlackFlameDamageTakenPercent / 100m
                : 1m;
    }

    internal bool IsSeventhBulletCard(CardModel? card) =>
        Mode == MagicBulletShooterPageMode.SeventhBullet
        && ReferenceEquals(_seventhAttackCard, card);

    internal static LibraryResistanceLevel TransformBlackFlameResistance(
        LibraryResistanceLevel resistance) => resistance switch
    {
        LibraryResistanceLevel.Resist => LibraryResistanceLevel.Vulnerable,
        LibraryResistanceLevel.Endure => LibraryResistanceLevel.Fatal,
        _ => resistance
    };

    internal static bool IsBlackFlameRoundActive(
        CombatStateLike? combatState) =>
        combatState is { RoundNumber: > 0 }
        && combatState.RoundNumber % BlackFlameTurnInterval == 0
        && combatState.Players.Any(static player =>
            player.Relics
                .OfType<MagicBulletShooterPageRelic>()
                .Any(static relic =>
                    relic.Mode == MagicBulletShooterPageMode.BlackFlame));

    private async Task HandleCommissionDamage(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode != MagicBulletShooterPageMode.Commission
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target))
        {
            return;
        }

        bool isOwnCommissionTarget = HasOwnCommissionMark(target);
        if (isOwnCommissionTarget)
        {
            CommissionMarkedThisCombat = true;
        }

        if (!isOwnCommissionTarget
            && IsOwnerAttackSource(dealer, cardSource, props)
            && !CommissionMarkedThisCombat
            && CommissionKillsThisCombat == 0
            && !HasActiveCommissionTarget())
        {
            // 委托只锁定本场首次命中的敌人，死亡或标记消失后也不重新选取。
            CommissionMarkedThisCombat = true;
            isOwnCommissionTarget = true;
            Flash([target]);
            if (!result.WasTargetKilled && target.IsAlive)
            {
                PowerModel mutable = ModelDb
                    .Power<MagicBulletCommissionTargetPower>()
                    .ToMutable();
                await PowerCmdCompat.Apply(
                    choiceContext,
                    mutable,
                    target,
                    1m,
                    Owner.Creature,
                    cardSource);
            }
        }

        if (isOwnCommissionTarget
            && result.WasTargetKilled
            && target.IsDead
            && dealer == Owner.Creature)
        {
            _pendingCommissionKills.Add(target);
        }

        UpdateModeUiState();
    }

    private bool HasActiveCommissionTarget()
    {
        CombatStateLike? combatState = Owner.Creature.CombatState;
        return combatState?.Enemies.Any(HasOwnCommissionMark) == true;
    }

    private bool HasOwnCommissionMark(Creature target) =>
        target.Powers
            .OfType<MagicBulletCommissionTargetPower>()
            .Any(power => power.Applier == Owner.Creature);

    private bool IsOwnerAttackSource(
        Creature? dealer,
        CardModel? cardSource,
        ValueProp props)
    {
        return dealer == Owner.Creature
            && cardSource?.Owner == Owner
            && cardSource.Type == CardType.Attack
            && ValuePropCompat.IsCardOrMonsterMove(props);
    }

    private bool ShouldIncreaseBlackFlameDamageTaken(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer)
    {
        return Mode == MagicBulletShooterPageMode.BlackFlame
            && BlackFlameActiveThisRound
            && target == Owner.Creature
            && dealer != null
            && amount > 0m
            && ValuePropCompat.IsCardOrMonsterMove(props);
    }

    private async Task ApplyBlackFlamePower(Creature creature)
    {
        if (Mode != MagicBulletShooterPageMode.BlackFlame
            || creature.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(creature)
            || creature is not LibraryCreature libraryCreature)
        {
            return;
        }

        if (creature.GetPower<MagicBulletBlackFlameResistancePower>() == null)
        {
            await PowerCmdCompat.Apply<MagicBulletBlackFlameResistancePower>(
                creature,
                1m,
                Owner.Creature,
                null,
                silent: true);
        }

        RefreshBlackFlameResistanceDisplay(libraryCreature);
    }

    private async Task ApplyBlackFlameToAllEnemies()
    {
        if (Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        foreach (Creature enemy in combatState.Enemies.ToArray())
        {
            await ApplyBlackFlamePower(enemy);
        }
    }

    private async Task RemoveBlackFlameFromAllEnemies()
    {
        if (Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        MagicBulletBlackFlameResistancePower[] powers = combatState.Enemies
            .SelectMany(static enemy => enemy.Powers)
            .OfType<MagicBulletBlackFlameResistancePower>()
            .ToArray();
        foreach (MagicBulletBlackFlameResistancePower power in powers)
        {
            if (power.IsMutable)
            {
                await PowerCmd.Remove(power);
            }
        }

        foreach (LibraryCreature enemy in combatState.Enemies
            .OfType<LibraryCreature>())
        {
            RefreshBlackFlameResistanceDisplay(enemy);
        }
    }

    private static void RefreshBlackFlameResistanceDisplay(
        LibraryCreature creature)
    {
        // 刷新图标时保留混乱恢复后的基础抗性，避免把临时致命抗性写回。
        creature.SetPhysicalResistance(
            LibraryDamageType.Slash,
            creature.GetPostStunPhysicalResistanceLevel(LibraryDamageType.Slash));
        creature.SetPhysicalResistance(
            LibraryDamageType.Pierce,
            creature.GetPostStunPhysicalResistanceLevel(LibraryDamageType.Pierce));
        creature.SetPhysicalResistance(
            LibraryDamageType.Blunt,
            creature.GetPostStunPhysicalResistanceLevel(LibraryDamageType.Blunt));
        creature.SetChaoResistance(
            LibraryDamageType.Slash,
            creature.GetPostStunChaosResistanceLevel(LibraryDamageType.Slash));
        creature.SetChaoResistance(
            LibraryDamageType.Pierce,
            creature.GetPostStunChaosResistanceLevel(LibraryDamageType.Pierce));
        creature.SetChaoResistance(
            LibraryDamageType.Blunt,
            creature.GetPostStunChaosResistanceLevel(LibraryDamageType.Blunt));
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        Owner.RunState.CreateCard<MagicBulletCommissionChoiceCard>(Owner),
        Owner.RunState.CreateCard<MagicBulletSeventhBulletChoiceCard>(Owner),
        Owner.RunState.CreateCard<MagicBulletBlackFlameChoiceCard>(Owner)
    ];

    private void ResetTransientCombatState()
    {
        CommissionKillsThisCombat = 0;
        CommissionMarkedThisCombat = false;
        AttackCardProgressThisCombat = 0;
        BlackFlameRoundProgressThisCombat = 0;
        BlackFlameActiveThisRound = false;
        _pendingCommissionKills.Clear();
        _seventhAttackCard = null;
    }

    protected override void ResetStateOnModeSet(MagicBulletShooterPageMode mode) => ResetTransientCombatState();

    protected override void ResetStateOnFallback() => ResetTransientCombatState();

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            MagicBulletShooterPageMode.Commission
                when CombatManager.Instance.IsInProgress
                    && (HasActiveCommissionTarget()
                        || CommissionKillsThisCombat > 0) =>
                RelicStatus.Active,
            MagicBulletShooterPageMode.SeventhBullet
                when CombatManager.Instance.IsInProgress
                    && (AttackCardProgressThisCombat == SeventhBulletAttackInterval - 1
                        || _seventhAttackCard != null) =>
                RelicStatus.Active,
            MagicBulletShooterPageMode.BlackFlame
                when CombatManager.Instance.IsInProgress
                    && BlackFlameActiveThisRound =>
                RelicStatus.Active,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }
}

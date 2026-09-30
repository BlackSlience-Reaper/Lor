using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Literature;

public sealed class BlackSwanDreamPageRelic : ModalPageRelic<BlackSwanDreamPageMode>
{
    internal const int FilthDebuffMultiplier = 3;
    internal const int BrokenUmbrellaTurnInterval = 4;
    internal const int BrokenUmbrellaDamageReductionPercent = 75;
    internal const int BrokenUmbrellaBlockMultiplier = 4;
    internal const int DearFamilyTurnInterval = 2;
    internal const int DearFamilySlippery = 1;

    private const decimal BrokenUmbrellaDamageMultiplier =
        (100m - BrokenUmbrellaDamageReductionPercent) / 100m;
    private const string LiteratureFloorIconPath =
        "res://images/ui/run_history/literature_floor_liberation_encounter.png";
    private const string LiteratureFloorIconOutlinePath =
        "res://images/ui/run_history/literature_floor_liberation_encounter_outline.png";

    private PowerModel? _filthPowerBeingModified;
    private PowerModel? _filthPowerAwaitingCommit;
    private bool _dearFamilyTriggerTurn;
    private bool _dearFamilySlipperyCandidate;
    private bool _dearFamilySlipperyShouldConsume;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override string PackedIconPath => LiteratureFloorIconPath;

    protected override string PackedIconOutlinePath =>
        LiteratureFloorIconOutlinePath;

    protected override string BigIconPath => LiteratureFloorIconPath;

    public override bool ShowCounter => Mode is
        BlackSwanDreamPageMode.BrokenUmbrella
        or BlackSwanDreamPageMode.DearFamily;

    public override int DisplayAmount => Mode switch
    {
        BlackSwanDreamPageMode.BrokenUmbrella =>
            BrokenUmbrellaTriggerTurn
                ? BrokenUmbrellaTurnInterval
                : BrokenUmbrellaTurnsSeenAcrossCombats,
        BlackSwanDreamPageMode.DearFamily =>
            _dearFamilyTriggerTurn
                ? DearFamilyTurnInterval
                : DearFamilyTurnsSeenAcrossCombats,
        _ => 0
    };

    public override bool IsAllowed(IRunState runState)
    {
        _ = runState;
        return false;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)BlackSwanDreamPageMode.None),
        new DynamicVar("DebuffMultiplier", FilthDebuffMultiplier),
        new DynamicVar("TurnInterval", BrokenUmbrellaTurnInterval),
        new DynamicVar(
            "DamageReductionPercent",
            BrokenUmbrellaDamageReductionPercent),
        new DynamicVar("BlockMultiplier", BrokenUmbrellaBlockMultiplier),
        new DynamicVar("DearFamilyTurnInterval", DearFamilyTurnInterval),
        new PowerVar<SlipperyPower>("Slippery", DearFamilySlippery)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        BlackSwanDreamPageMode.BrokenUmbrella =>
        [
            HoverTipFactory.Static(StaticHoverTip.Block)
        ],
        BlackSwanDreamPageMode.DearFamily =>
        [
            HoverTipFactory.FromPower<SlipperyPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public BlackSwanDreamPageMode Mode { get; private set; }

    protected override BlackSwanDreamPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshIconOnModeChange => false;

    protected override bool RefreshUiBeforeModeChoice => true;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FilthUsedThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BrokenUmbrellaTurnsSeenAcrossCombats { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BrokenUmbrellaTriggerTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DearFamilyTurnsSeenAcrossCombats { get; private set; }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _filthPowerBeingModified = null;
        _filthPowerAwaitingCommit = null;
        _dearFamilyTriggerTurn = false;
        _dearFamilySlipperyCandidate = false;
        _dearFamilySlipperyShouldConsume = false;
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        FilthUsedThisTurn = false;
        BrokenUmbrellaTriggerTurn = false;
        _dearFamilyTriggerTurn = false;
        _dearFamilySlipperyCandidate = false;
        _dearFamilySlipperyShouldConsume = false;
        ClearFilthPowerTracking();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        _ = combatState;
        if (Owner.Creature == null
            || side != Owner.Creature.Side
            || !Owner.Creature.IsAlive
            || !participants.Contains(Owner.Creature))
        {
            return;
        }

        if (Mode == BlackSwanDreamPageMode.Filth)
        {
            FilthUsedThisTurn = false;
            ClearFilthPowerTracking();
            UpdateModeUiState();
            return;
        }

        if (Mode == BlackSwanDreamPageMode.BrokenUmbrella)
        {
            BrokenUmbrellaTriggerTurn =
                BrokenUmbrellaTurnsSeenAcrossCombats
                >= BrokenUmbrellaTurnInterval - 1;
            BrokenUmbrellaTurnsSeenAcrossCombats =
                BrokenUmbrellaTriggerTurn
                    ? 0
                    : BrokenUmbrellaTurnsSeenAcrossCombats + 1;
            if (BrokenUmbrellaTriggerTurn)
            {
                Flash();
            }

            UpdateModeUiState();
            return;
        }

        if (Mode == BlackSwanDreamPageMode.DearFamily)
        {
            _dearFamilyTriggerTurn =
                DearFamilyTurnsSeenAcrossCombats
                >= DearFamilyTurnInterval - 1;
            DearFamilyTurnsSeenAcrossCombats = _dearFamilyTriggerTurn
                ? 0
                : DearFamilyTurnsSeenAcrossCombats + 1;
            if (_dearFamilyTriggerTurn
                && Owner.Creature.GetPowerAmount<SlipperyPower>() <= 0)
            {
                Flash();
                await PowerCmdCompat.Apply<SlipperyPower>(
                    choiceContext,
                    Owner.Creature,
                    DearFamilySlippery,
                    Owner.Creature,
                    null);
            }
        }

        UpdateModeUiState();
    }

    public override decimal ModifyHpLostBeforeOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        _ = props;
        _ = dealer;
        _ = cardSource;
        if (target == Owner.Creature)
        {
            _dearFamilySlipperyShouldConsume = false;
        }

        _dearFamilySlipperyCandidate =
            Mode == BlackSwanDreamPageMode.DearFamily
            && target == Owner.Creature
            && amount >= 1m
            && target.GetPowerAmount<SlipperyPower>() > 0;
        return amount;
    }

    public override decimal ModifyHpLostAfterOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        _ = props;
        _ = dealer;
        _ = cardSource;
        if (_dearFamilySlipperyCandidate && target == Owner.Creature)
        {
            _dearFamilySlipperyShouldConsume = true;
        }

        _dearFamilySlipperyCandidate = false;
        return amount;
    }

    public override async Task AfterDamageReceivedLate(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        _ = props;
        _ = dealer;
        _ = cardSource;
        if (target != Owner.Creature)
        {
            return;
        }

        bool shouldConsume = _dearFamilySlipperyShouldConsume;
        _dearFamilySlipperyShouldConsume = false;
        if (!shouldConsume
            || result.UnblockedDamage >= 1
            || target.GetPower<SlipperyPower>() is not { } slippery)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.ModifyAmount(
            choiceContext,
            slippery,
            -1m,
            null,
            null);
    }

    public override Task BeforePowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature target,
        Creature? applier,
        CardModel? cardSource)
    {
        _filthPowerAwaitingCommit = null;
        if (_filthPowerBeingModified == null
            && ShouldTripleDebuff(
                power,
                applier,
                amount,
                target,
                cardSource))
        {
            _filthPowerBeingModified = power;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyPowerAmountGivenMultiplicative(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature? target,
        CardModel? cardSource)
    {
        if (_filthPowerBeingModified != null)
        {
            return ReferenceEquals(_filthPowerBeingModified, power)
                ? FilthDebuffMultiplier
                : 1m;
        }

        return ShouldTripleDebuff(
            power,
            giver,
            amount,
            target,
            cardSource)
                ? FilthDebuffMultiplier
                : 1m;
    }

    public override Task AfterModifyingPowerAmountGiven(PowerModel power)
    {
        if (ReferenceEquals(_filthPowerBeingModified, power))
        {
            _filthPowerBeingModified = null;
            _filthPowerAwaitingCommit = power;
        }

        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        _ = choiceContext;
        _ = applier;
        _ = cardSource;
        if (amount == 0m
            || !ReferenceEquals(_filthPowerAwaitingCommit, power))
        {
            return Task.CompletedTask;
        }

        _filthPowerAwaitingCommit = null;
        FilthUsedThisTurn = true;
        Flash();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        _ = amount;
        _ = cardSource;
        _ = cardPlay;
        return Mode == BlackSwanDreamPageMode.BrokenUmbrella
            && BrokenUmbrellaTriggerTurn
            && target == Owner.Creature
            && dealer != null
            && dealer.Side != Owner.Creature.Side
            && ValuePropCompat.IsPoweredAttack(props)
                ? BrokenUmbrellaDamageMultiplier
                : 1m;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        _ = cardSource;
        if (Mode != BlackSwanDreamPageMode.BrokenUmbrella
            || !BrokenUmbrellaTriggerTurn
            || target != Owner.Creature
            || dealer == null
            || !dealer.IsAlive
            || dealer.Side == target.Side
            || AllyTurnRegistry.IsFriendlyAlly(dealer)
            || result.TotalDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        int damage = Math.Max(
            0,
            target.Block * BrokenUmbrellaBlockMultiplier);
        if (damage <= 0)
        {
            return;
        }

        Flash([dealer]);
        await LibraryCreatureCmd.ChaoDamage(
            choiceContext,
            dealer,
            damage,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature,
            null);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _ = room;
        FilthUsedThisTurn = false;
        BrokenUmbrellaTriggerTurn = false;
        _dearFamilyTriggerTurn = false;
        _dearFamilySlipperyCandidate = false;
        _dearFamilySlipperyShouldConsume = false;
        ClearFilthPowerTracking();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private bool ShouldTripleDebuff(
        PowerModel power,
        Creature? giver,
        decimal amount,
        Creature? target,
        CardModel? cardSource)
    {
        _ = cardSource;
        return Mode == BlackSwanDreamPageMode.Filth
            && !FilthUsedThisTurn
            && amount != 0m
            && giver == Owner.Creature
            && target != null
            && target.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target)
            && power.IsVisible
            && power.GetTypeForAmount(amount) == PowerType.Debuff
            && !target.HasPower<ArtifactPower>();
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        Owner.RunState.CreateCard<BlackSwanFilthChoiceCard>(Owner),
        Owner.RunState.CreateCard<BlackSwanBrokenUmbrellaChoiceCard>(Owner),
        Owner.RunState.CreateCard<BlackSwanDearFamilyChoiceCard>(Owner)
    ];

    private void ClearFilthPowerTracking()
    {
        _filthPowerBeingModified = null;
        _filthPowerAwaitingCommit = null;
    }

    protected override void ResetStateOnModeSet(BlackSwanDreamPageMode mode)
    {
        FilthUsedThisTurn = false;
        BrokenUmbrellaTurnsSeenAcrossCombats = 0;
        BrokenUmbrellaTriggerTurn = false;
        DearFamilyTurnsSeenAcrossCombats = 0;
        _dearFamilyTriggerTurn = false;
        _dearFamilySlipperyCandidate = false;
        _dearFamilySlipperyShouldConsume = false;
        ClearFilthPowerTracking();
    }

    protected override void ResetStateOnFallback() => ResetStateOnModeSet(FallbackMode);

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            BlackSwanDreamPageMode.Filth
                when CombatManager.Instance.IsInProgress
                    && !FilthUsedThisTurn => RelicStatus.Active,
            BlackSwanDreamPageMode.BrokenUmbrella
                when CombatManager.Instance.IsInProgress
                    && BrokenUmbrellaTriggerTurn => RelicStatus.Active,
            BlackSwanDreamPageMode.DearFamily
                when CombatManager.Instance.IsInProgress
                    && _dearFamilyTriggerTurn => RelicStatus.Active,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }
}

public enum BlackSwanDreamPageMode
{
    None = 0,
    Filth = 1,
    BrokenUmbrella = 2,
    DearFamily = 3
}

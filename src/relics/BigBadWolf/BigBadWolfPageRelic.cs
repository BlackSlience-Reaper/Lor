using System;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.BigBadWolf;
using LibraryOfRuina.compat;
using LibraryOfRuina.powers.BigBadWolf;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.BigBadWolf;

public sealed class BigBadWolfPageRelic : RelicModel
{
    internal const int PredatoryInstinctTurnInterval = 8;
    internal const int PredatoryInstinctHeal = 3;
    internal const decimal PredatoryInstinctDamageMultiplier = 2m;
    // 狼的角色：战斗开始时获得的永久强壮层数。
    internal const int WolfRoleStrong = 2;

    // 狼的角色：每次造成未被格挡伤害时施加的永久易损层数。
    internal const int WolfRoleVulnerable = 1;

    // 狼的角色：每次造成未被格挡伤害时施加的永久混乱易伤层数。
    internal const int WolfRoleBreakVulnerable = 1;

    // 狼的角色：强壮持续整场战斗。
    private const int WolfRoleStrongTurns = -1;

    // 狼的角色：易损与混乱易伤持续整场战斗。
    private const int WolfRoleDebuffTurns = -1;

    // 狼的角色：每场战斗施加易损与混乱易伤的最大触发次数。
    internal const int WolfRoleMaxTriggers = 2;
    internal const int CruelClawsHpThresholdPercent = 25;
    internal const int CruelClawsStrong = 4;
    internal const int CruelClawsBleed = 4;
    internal const int CruelClawsTurns = 1;

    protected override string IconBaseName => "big_bad_wolf_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<BigBadWolfPageRelic>(runState);

    public override bool ShowCounter =>
        Mode == BigBadWolfPageMode.PredatoryInstinct
        || (CombatManager.Instance.IsInProgress && Mode == BigBadWolfPageMode.WolfRole);

    public override int DisplayAmount => Mode switch
    {
        BigBadWolfPageMode.PredatoryInstinct => GetPredatoryInstinctDisplayAmount(),
        BigBadWolfPageMode.WolfRole => Math.Max(0, WolfRoleMaxTriggers - WolfRoleTriggersThisCombat),
        // BigBadWolfPageMode.CruelClaws => Math.Max(0, GetCruelClawsHpThreshold() - HpLossRemainder),
        _ => 0
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)BigBadWolfPageMode.None),
        new DynamicVar("TurnInterval", PredatoryInstinctTurnInterval),
        new DynamicVar("DamageMultiplier", PredatoryInstinctDamageMultiplier),
        new HealVar("Heal", PredatoryInstinctHeal),
        new DynamicVar("WolfRoleStrong", WolfRoleStrong),
        new DynamicVar("Vulnerable", WolfRoleVulnerable),
        new DynamicVar("BreakVulnerable", WolfRoleBreakVulnerable),
        new DynamicVar("MaxTriggers", WolfRoleMaxTriggers),
        new DynamicVar("HpThresholdPercent", CruelClawsHpThresholdPercent),
        new DynamicVar("Turns", CruelClawsTurns),
        new DynamicVar("Strong", CruelClawsStrong),
        new DynamicVar("Bleed", CruelClawsBleed)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        BigBadWolfPageMode.PredatoryInstinct =>
        [
            HoverTipFactory.FromPower<DoubleDamagePower>()
        ],
        BigBadWolfPageMode.WolfRole =>
        [
            HoverTipFactory.FromPower<LibraryStrongPower>(),
            HoverTipFactory.FromPower<LibraryVulnerablePower>(),
            HoverTipFactory.FromPower<LibraryBreakVulnerablePower>()
        ],
        BigBadWolfPageMode.CruelClaws =>
        [
            HoverTipFactory.FromPower<LibraryStrongPower>(),
            HoverTipFactory.FromPower<LibraryBleedingPower>(),
            HoverTipFactory.FromPower<BigBadWolfUntargetablePower>()
        ],
        _ => []
    };

    [SavedProperty]
    public BigBadWolfPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PredatoryInstinctTurnsSeen { get; private set; }

    public int WolfRoleTriggersThisCombat { get; private set; }

    public int HpLossRemainder { get; private set; }

    public int PendingCruelClawsProcs { get; private set; }

    private bool _predatoryTriggerTurn;
    private bool _predatoryUnblockedDamageThisTurn;

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != BigBadWolfPageMode.None)
        {
            UpdateModeUiState();
            RefreshInventoryIcon();
            return;
        }

        IReadOnlyList<CardModel> options = CreateModeChoiceCards();
        CardModel? chosenCard = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            options,
            Owner,
            canSkip: true);

        if (chosenCard == null)
        {
            await AbnormalityPageRewardHelper.SkipObtainedPageRelic(this, nameof(AfterObtained));
            return;
        }

        SetMode(ResolveModeFromChoiceCard(chosenCard));
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        RefreshInventoryIcon();
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetTransientCombatState();

        if (Mode == BigBadWolfPageMode.WolfRole && Owner.Creature != null)
        {
            Flash();
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                Owner.Creature,
                WolfRoleStrong,
                turns: WolfRoleStrongTurns,
                Owner.Creature,
                null,
                silent: true);
        }

        UpdateModeUiState();
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return;
        }

        if (Mode == BigBadWolfPageMode.PredatoryInstinct)
        {
            bool shouldTrigger = PredatoryInstinctTurnsSeen >= PredatoryInstinctTurnInterval - 1;
            PredatoryInstinctTurnsSeen = shouldTrigger ? 0 : PredatoryInstinctTurnsSeen + 1;
            _predatoryTriggerTurn = shouldTrigger;
            _predatoryUnblockedDamageThisTurn = false;

            if (shouldTrigger && Owner.Creature is { IsAlive: true })
            {
                Flash();
                await PowerCmdCompat.Apply<DoubleDamagePower>(
                    Owner.Creature,
                    1m,
                    Owner.Creature,
                    null,
                    silent: true);
            }

            UpdateModeUiState();
        }

        if (Mode == BigBadWolfPageMode.CruelClaws
            && PendingCruelClawsProcs > 0
            && Owner.Creature is { IsAlive: true })
        {
            //int procs = PendingCruelClawsProcs;
            PendingCruelClawsProcs = 0;
            Flash();
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                Owner.Creature,
                CruelClawsStrong,
                turns: 0,
                Owner.Creature,
                null,
                silent: true);
            await PowerCmdCompat.Apply<BigBadWolfCruelClawsPower>(
                Owner.Creature,
                CruelClawsBleed,
                Owner.Creature,
                null,
                silent: true);
            await PowerCmdCompat.Apply<BigBadWolfUntargetablePower>(
                Owner.Creature,
                1m,
                Owner.Creature,
                null,
                silent: true);
            UpdateModeUiState();
        }
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Owner?.Creature == null
            || result.UnblockedDamage <= 0m
            || !IsOwnerDamageSource(dealer)
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target))
        {
            return;
        }

        if (Mode == BigBadWolfPageMode.PredatoryInstinct && _predatoryTriggerTurn)
        {
            _predatoryUnblockedDamageThisTurn = true;
        }

        if (Mode == BigBadWolfPageMode.WolfRole
            && WolfRoleTriggersThisCombat < WolfRoleMaxTriggers)
        {
            WolfRoleTriggersThisCombat++;
            Flash([target]);
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target,
                WolfRoleVulnerable,
                turns: WolfRoleDebuffTurns,
                Owner.Creature,
                cardSource,
                silent: true);
            await LibraryPowerCmd.Apply<LibraryBreakVulnerablePower>(
                target,
                WolfRoleBreakVulnerable,
                turns: WolfRoleDebuffTurns,
                Owner.Creature,
                cardSource,
                silent: true);
            UpdateModeUiState();
        }
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (Mode != BigBadWolfPageMode.CruelClaws
            || creature != Owner?.Creature
            || delta >= 0m)
        {
            return Task.CompletedTask;
        }

        int hpLost = (int)(-delta);
        if (hpLost <= 0)
        {
            return Task.CompletedTask;
        }

        HpLossRemainder += hpLost;
        int threshold = GetCruelClawsHpThreshold();
        if (HpLossRemainder >= threshold)
        {
            int triggers = HpLossRemainder / threshold;
            HpLossRemainder -= triggers * threshold;
            PendingCruelClawsProcs += triggers;
            Flash();
        }

        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Mode == BigBadWolfPageMode.PredatoryInstinct
            && _predatoryTriggerTurn
            && Owner.Creature is { IsAlive: true }
            && side == Owner.Creature.Side
            && _predatoryUnblockedDamageThisTurn)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, PredatoryInstinctHeal);
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTransientCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<BigBadWolfPredatoryInstinctChoiceCard>(Owner),
            Owner.RunState.CreateCard<BigBadWolfWolfRoleChoiceCard>(Owner),
            Owner.RunState.CreateCard<BigBadWolfCruelClawsChoiceCard>(Owner)
        ];
    }

    private static BigBadWolfPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            BigBadWolfPredatoryInstinctChoiceCard => BigBadWolfPageMode.PredatoryInstinct,
            BigBadWolfWolfRoleChoiceCard => BigBadWolfPageMode.WolfRole,
            BigBadWolfCruelClawsChoiceCard => BigBadWolfPageMode.CruelClaws,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(BigBadWolfPageMode mode)
    {
        return mode is BigBadWolfPageMode.None
            or BigBadWolfPageMode.PredatoryInstinct
            or BigBadWolfPageMode.WolfRole
            or BigBadWolfPageMode.CruelClaws;
    }

    private static bool IsConcreteMode(BigBadWolfPageMode mode)
    {
        return mode is BigBadWolfPageMode.PredatoryInstinct
            or BigBadWolfPageMode.WolfRole
            or BigBadWolfPageMode.CruelClaws;
    }

    private void SetMode(BigBadWolfPageMode mode)
    {
        Mode = mode;
        ResetTransientCombatState();
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void EnsureValidModeOrFallback(string context)
    {
        if (IsConcreteMode(Mode))
        {
            return;
        }

        FallbackToDefaultModeAfterLoad(context);
    }

    private void FallbackToDefaultModeAfterLoad(string context)
    {
        BigBadWolfPageMode oldMode = Mode;
        Mode = BigBadWolfPageMode.PredatoryInstinct;
        ResetTransientCombatState();
        Log.Warn("[LibraryOfRuina.PageRelic] BigBadWolfPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to PredatoryInstinct.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void ResetTransientCombatState()
    {
        WolfRoleTriggersThisCombat = 0;
        HpLossRemainder = 0;
        PendingCruelClawsProcs = 0;
        _predatoryTriggerTurn = false;
        _predatoryUnblockedDamageThisTurn = false;
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            BigBadWolfPageMode.PredatoryInstinct when CombatManager.Instance.IsInProgress =>
                _predatoryTriggerTurn ? RelicStatus.Active : RelicStatus.Normal,
            BigBadWolfPageMode.WolfRole when CombatManager.Instance.IsInProgress =>
                WolfRoleTriggersThisCombat < WolfRoleMaxTriggers ? RelicStatus.Active : RelicStatus.Disabled,
            BigBadWolfPageMode.CruelClaws when CombatManager.Instance.IsInProgress =>
                PendingCruelClawsProcs > 0 ? RelicStatus.Active : RelicStatus.Normal,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }

    private int GetPredatoryInstinctDisplayAmount() =>
        _predatoryTriggerTurn ? PredatoryInstinctTurnInterval : PredatoryInstinctTurnsSeen;

    private bool IsOwnerDamageSource(Creature? dealer)
    {
        if (dealer == null)
        {
            return false;
        }

        return dealer == Owner.Creature || dealer == Owner.Osty;
    }

    private int GetCruelClawsHpThreshold()
    {
        if (Owner?.Creature == null)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling(Owner.Creature.MaxHp * CruelClawsHpThresholdPercent / 100m));
    }

    private void RefreshInventoryIcon()
    {
        NRelicInventory? inventory = NRun.Instance?.GlobalUi?.RelicInventory;
        if (inventory == null)
        {
            return;
        }

        foreach (NRelicInventoryHolder holder in inventory.RelicNodes)
        {
            if (!ReferenceEquals(holder.Relic.Model, this))
            {
                continue;
            }

            holder.Relic.Icon.Texture = Icon;
            holder.Relic.Outline.Texture = IconOutline;
            break;
        }
    }
}

public enum BigBadWolfPageMode
{
    None = 0,
    PredatoryInstinct = 1,
    WolfRole = 2,
    CruelClaws = 3
}

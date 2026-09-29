using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.KingOfGreed;

public enum KingOfGreedPageMode
{
    None = 0,
    Indulgence = 1,
    HappinessPath = 2,
    Greed = 3
}

public sealed class KingOfGreedPageRelic : ModalPageRelic<KingOfGreedPageMode>
{
    internal const int IndulgenceRequiredTurns = 6;
    internal const int HappinessPathMaxEndurance = 6;
    internal const int HappinessPathTurns = 1;
    internal const int GreedMaxHpLossPercent = 30;
    internal const int GreedLifestealPercent = 30;

    protected override string IconBaseName => "king_of_greed_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<KingOfGreedPageRelic>(runState);

    public override bool ShowCounter =>
        Mode == KingOfGreedPageMode.Indulgence
        || (CombatManager.Instance.IsInProgress && Mode == KingOfGreedPageMode.HappinessPath);

    public override int DisplayAmount => Mode switch
    {
        KingOfGreedPageMode.Indulgence =>
            StunNextAttackerPending ? IndulgenceRequiredTurns : FullyBlockedTurnCount,
        KingOfGreedPageMode.HappinessPath => HappinessPathEnduranceThisTurn,
        _ => 0
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)KingOfGreedPageMode.None),
        new DynamicVar("BlockTurns", IndulgenceRequiredTurns),
        new DynamicVar("MaxEndurance", HappinessPathMaxEndurance),
        new DynamicVar("Turns", HappinessPathTurns),
        new DynamicVar("MaxHpPercent", GreedMaxHpLossPercent),
        new DynamicVar("LifestealPercent", GreedLifestealPercent)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        KingOfGreedPageMode.Indulgence => [HoverTipFactory.Static(StaticHoverTip.Stun)],
        KingOfGreedPageMode.HappinessPath => [HoverTipFactory.FromPower<LibraryEndurancePower>()],
        _ => []
    };

    [SavedProperty]
    public KingOfGreedPageMode Mode { get; private set; }

    protected override KingOfGreedPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FullyBlockedTurnCount { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool StunNextAttackerPending { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HappinessPathEnduranceThisTurn { get; private set; }

    private List<Creature> _blockedAttackersThisEnemyTurn = [];
    private List<Creature> _pendingStunAttackers = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _blockedAttackersThisEnemyTurn = [.. _blockedAttackersThisEnemyTurn];
        _pendingStunAttackers = [.. _pendingStunAttackers];
    }

    private bool _receivedAttackThisEnemyTurn;
    private bool _receivedUnblockedAttackThisEnemyTurn;

    protected override Task ApplyObtainedChoiceAsync(KingOfGreedPageMode mode) => SetModeAsync(mode);

    // 预选只写模式、通知图标变化并刷新界面；清状态与拾取效果由获得后作为 AbnormalityPagePostObtainEffect 执行的 SetModeAsync 完成。
    protected override void ApplyPreselectedMode(KingOfGreedPageMode mode) => AssignPreselectedModeOnly(mode);

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetTransientCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == Owner.Creature.Side)
        {
            HappinessPathEnduranceThisTurn = 0;
            UpdateModeUiState();
        }
        else
        {
            _blockedAttackersThisEnemyTurn.Clear();
            _receivedAttackThisEnemyTurn = false;
            _receivedUnblockedAttackThisEnemyTurn = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != Owner.Creature.Side || !StunNextAttackerPending)
        {
            return;
        }

        IReadOnlyList<Creature> attackers = _pendingStunAttackers
            .Where(static attacker => attacker.IsAlive)
            .Distinct()
            .ToArray();
        _pendingStunAttackers.Clear();

        if (Mode != KingOfGreedPageMode.Indulgence)
        {
            StunNextAttackerPending = false;
            UpdateModeUiState();
            return;
        }

        if (attackers.Count == 0)
        {
            Flash();
            StunNextAttackerPending = false;
            UpdateModeUiState();
            return;
        }

        Flash(attackers);
        foreach (Creature attacker in attackers)
        {
            await CreatureCmd.Stun(attacker);
        }

        StunNextAttackerPending = false;
        UpdateModeUiState();
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Mode != KingOfGreedPageMode.Indulgence || side == Owner.Creature.Side)
        {
            return Task.CompletedTask;
        }

        if (_receivedAttackThisEnemyTurn && !_receivedUnblockedAttackThisEnemyTurn)
        {
            FullyBlockedTurnCount++;
            if (FullyBlockedTurnCount >= IndulgenceRequiredTurns)
            {
                FullyBlockedTurnCount = 0;
                _pendingStunAttackers.Clear();
                _pendingStunAttackers.AddRange(_blockedAttackersThisEnemyTurn
                    .Where(static attacker => attacker.IsAlive)
                    .Distinct()
                    .ToArray());
                StunNextAttackerPending = _pendingStunAttackers.Count > 0;
                if (!StunNextAttackerPending)
                {
                    Flash();
                }
            }
            else
            {
                Flash();
            }
        }

        _blockedAttackersThisEnemyTurn.Clear();
        _receivedAttackThisEnemyTurn = false;
        _receivedUnblockedAttackThisEnemyTurn = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner.Creature || dealer == null || dealer.Side == Owner.Creature.Side)
        {
            return;
        }

        if (Mode == KingOfGreedPageMode.Indulgence && ValuePropCompat.IsPoweredAttack(props))
        {
            _receivedAttackThisEnemyTurn = true;
            if (result.UnblockedDamage > 0m)
            {
                _receivedUnblockedAttackThisEnemyTurn = true;
            }
            else if (!_blockedAttackersThisEnemyTurn.Contains(dealer))
            {
                _blockedAttackersThisEnemyTurn.Add(dealer);
            }
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
        if (Mode == KingOfGreedPageMode.HappinessPath
            && dealer == Owner.Creature
            && result.UnblockedDamage > 0m
            && HappinessPathEnduranceThisTurn < HappinessPathMaxEndurance)
        {
            HappinessPathEnduranceThisTurn++;
            Flash();
            var Ep = Owner.Creature.GetPowerInstances<LibraryEndurancePower>()
            .FirstOrDefault(static p => p.TurnsRemaining > 0);
            if (Ep == null)
            {
                await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                    new ThrowingPlayerChoiceContext(),
                    Owner.Creature,
                    1,
                    HappinessPathTurns - 1,
                    IsPermanent: false,
                    Owner.Creature,
                    cardSource,
                    silent: true);
                InvokeDisplayAmountChanged();
            }
            else
            {
                await LibraryPowerCmd.ModifyAmount(
                    new ThrowingPlayerChoiceContext(),
                    Ep,
                    1,
                    0,
                    IsPermanent: false,
                    Owner.Creature,
                    null);
                InvokeDisplayAmountChanged();
            }
            
        }

        if (Mode == KingOfGreedPageMode.Greed
            && dealer == Owner.Creature
            && result.UnblockedDamage > 0m
            && ValuePropCompat.IsPoweredAttack(props)
            && Owner.Creature.IsAlive)
        {
            int heal = (int)Math.Floor(result.UnblockedDamage * GreedLifestealPercent / 100m);
            if (heal > 0)
            {
                await CreatureCmd.Heal(Owner.Creature, heal);
            }
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTransientCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        Owner.RunState.CreateCard<KingOfGreedIndulgenceChoiceCard>(Owner),
        Owner.RunState.CreateCard<KingOfGreedHappinessPathChoiceCard>(Owner),
        Owner.RunState.CreateCard<KingOfGreedGreedChoiceCard>(Owner)
    ];

    [AbnormalityPagePostObtainEffect]
    private async Task SetModeAsync(KingOfGreedPageMode mode)
    {
        Mode = mode;
        ResetTransientCombatState();
        if (mode == KingOfGreedPageMode.Greed && Owner.Creature != null)
        {
            decimal targetMax = Math.Max(1m, Owner.Creature.MaxHp * (100 - GreedMaxHpLossPercent) / 100m);
            await CreatureCmd.SetMaxHp(Owner.Creature, targetMax);
            if (Owner.Creature.CurrentHp > targetMax)
            {
                await CreatureCmd.SetCurrentHp(Owner.Creature, targetMax);
            }
        }

        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void ResetTransientCombatState()
    {
        StunNextAttackerPending = false;
        HappinessPathEnduranceThisTurn = 0;
        _blockedAttackersThisEnemyTurn.Clear();
        _pendingStunAttackers.Clear();
        _receivedAttackThisEnemyTurn = false;
        _receivedUnblockedAttackThisEnemyTurn = false;
    }

    // 读档时 None 保持不动，只有越界值才回退，而且回退到 None（其他书页回退到第一个模式）。
    protected override void EnsureValidModeOrFallback(string context)
    {
        if (IsKnownMode(Mode) && Mode != KingOfGreedPageMode.None)
        {
            return;
        }

        if (Mode == KingOfGreedPageMode.None)
        {
            return;
        }

        FallbackToDefaultModeAfterLoad(context);
    }

    protected override void FallbackToDefaultModeAfterLoad(string context)
    {
        Log.Warn("[LibraryOfRuina.PageRelic] KingOfGreedPageRelic invalid mode during " + context + "; resetting to None.");
        Mode = KingOfGreedPageMode.None;
        ResetTransientCombatState();
        UpdateModeUiState();
    }

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == KingOfGreedPageMode.Indulgence
            && CombatManager.Instance.IsInProgress
            && StunNextAttackerPending
                ? RelicStatus.Active
                : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }
}

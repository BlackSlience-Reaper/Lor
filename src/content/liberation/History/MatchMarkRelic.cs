using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.History;

public sealed class MatchMarkRelic : ModalPageRelic<MatchMarkMode>
{
    internal const int EmberBurnStacks = 12;
    internal const int EmberHpLossReduction = 1;
    internal const int FootstepsBurnStacks = 9;
    internal const int AfterglowSelectionMax = 2;

    private bool _isResolvingPendingDamage;
    private int _pendingFootstepsDamage;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _isResolvingPendingDamage = false;
        _pendingFootstepsDamage = 0;
    }

    protected override string IconBaseName => "match_mark_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<MatchMarkRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)MatchMarkMode.None),
        new DynamicVar("EmberBurn", EmberBurnStacks),
        new DynamicVar("PreventedHpLoss", EmberHpLossReduction),
        new DynamicVar("FootstepsBurn", FootstepsBurnStacks),
        new CardsVar(AfterglowSelectionMax)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>(),
        HoverTipFactory.FromPower<PreservedDamagePower>(),
        ..HoverTipFactory.FromEnchantment<MatchFlameEnchantment>()
    ];

    [SavedProperty]
    public MatchMarkMode Mode { get; private set; }

    protected override MatchMarkMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshIconOnModeChange => false;

    protected override bool RefreshUiBeforeModeChoice => true;

    // 预选写入模式时还会通知一次图标变化，获得时选择则不会；Afterglow 的附魔选择在获得后由 AbnormalityPagePostObtainEffect 执行。
    protected override void ApplyPreselectedMode(MatchMarkMode mode) => AssignPreselectedModeOnly(mode);

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FootstepsSpent { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FootstepsArmed { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingDamage { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ResolveAtNextPlayerTurnEnd { get; private set; }

    protected override async Task ApplyObtainedChoiceAsync(MatchMarkMode mode)
    {
        SetMode(mode);
        if (Mode == MatchMarkMode.Afterglow)
        {
            await ApplyAfterglowEnchantmentSelection();
        }
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        return PreservedDamagePower.SyncFor(Owner);
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetTransientCombatState();
        UpdateModeUiState();
        return PreservedDamagePower.SyncFor(Owner);
    }

    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (Mode == MatchMarkMode.Footsteps && player == Owner && !FootstepsArmed)
        {
            FootstepsArmed = true;
            UpdateModeUiState();
        }

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
        if (Mode != MatchMarkMode.Ember || target != Owner.Creature)
        {
            return;
        }

        if (result.UnblockedDamage <= 0 || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        if (dealer == null
            || !dealer.IsAlive
            || dealer.Side == target.Side
            || AllyTurnRegistry.IsFriendlyAlly(dealer))
        {
            return;
        }

        Flash([dealer]);
        await PowerCmdCompat.Apply<LibraryBurnPower>(dealer, EmberBurnStacks, Owner.Creature, cardSource);
    }

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        _pendingFootstepsDamage = 0;
        if (target != Owner?.Creature
            || amount <= 0m
            || !CombatManager.Instance.IsInProgress)
        {
            return amount;
        }

        if (Mode == MatchMarkMode.Ember)
        {
            return Math.Max(0m, amount - EmberHpLossReduction);
        }

        if (Mode != MatchMarkMode.Footsteps
            || _isResolvingPendingDamage
            || !FootstepsArmed
            || FootstepsSpent)
        {
            return amount;
        }

        _pendingFootstepsDamage = Math.Max(1, (int)Math.Ceiling(amount));
        return 0m;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        int preventedDamage = _pendingFootstepsDamage;
        _pendingFootstepsDamage = 0;
        if (preventedDamage <= 0)
        {
            return;
        }

        FootstepsSpent = true;
        PendingDamage += preventedDamage;
        ResolveAtNextPlayerTurnEnd = true;
        UpdateModeUiState();

        await ApplyFootstepsTriggerEffects();
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !ResolveAtNextPlayerTurnEnd || PendingDamage <= 0)
        {
            return;
        }

        int damageToResolve = PendingDamage;
        PendingDamage = 0;
        ResolveAtNextPlayerTurnEnd = false;
        UpdateModeUiState();

        _isResolvingPendingDamage = true;
        try
        {
            await PreservedDamagePower.SyncFor(Owner);

            await CreatureCmdCompat.Damage(
                choiceContext,
                Owner.Creature,
                damageToResolve,
                ValueProp.Unpowered,
                dealer: null,
                cardSource: null);
        }
        finally
        {
            _isResolvingPendingDamage = false;
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTransientCombatState();
        
        UpdateModeUiStateCore(forceNormalStatus: true);
        return PreservedDamagePower.SyncFor(Owner);
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<MatchMarkEmberChoiceCard>(Owner),
            Owner.RunState.CreateCard<MatchMarkFootstepsChoiceCard>(Owner),
            Owner.RunState.CreateCard<MatchMarkAfterglowChoiceCard>(Owner)
        ];
    }

    private void ResetTransientCombatState()
    {
        FootstepsSpent = false;
        FootstepsArmed = false;
        PendingDamage = 0;
        ResolveAtNextPlayerTurnEnd = false;
        _isResolvingPendingDamage = false;
        _pendingFootstepsDamage = 0;
    }

    protected override void ResetStateOnFallback() => ResetTransientCombatState();

    protected override void UpdateModeUiState()
    {
        UpdateModeUiStateCore(forceNormalStatus: false);
    }

    private void UpdateModeUiStateCore(bool forceNormalStatus)
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = !forceNormalStatus
            && CombatManager.Instance.IsInProgress
            && Mode == MatchMarkMode.Footsteps
            && (!FootstepsArmed || FootstepsSpent)
            ? RelicStatus.Disabled
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    [AbnormalityPagePostObtainEffect((int)MatchMarkMode.Afterglow)]
    private async Task ApplyAfterglowEnchantmentSelection()
    {
        EnchantmentModel enchantmentForSelection = ModelDb.Enchantment<MatchFlameEnchantment>();
        CardSelectorPrefs prefs = new(SelectionScreenPrompt, 0, AfterglowSelectionMax);

        IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckForEnchantment(
            Owner,
            enchantmentForSelection,
            amount: 1,
            additionalFilter: card => card?.Type == CardType.Attack,
            prefs: prefs);

        foreach (CardModel card in selectedCards)
        {
            CardCmd.Enchant<MatchFlameEnchantment>(card, 1);
            PlayEnchantVfx(card);
        }
    }

    private async Task ApplyFootstepsTriggerEffects()
    {
        if (CombatManager.Instance.IsInProgress)
        {
            IReadOnlyList<Creature> enemyList = AllyTurnRegistry
                .FilterPlayerEnemyTargets(Owner.Creature.CombatState?.Enemies);
            if (enemyList.Count > 0)
            {
                List<Creature> aliveEnemies = enemyList.Where(enemy => enemy.IsAlive).ToList();
                if (aliveEnemies.Count > 0)
                {
                    Flash(aliveEnemies);
                    await PowerCmdCompat.Apply<LibraryBurnPower>(aliveEnemies, FootstepsBurnStacks, Owner.Creature, null);
                }
            }
        }

        await PreservedDamagePower.SyncFor(Owner);
    }

    private static void PlayEnchantVfx(CardModel card)
    {
        NCardEnchantVfx? vfx = NCardEnchantVfx.Create(card);
        if (vfx != null)
        {
            NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
        }
    }
}

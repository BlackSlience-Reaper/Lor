using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.HistoryFloorLiberation;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.enchantments.HistoryFloorLiberation;
using LibraryOfRuina.interop;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.powers;
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
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.HistoryFloorLiberation;

public sealed class MatchMarkRelic : RelicModel
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

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FootstepsSpent { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool FootstepsArmed { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingDamage { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ResolveAtNextPlayerTurnEnd { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        UpdateModeUiState();
        if (Mode != MatchMarkMode.None)
        {
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

        Mode = ResolveModeFromChoiceCard(chosenCard);
        UpdateModeUiState();

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

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<MatchMarkEmberChoiceCard>(Owner),
            Owner.RunState.CreateCard<MatchMarkFootstepsChoiceCard>(Owner),
            Owner.RunState.CreateCard<MatchMarkAfterglowChoiceCard>(Owner)
        ];
    }

    private static MatchMarkMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            MatchMarkEmberChoiceCard => MatchMarkMode.Ember,
            MatchMarkFootstepsChoiceCard => MatchMarkMode.Footsteps,
            MatchMarkAfterglowChoiceCard => MatchMarkMode.Afterglow,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(MatchMarkMode mode)
    {
        return mode is MatchMarkMode.None
            or MatchMarkMode.Ember
            or MatchMarkMode.Footsteps
            or MatchMarkMode.Afterglow;
    }

    private static bool IsConcreteMode(MatchMarkMode mode)
    {
        return mode is MatchMarkMode.Ember
            or MatchMarkMode.Footsteps
            or MatchMarkMode.Afterglow;
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
        MatchMarkMode oldMode = Mode;
        Mode = MatchMarkMode.Ember;
        ResetTransientCombatState();
        Log.Warn("[LibraryOfRuina.PageRelic] MatchMarkRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Ember.");
        UpdateModeUiState();
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

    private void UpdateModeUiState()
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

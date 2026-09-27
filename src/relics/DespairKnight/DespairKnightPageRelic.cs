using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.DespairKnight;
using LibraryOfRuina.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.DespairKnight;

public sealed class DespairKnightPageRelic : LibraryRelicModel
{
    internal const int BlessingGuard = 9;
    internal const int BlessingTurns = 4;
    internal const int DespairStrength = 3;
    internal const int DespairGuard = 3;
    internal const int TearSwordEnchantCards = 2;
    internal const int TearSwordSharpAmount = 3;
    internal const int TearSwordMaxHpPercent = 3;
    private const decimal TearSwordMaxHpRatio = 0.03m;

    protected override string IconBaseName => "despair_knight_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<DespairKnightPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)DespairKnightPageMode.None),
        new DynamicVar("Guard", BlessingGuard),
        new DynamicVar("Turns", BlessingTurns),
        new DynamicVar("Strength", DespairStrength),
        new DynamicVar("PermanentGuard", DespairGuard),
        new CardsVar(TearSwordEnchantCards),
        new DynamicVar("SharpAmount", TearSwordSharpAmount),
        new DynamicVar("MaxHpPercent", TearSwordMaxHpPercent)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        DespairKnightPageMode.Blessing =>
        [
            HoverTipFactory.FromPower<LibraryEndurancePower>()
        ],
        DespairKnightPageMode.Despair =>
        [
            HoverTipFactory.FromPower<LibraryStrongPower>(),
            HoverTipFactory.FromPower<LibraryProtectionPower>()
        ],
        DespairKnightPageMode.TearSword =>
        [
            ..HoverTipFactory.FromEnchantment<Sharp>(TearSwordSharpAmount)
        ],
        _ => []
    };

    [SavedProperty]
    public DespairKnightPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BlessingTriggeredThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BlessingPendingNextPlayerTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool DespairTriggeredThisCombat { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != DespairKnightPageMode.None)
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

        await SetMode(ResolveModeFromChoiceCard(chosenCard));
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        RefreshInventoryIcon();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        BlessingTriggeredThisCombat = false;
        BlessingPendingNextPlayerTurn = false;
        DespairTriggeredThisCombat = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (Mode != DespairKnightPageMode.Blessing
            || side != Owner.Creature.Side
            || !BlessingPendingNextPlayerTurn
            || Owner.Creature == null
            || !Owner.Creature.IsAlive)
        {
            UpdateModeUiState();
            return;
        }

        BlessingPendingNextPlayerTurn = false;
        Flash();
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            BlessingGuard,
            BlessingTurns - 1,
            IsPermanent: false,
            Owner.Creature,
            null,
            silent: true);
        UpdateModeUiState();
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Mode != DespairKnightPageMode.Blessing
            || BlessingTriggeredThisCombat
            || target != Owner.Creature
            || result.UnblockedDamage <= 0m
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        BlessingTriggeredThisCombat = true;
        BlessingPendingNextPlayerTurn = true;
        Flash();
        UpdateModeUiState();
        await Task.CompletedTask;
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (Mode != DespairKnightPageMode.Despair
            || DespairTriggeredThisCombat
            || wasRemovalPrevented
            || creature.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(creature)
            || creature.IsPlayer)
        {
            return;
        }

        DespairTriggeredThisCombat = true;
        Flash();

        Creature ownerCreature = Owner.Creature;
        if (ownerCreature.IsAlive)
        {
            await PowerCmdCompat.Apply<LibraryStrongPower>(
                ownerCreature,
                DespairStrength,
                ownerCreature,
                null,
                silent: true);
            await LibraryPowerCmd.Apply<LibraryProtectionPower>(
                ownerCreature,
                DespairGuard,
                turns: -1,
                ownerCreature,
                null,
                silent: true);
        }

        UpdateModeUiState();
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode != DespairKnightPageMode.TearSword
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || target.IsDead
            || result.UnblockedDamage <= 0m
            || cardSource?.Enchantment is not Sharp
            || !IsOwnerAttackCardSource(dealer, cardSource, props))
        {
            return;
        }

        int extraDamage = Math.Max(1, (int)Math.Ceiling(target.MaxHp * TearSwordMaxHpRatio));
        Flash([target]);
        await CreatureCmdCompat.Damage(
            choiceContext,
            target,
            extraDamage,
            ValueProp.Unpowered,
            Owner.Creature,
            null);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        BlessingTriggeredThisCombat = false;
        BlessingPendingNextPlayerTurn = false;
        DespairTriggeredThisCombat = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<DespairKnightBlessingChoiceCard>(Owner),
            Owner.RunState.CreateCard<DespairKnightDespairChoiceCard>(Owner),
            Owner.RunState.CreateCard<DespairKnightTearSwordChoiceCard>(Owner)
        ];
    }

    private static DespairKnightPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            DespairKnightBlessingChoiceCard => DespairKnightPageMode.Blessing,
            DespairKnightDespairChoiceCard => DespairKnightPageMode.Despair,
            DespairKnightTearSwordChoiceCard => DespairKnightPageMode.TearSword,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(DespairKnightPageMode mode)
    {
        return mode is DespairKnightPageMode.None
            or DespairKnightPageMode.Blessing
            or DespairKnightPageMode.Despair
            or DespairKnightPageMode.TearSword;
    }

    private static bool IsConcreteMode(DespairKnightPageMode mode)
    {
        return mode is DespairKnightPageMode.Blessing
            or DespairKnightPageMode.Despair
            or DespairKnightPageMode.TearSword;
    }

    [AbnormalityPagePostObtainEffect]
    private async Task SetMode(DespairKnightPageMode mode)
    {
        Mode = mode;
        BlessingTriggeredThisCombat = false;
        BlessingPendingNextPlayerTurn = false;
        DespairTriggeredThisCombat = false;
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();

        if (Mode == DespairKnightPageMode.TearSword)
        {
            await ApplyTearSwordEnchantmentSelection();
        }
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
        DespairKnightPageMode oldMode = Mode;
        Mode = DespairKnightPageMode.Blessing;
        BlessingTriggeredThisCombat = false;
        BlessingPendingNextPlayerTurn = false;
        DespairTriggeredThisCombat = false;
        Log.Warn("[LibraryOfRuina.PageRelic] DespairKnightPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Blessing.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            DespairKnightPageMode.Blessing when CombatManager.Instance.IsInProgress =>
                BlessingTriggeredThisCombat ? RelicStatus.Disabled : RelicStatus.Active,
            DespairKnightPageMode.Despair when CombatManager.Instance.IsInProgress =>
                DespairTriggeredThisCombat ? RelicStatus.Disabled : RelicStatus.Active,
            DespairKnightPageMode.TearSword when CombatManager.Instance.IsInProgress => RelicStatus.Active,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }

    private async Task ApplyTearSwordEnchantmentSelection()
    {
        Sharp canonicalSharp = ModelDb.Enchantment<Sharp>();
        int targetCount = PileType.Deck.GetPile(Owner).Cards.Count(canonicalSharp.CanEnchant);
        int selectionCount = Math.Min(TearSwordEnchantCards, targetCount);
        if (selectionCount <= 0)
        {
            return;
        }

        CardSelectorPrefs prefs = new(CardSelectorPrefs.EnchantSelectionPrompt, selectionCount)
        {
            Cancelable = false
        };
        IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckForEnchantment(
            Owner,
            canonicalSharp,
            TearSwordSharpAmount,
            prefs);

        foreach (CardModel card in selectedCards)
        {
            CardCmd.Enchant(canonicalSharp.ToMutable(), card, TearSwordSharpAmount);
            CardCmd.Preview(card);
        }
    }

    private bool IsOwnerAttackCardSource(Creature? dealer, CardModel? cardSource, ValueProp props)
    {
        if (dealer == null || cardSource == null || !ValuePropCompat.IsPoweredAttack(props))
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        return cardSource.Owner == Owner && cardSource.Type == CardType.Attack;
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

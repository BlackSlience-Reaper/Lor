using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.RedShoes;
using LibraryOfRuina.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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

namespace LibraryOfRuina.relics.RedShoes;

public sealed class RedShoesPageRelic : LibraryRelicModel
{
    internal const int GlitterStrength = 2;
    internal const int GlitterEndTurnHpLoss = 1;
    internal const int BloodThirstActiveRound = 1;
    internal const decimal BloodThirstDamageMultiplier = 2m;
    internal const int AxeStrength = 3;
    internal const int AxeFailedBlockBreakHpLoss = 1;

    protected override string IconBaseName => "red_shoes_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<RedShoesPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)RedShoesPageMode.None),
        new PowerVar<LibraryStrongPower>("GlitterStrength", GlitterStrength),
        new HpLossVar(GlitterEndTurnHpLoss),
        new DynamicVar("ActiveRound", BloodThirstActiveRound),
        new DynamicVar("DamageMultiplier", BloodThirstDamageMultiplier),
        new PowerVar<LibraryStrongPower>("AxeStrength", AxeStrength),
        new DynamicVar("AxeHpLoss", AxeFailedBlockBreakHpLoss)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        RedShoesPageMode.Glitter =>
        [
            HoverTipFactory.FromPower<LibraryStrongPower>()
        ],
        RedShoesPageMode.BloodThirst => [],
        RedShoesPageMode.Axe =>
        [
            HoverTipFactory.FromPower<LibraryStrongPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public RedShoesPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BloodThirstTriggeredThisCombat { get; private set; }

    private CardModel? _bloodThirstActiveCard;

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != RedShoesPageMode.None)
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

        if (Owner.Creature == null)
        {
            UpdateModeUiState();
            return;
        }

        if (Mode == RedShoesPageMode.Glitter)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                Owner.Creature,
                GlitterStrength,
                turns: -1,
                Owner.Creature,
                null,
                silent: true);
        }
        else if (Mode == RedShoesPageMode.Axe)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                Owner.Creature,
                AxeStrength,
                turns: -1,
                Owner.Creature,
                null,
                silent: true);
        }

        UpdateModeUiState();
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Mode == RedShoesPageMode.Glitter
            && Owner.Creature.IsAlive && side == Owner.Creature.Side)
        {
            Flash();
            await CreatureCmdCompat.Damage(
                choiceContext,
                Owner.Creature,
                GlitterEndTurnHpLoss,
                ValueProp.Unpowered,
                Owner.Creature,
                null);
        }

        if (Mode == RedShoesPageMode.BloodThirst
            && side == Owner.Creature.Side)
        {
            UpdateModeUiState();
        }
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!IsOwnerAttackSource(cardPlay.Card.Owner.Creature, cardPlay.Card, ValueProp.Move))
        {
            return Task.CompletedTask;
        }

        if (Mode == RedShoesPageMode.BloodThirst
            && IsBloodThirstAvailable()
            && cardPlay.Card.Type == CardType.Attack)
        {
            BloodThirstTriggeredThisCombat = true;
            _bloodThirstActiveCard = cardPlay.Card;
            Flash();
            UpdateModeUiState();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (ReferenceEquals(_bloodThirstActiveCard, cardPlay.Card))
        {
            _bloodThirstActiveCard = null;
        }

        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Mode != RedShoesPageMode.BloodThirst
            || cardSource == null
            || !ReferenceEquals(cardSource, _bloodThirstActiveCard)
            || !IsOwnerAttackSource(dealer, cardSource, props))
        {
            return 1m;
        }

        return BloodThirstDamageMultiplier;
    }

    public override decimal ModifyChaoDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        if (Mode != RedShoesPageMode.BloodThirst
            || cardSource == null
            || !ReferenceEquals(cardSource, _bloodThirstActiveCard)
            || !IsOwnerAttackSource(dealer, cardSource, props))
        {
            return 1m;
        }

        return BloodThirstDamageMultiplier;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode == RedShoesPageMode.Axe
            && IsOwnerAttackSource(dealer, cardSource, props)
            && target.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target)
            && result.BlockedDamage > 0
            && !result.WasBlockBroken)
        {
            Flash();
            await CreatureCmdCompat.Damage(
                choiceContext,
                Owner.Creature,
                AxeFailedBlockBreakHpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Owner.Creature,
                null);
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
            Owner.RunState.CreateCard<RedShoesGlitterChoiceCard>(Owner),
            Owner.RunState.CreateCard<RedShoesBloodThirstChoiceCard>(Owner),
            Owner.RunState.CreateCard<RedShoesAxeChoiceCard>(Owner)
        ];
    }

    private static RedShoesPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            RedShoesGlitterChoiceCard => RedShoesPageMode.Glitter,
            RedShoesBloodThirstChoiceCard => RedShoesPageMode.BloodThirst,
            RedShoesAxeChoiceCard => RedShoesPageMode.Axe,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(RedShoesPageMode mode)
    {
        return mode is RedShoesPageMode.None
            or RedShoesPageMode.Glitter
            or RedShoesPageMode.BloodThirst
            or RedShoesPageMode.Axe;
    }

    private static bool IsConcreteMode(RedShoesPageMode mode)
    {
        return mode is RedShoesPageMode.Glitter
            or RedShoesPageMode.BloodThirst
            or RedShoesPageMode.Axe;
    }

    private void SetMode(RedShoesPageMode mode)
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
        RedShoesPageMode oldMode = Mode;
        Mode = RedShoesPageMode.Glitter;
        ResetTransientCombatState();
        Log.Warn("[LibraryOfRuina.PageRelic] RedShoesPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Glitter.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void ResetTransientCombatState()
    {
        BloodThirstTriggeredThisCombat = false;
        _bloodThirstActiveCard = null;
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == RedShoesPageMode.BloodThirst && CombatManager.Instance.IsInProgress
            ? IsBloodThirstAvailable() ? RelicStatus.Active : RelicStatus.Disabled
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private bool IsBloodThirstAvailable()
    {
        return !BloodThirstTriggeredThisCombat
            && Owner.Creature?.CombatState?.RoundNumber == BloodThirstActiveRound;
    }

    private bool IsOwnerAttackSource(Creature? dealer, CardModel? cardSource, ValueProp props)
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

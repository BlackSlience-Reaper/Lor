using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.FuneralOfTheDeadButterflies;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.enchantments.FuneralOfTheDeadButterflies;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.FuneralOfTheDeadButterflies;

public sealed class FuneralOfTheDeadButterfliesPageRelic : ModalPageRelic<FuneralOfTheDeadButterfliesPageMode>
{
    internal const int RestEnchantMaxSelect = 3;
    internal const int CoffinStrength = 4;
    internal const int CoffinDexterity = 4;
    internal const int MourningStunTurns = 1;

    protected override string IconBaseName => "funeral_of_the_dead_butterflies_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<FuneralOfTheDeadButterfliesPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)FuneralOfTheDeadButterfliesPageMode.None),
        new CardsVar(RestEnchantMaxSelect),
        new PowerVar<StrengthPower>(CoffinStrength),
        new PowerVar<DexterityPower>(CoffinDexterity),
        new DynamicVar("StunTurns", MourningStunTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        FuneralOfTheDeadButterfliesPageMode.Rest =>
        [
            ..HoverTipFactory.FromEnchantment<ChainEnchantment>()
        ],
        FuneralOfTheDeadButterfliesPageMode.Coffin =>
        [
            HoverTipFactory.FromPower<StrengthPower>(),
            HoverTipFactory.FromPower<DexterityPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public FuneralOfTheDeadButterfliesPageMode Mode { get; private set; }

    protected override FuneralOfTheDeadButterfliesPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshIconOnModeChange => false;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool MourningTriggeredThisCombat { get; private set; }

    protected override async Task ApplyObtainedChoiceAsync(FuneralOfTheDeadButterfliesPageMode mode)
    {
        SetMode(mode);
        if (Mode == FuneralOfTheDeadButterfliesPageMode.Rest)
        {
            await ApplyRestEnchantmentSelection();
        }
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        MourningTriggeredThisCombat = false;

        UpdateModeUiState();
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Mode != FuneralOfTheDeadButterfliesPageMode.Coffin
            || side != Owner.Creature.Side
            || Owner.Creature == null
            || !Owner.Creature.Powers.Any(power => power.TypeForCurrentAmount == PowerType.Debuff))
        {
            return;
        }

        Flash();
        
        await PowerCmdCompat.Apply<FlexPotionPower>(Owner.Creature, CoffinStrength, Owner.Creature, null, silent: true);
        
        await PowerCmdCompat.Apply<AnticipatePower>(Owner.Creature, CoffinDexterity, Owner.Creature, null, silent: true);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode != FuneralOfTheDeadButterfliesPageMode.Mourning
            || MourningTriggeredThisCombat
            || result.UnblockedDamage <= 0
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || target.Monster == null
            || !IsOwnerAttackSource(dealer, cardSource, props))
        {
            return;
        }

        MourningTriggeredThisCombat = true;
        Flash([target]);
        UpdateModeUiState();
        string? recoveryMoveId = ResolveStunRecoveryMoveId(target);
        if (recoveryMoveId == null)
        {
            return;
        }

        await CreatureCmd.Stun(target, recoveryMoveId);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        MourningTriggeredThisCombat = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<FuneralRestChoiceCard>(Owner),
            Owner.RunState.CreateCard<FuneralCoffinChoiceCard>(Owner),
            Owner.RunState.CreateCard<FuneralMourningChoiceCard>(Owner)
        ];
    }

    protected override void ResetStateOnModeSet(FuneralOfTheDeadButterfliesPageMode mode) => MourningTriggeredThisCombat = false;

    protected override void ResetStateOnFallback() => MourningTriggeredThisCombat = false;

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == FuneralOfTheDeadButterfliesPageMode.Mourning && CombatManager.Instance.IsInProgress
            ? MourningTriggeredThisCombat ? RelicStatus.Disabled : RelicStatus.Active
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
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

    private static string? ResolveStunRecoveryMoveId(Creature target)
    {
        string? nextMoveId = target.Monster?.NextMove?.Id;
        return string.IsNullOrWhiteSpace(nextMoveId) || nextMoveId == MonsterModel.stunnedMoveId
            ? null
            : nextMoveId;
    }

    [AbnormalityPagePostObtainEffect((int)FuneralOfTheDeadButterfliesPageMode.Rest)]
    private async Task ApplyRestEnchantmentSelection()
    {
        EnchantmentModel enchantmentForSelection = ModelDb.Enchantment<ChainEnchantment>();
        CardSelectorPrefs prefs = new(SelectionScreenPrompt, RestEnchantMaxSelect);

        IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckForEnchantment(
            Owner,
            enchantmentForSelection,
            amount: 1,
            additionalFilter: static card => card?.Type == CardType.Attack,
            prefs: prefs);

        foreach (CardModel card in selectedCards)
        {
            CardCmd.Enchant<ChainEnchantment>(card, 1);
            PlayEnchantVfx(card);
        }
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

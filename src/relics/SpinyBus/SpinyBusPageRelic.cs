using System.Threading.Tasks;
using LibraryOfRuina.cards.SpinyBus;
using LibraryOfRuina.combat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.SpinyBus;

public sealed class SpinyBusPageRelic : ModalPageRelic<SpinyBusPageMode>
{
    internal const int ThornsDamageBonus = 5;
    internal const int PleasureFlawStacks = 1;
    internal const int PleasureStrongStacks = 6;
    internal const int PleasureTurns = 3;
    internal const int LaughingPowderHeal = 1;

    protected override string IconBaseName => "spiny_bus_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<SpinyBusPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)SpinyBusPageMode.None),
        new DynamicVar("Damage", ThornsDamageBonus),
        new DynamicVar("Flaw", PleasureFlawStacks),
        new DynamicVar("Strong", PleasureStrongStacks),
        new DynamicVar("Turns", PleasureTurns),
        new HealVar(LaughingPowderHeal)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        SpinyBusPageMode.Pleasure =>
        [
            HoverTipFactory.FromPower<LibraryDisarmPower>(),
            HoverTipFactory.FromPower<LibraryStrongPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public SpinyBusPageMode Mode { get; private set; }

    protected override SpinyBusPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        UpdateModeUiState();

        if (Mode != SpinyBusPageMode.Pleasure)
        {
            return;
        }

        Flash();
        await LibraryPowerCmd.Apply<LibraryDisarmPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            PleasureFlawStacks,
            PleasureTurns - 1,
            IsPermanent: false,
            Owner.Creature,
            null,
            silent: true);
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            PleasureStrongStacks,
            PleasureTurns - 1,
            IsPermanent: false,
            Owner.Creature,
            null,
            silent: true);
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        if (Mode != SpinyBusPageMode.Thorns
            || type != LibraryDamageType.Pierce
            || target == null
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !IsOwnerPoweredAttackSource(dealer, props))
        {
            return 0m;
        }

        return ThornsDamageBonus;
    }

    public override decimal ModifyChaoDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        if (Mode != SpinyBusPageMode.Thorns
            || type != LibraryDamageType.Pierce
            || target == null
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !IsOwnerPoweredAttackSource(dealer, props))
        {
            return 0m;
        }

        return ThornsDamageBonus;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Mode != SpinyBusPageMode.LaughingPowder
            || target != Owner.Creature
            || !target.IsAlive
            || result.UnblockedDamage <= 0m
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(Owner.Creature, LaughingPowderHeal);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<SpinyBusThornsChoiceCard>(Owner),
            Owner.RunState.CreateCard<SpinyBusPleasureChoiceCard>(Owner),
            Owner.RunState.CreateCard<SpinyBusLaughingPowderChoiceCard>(Owner)
        ];
    }

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == SpinyBusPageMode.LaughingPowder && CombatManager.Instance.IsInProgress
            ? RelicStatus.Active
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private bool IsOwnerPoweredAttackSource(Creature? dealer, ValueProp props)
    {
        if (dealer == null || !ValuePropCompat.IsPoweredAttack(props))
        {
            return false;
        }

        return dealer == Owner.Creature || dealer == Owner.Osty;
    }
}

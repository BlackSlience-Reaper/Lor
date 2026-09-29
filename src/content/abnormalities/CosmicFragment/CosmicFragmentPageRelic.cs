using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.cards.TechnologyFloorLiberation;
using LibraryOfRuina.combat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.CosmicFragment;

public sealed class CosmicFragmentPageRelic : ModalPageRelic<CosmicFragmentPageMode>
{
    internal const int OtherworldlyEchoChaosLoss = 13;
    internal const int OtherworldlyEchoHeal = 3;
    internal const decimal TentacleChaosRatio = 0.06m;
    internal const int TentacleChaosPercent = 6;
    internal const int IncomprehensibleChaosStepPercent = 10;
    internal const int IncomprehensibleResetAttacks = 4;

    private Dictionary<CardModel, int> _incomprehensibleBonusesByActiveCard = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _incomprehensibleBonusesByActiveCard = new(
            _incomprehensibleBonusesByActiveCard);
    }

    protected override string IconBaseName => "cosmic_fragment_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<CosmicFragmentPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)CosmicFragmentPageMode.None),
        new DynamicVar("ChaosLoss", OtherworldlyEchoChaosLoss),
        new HealVar(OtherworldlyEchoHeal),
        new DynamicVar("ChaosPercent", TentacleChaosPercent),
        new DynamicVar("ChaosStepPercent", IncomprehensibleChaosStepPercent),
        new DynamicVar("ResetAttacks", IncomprehensibleResetAttacks)
    ];

    public override bool ShowCounter => Mode == CosmicFragmentPageMode.Incomprehensible;

    public override int DisplayAmount =>
        Mode == CosmicFragmentPageMode.Incomprehensible ? IncomprehensibleAttackCount : 0;

    [SavedProperty]
    public CosmicFragmentPageMode Mode { get; private set; }

    protected override CosmicFragmentPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int IncomprehensibleAttackCount { get; private set; }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        _incomprehensibleBonusesByActiveCard.Clear();
        UpdateModeUiState();

        if (Mode != CosmicFragmentPageMode.OtherworldlyEcho)
        {
            return;
        }

        List<Creature> targets = AllyTurnRegistry
            .FilterPlayerEnemyTargets(Owner.Creature.CombatState?.Enemies)
            .Where(static enemy => enemy.IsAlive && enemy is LibraryCreature { HasChaoResistance: true, MaxChaoValue: > 0 })
            .ToList();

        if (targets.Count > 0)
        {
            Flash(targets);
            await LibraryCreatureCmd.ChaoDamage(
                new BlockingPlayerChoiceContext(),
                targets,
                OtherworldlyEchoChaosLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Owner.Creature,
                null,
                null);
        }

        Flash();
        await CreatureCmd.Heal(Owner.Creature, OtherworldlyEchoHeal);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (cardSource is MagicBulletPierceEgoCard)
        {
            await TryDealTentacleChaos(choiceContext, dealer, result, props, target, cardSource);
        }
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        if (type == LibraryDamageType.Pierce)
        {
            await TryDealTentacleChaos(choiceContext, dealer, result, props, target, cardSource);
        }
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Mode == CosmicFragmentPageMode.Incomprehensible
            && cardPlay.Card.Owner == Owner
            && cardPlay.Card.Type == CardType.Attack)
        {
            _incomprehensibleBonusesByActiveCard[cardPlay.Card] = IncomprehensibleAttackCount;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Mode != CosmicFragmentPageMode.Incomprehensible
            || cardPlay.Card.Owner != Owner
            || !_incomprehensibleBonusesByActiveCard.Remove(cardPlay.Card))
        {
            return Task.CompletedTask;
        }

        IncomprehensibleAttackCount = (IncomprehensibleAttackCount + 1) % IncomprehensibleResetAttacks;
        Flash();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override decimal ModifyChaoDamageMultiplicative(
        Creature? target,
        decimal num,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        if (Mode != CosmicFragmentPageMode.Incomprehensible
            || cardSource == null
            || target?.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !IsOwnerAttackSource(dealer, cardSource, props)
            || !_incomprehensibleBonusesByActiveCard.TryGetValue(cardSource, out int attackCount))
        {
            return 1m;
        }

        return 1m + attackCount * IncomprehensibleChaosStepPercent / 100m;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _incomprehensibleBonusesByActiveCard.Clear();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<CosmicFragmentOtherworldlyEchoChoiceCard>(Owner),
            Owner.RunState.CreateCard<CosmicFragmentTentacleChoiceCard>(Owner),
            Owner.RunState.CreateCard<CosmicFragmentIncomprehensibleChoiceCard>(Owner)
        ];
    }

    protected override void ResetStateOnModeSet(CosmicFragmentPageMode mode)
    {
        IncomprehensibleAttackCount = 0;
        _incomprehensibleBonusesByActiveCard.Clear();
    }

    protected override void ResetStateOnFallback() => ResetStateOnModeSet(FallbackMode);

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == CosmicFragmentPageMode.Incomprehensible && CombatManager.Instance.IsInProgress
            ? IncomprehensibleAttackCount > 0 ? RelicStatus.Active : RelicStatus.Normal
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private bool IsOwnerAttackSource(Creature? dealer, CardModel? cardSource, ValueProp props)
    {
        if (cardSource == null || !IsOwnerPoweredDamageSource(dealer, props))
        {
            return false;
        }

        return cardSource.Owner == Owner && cardSource.Type == CardType.Attack;
    }

    private bool IsOwnerPoweredDamageSource(Creature? dealer, ValueProp props)
    {
        if (dealer == null || !ValuePropCompat.IsPoweredAttack(props))
        {
            return false;
        }

        return dealer == Owner.Creature || dealer == Owner.Osty;
    }

    private async Task TryDealTentacleChaos(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode != CosmicFragmentPageMode.Tentacle
            || !IsOwnerPoweredDamageSource(dealer, props)
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || result.UnblockedDamage <= 0m
            || target is not LibraryCreature { HasChaoResistance: true, MaxChaoValue: > 0 } libraryTarget)
        {
            return;
        }

        int chaosDamage = Math.Max(1, (int)Math.Ceiling(libraryTarget.MaxChaoValue * TentacleChaosRatio));
        Flash([target]);
        await LibraryCreatureCmd.ChaoDamage(
            choiceContext,
            [target],
            chaosDamage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner.Creature,
            cardSource,
            null,
            LibraryDamageType.Pierce);
    }
}

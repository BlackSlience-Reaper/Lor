using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.cards.CosmicFragment;
using LibraryOfRuina.cards.TechnologyFloorLiberation;
using LibraryOfRuina.combat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.CosmicFragment;

public sealed class CosmicFragmentPageRelic : LibraryRelicModel
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

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int IncomprehensibleAttackCount { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != CosmicFragmentPageMode.None)
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

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<CosmicFragmentOtherworldlyEchoChoiceCard>(Owner),
            Owner.RunState.CreateCard<CosmicFragmentTentacleChoiceCard>(Owner),
            Owner.RunState.CreateCard<CosmicFragmentIncomprehensibleChoiceCard>(Owner)
        ];
    }

    private static CosmicFragmentPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            CosmicFragmentOtherworldlyEchoChoiceCard => CosmicFragmentPageMode.OtherworldlyEcho,
            CosmicFragmentTentacleChoiceCard => CosmicFragmentPageMode.Tentacle,
            CosmicFragmentIncomprehensibleChoiceCard => CosmicFragmentPageMode.Incomprehensible,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(CosmicFragmentPageMode mode)
    {
        return mode is CosmicFragmentPageMode.None
            or CosmicFragmentPageMode.OtherworldlyEcho
            or CosmicFragmentPageMode.Tentacle
            or CosmicFragmentPageMode.Incomprehensible;
    }

    private static bool IsConcreteMode(CosmicFragmentPageMode mode)
    {
        return mode is CosmicFragmentPageMode.OtherworldlyEcho
            or CosmicFragmentPageMode.Tentacle
            or CosmicFragmentPageMode.Incomprehensible;
    }

    private void SetMode(CosmicFragmentPageMode mode)
    {
        Mode = mode;
        IncomprehensibleAttackCount = 0;
        _incomprehensibleBonusesByActiveCard.Clear();
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
        CosmicFragmentPageMode oldMode = Mode;
        Mode = CosmicFragmentPageMode.OtherworldlyEcho;
        IncomprehensibleAttackCount = 0;
        _incomprehensibleBonusesByActiveCard.Clear();
        Log.Warn("[LibraryOfRuina.PageRelic] CosmicFragmentPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to OtherworldlyEcho.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void UpdateModeUiState()
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

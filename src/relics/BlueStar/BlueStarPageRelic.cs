using System;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.BlueStar;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.BlueStar;

public sealed class BlueStarPageRelic : LibraryRelicModel
{
    public const int AtonementPercent = 15;
    public const int VoiceTurnInterval = 3;
    public const int VoiceChaoDamage = 4;

    private bool _voiceTriggerTurn;
    private bool _isApplyingVoiceDamage;

    protected override string IconBaseName => "blue_star_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<BlueStarPageRelic>(
            runState);

    public override bool ShowCounter =>
        Mode == BlueStarPageMode.VoiceOfRemembrance;

    public override int DisplayAmount =>
        Mode == BlueStarPageMode.VoiceOfRemembrance
            ? _voiceTriggerTurn
                ? VoiceTurnInterval
                : Math.Max(0, VoiceTurnsSeen)
            : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)BlueStarPageMode.None),
        new DynamicVar("ChaoDamage", BlueStarMartyrdomCard.BaseChaoDamage),
        new DynamicVar(
            "UpgradedChaoDamage",
            BlueStarMartyrdomCard.UpgradedChaoDamage),
        new HpLossVar(BlueStarMartyrdomCard.SelfHpLoss),
        new DynamicVar("AtonementPercent", AtonementPercent),
        new DynamicVar("TurnInterval", VoiceTurnInterval),
        new DynamicVar("VoiceChaoDamage", VoiceChaoDamage)
    ];

    // protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    // [
    //     ..HoverTipFactory.FromCardWithCardHoverTips<BlueStarMartyrdomCard>()
    // ];

    [SavedProperty]
    public BlueStarPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int VoiceTurnsSeen { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != BlueStarPageMode.None)
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
            await AbnormalityPageRewardHelper.SkipObtainedPageRelic(
                this,
                nameof(AfterObtained));
            return;
        }

        SetMode(ResolveModeFromChoiceCard(chosenCard));
        if (Mode == BlueStarPageMode.Martyrdom)
        {
            await AddMartyrdomCardOnPickup();
        }
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
        _voiceTriggerTurn = false;
        _isApplyingVoiceDamage = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner
            || Mode != BlueStarPageMode.VoiceOfRemembrance)
        {
            return Task.CompletedTask;
        }

        bool shouldTrigger =
            VoiceTurnsSeen >= VoiceTurnInterval - 1;
        VoiceTurnsSeen = shouldTrigger ? 0 : VoiceTurnsSeen + 1;
        _voiceTriggerTurn = shouldTrigger;
        if (shouldTrigger)
        {
            Flash();
        }

        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (_isApplyingVoiceDamage
            || Mode != BlueStarPageMode.VoiceOfRemembrance
            || !_voiceTriggerTurn
            || dealer?.IsPlayer != true
            || target.IsDead
            || target.Side == dealer.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !ValuePropCompat.IsPoweredAttack(props)
            || target is not LibraryCreature
            {
                HasChaoResistance: true,
                MaxChaoValue: > 0
            })
        {
            return;
        }

        _isApplyingVoiceDamage = true;
        try
        {
            Flash([target]);
            await LibraryCreatureCmd.ChaoDamage(
                choiceContext,
                [target],
                VoiceChaoDamage,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer,
                cardSource,
                null);
        }
        finally
        {
            _isApplyingVoiceDamage = false;
        }
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
        if (Mode != BlueStarPageMode.Atonement
            || dealer == null
            || (dealer != Owner.Creature && dealer != Owner.Osty)
            || target?.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target))
        {
            return 1m;
        }

        return 1m + AtonementPercent / 100m;
    }

    public override async Task AfterStun(Creature creature)
    {
        if (Mode != BlueStarPageMode.Atonement
            || creature.IsDead
            || creature.CurrentHp <= 0m
            || creature.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(creature))
        {
            return;
        }

        decimal hpLoss = Math.Ceiling(
            creature.CurrentHp * AtonementPercent / 100m);
        if (hpLoss <= 0m)
        {
            return;
        }

        Flash([creature]);
        await CreatureCmd.SetCurrentHp(
            creature,
            Math.Max(0m, creature.CurrentHp - hpLoss));
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _voiceTriggerTurn = false;
        _isApplyingVoiceDamage = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        Owner.RunState.CreateCard<BlueStarMartyrdomChoiceCard>(Owner),
        Owner.RunState.CreateCard<BlueStarAtonementChoiceCard>(Owner),
        Owner.RunState.CreateCard<BlueStarVoiceOfRemembranceChoiceCard>(Owner)
    ];

    private static BlueStarPageMode ResolveModeFromChoiceCard(
        CardModel? card) => card switch
    {
        BlueStarMartyrdomChoiceCard => BlueStarPageMode.Martyrdom,
        BlueStarAtonementChoiceCard => BlueStarPageMode.Atonement,
        BlueStarVoiceOfRemembranceChoiceCard =>
            BlueStarPageMode.VoiceOfRemembrance,
        _ => throw AbnormalityPageRewardHelper
            .UnexpectedPageChoiceCard(card)
    };

    [AbnormalityPagePostObtainEffect((int)BlueStarPageMode.Martyrdom)]
    private async Task AddMartyrdomCardOnPickup()
    {
        CardModel card = Owner.RunState.CreateCard<BlueStarMartyrdomCard>(
            Owner);
        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.Add(card, PileType.Deck));
        SaveManager.Instance.MarkCardAsSeen(card);
    }

    private void SetMode(BlueStarPageMode mode)
    {
        Mode = mode;
        _voiceTriggerTurn = false;
        if (mode != BlueStarPageMode.VoiceOfRemembrance)
        {
            VoiceTurnsSeen = 0;
        }

        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private static bool IsKnownMode(BlueStarPageMode mode) =>
        mode is BlueStarPageMode.None
            or BlueStarPageMode.Martyrdom
            or BlueStarPageMode.Atonement
            or BlueStarPageMode.VoiceOfRemembrance;

    private static bool IsConcreteMode(BlueStarPageMode mode) =>
        mode is BlueStarPageMode.Martyrdom
            or BlueStarPageMode.Atonement
            or BlueStarPageMode.VoiceOfRemembrance;

    private void EnsureValidModeOrFallback(string context)
    {
        if (!IsConcreteMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(context);
        }
    }

    private void FallbackToDefaultModeAfterLoad(string context)
    {
        BlueStarPageMode oldMode = Mode;
        Mode = BlueStarPageMode.Martyrdom;
        VoiceTurnsSeen = 0;
        _voiceTriggerTurn = false;
        Log.Warn(
            "[LibraryOfRuina.PageRelic] BlueStarPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Martyrdom.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == BlueStarPageMode.VoiceOfRemembrance
                 && CombatManager.Instance.IsInProgress
                 && _voiceTriggerTurn
            ? RelicStatus.Active
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
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

public enum BlueStarPageMode
{
    None = 0,
    Martyrdom = 1,
    Atonement = 2,
    VoiceOfRemembrance = 3
}

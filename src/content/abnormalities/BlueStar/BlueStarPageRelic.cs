using System;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.BlueStar;

public sealed class BlueStarPageRelic : ModalPageRelic<BlueStarPageMode>
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

    protected override BlueStarPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int VoiceTurnsSeen { get; private set; }

    protected override async Task ApplyObtainedChoiceAsync(BlueStarPageMode mode)
    {
        SetMode(mode);
        if (Mode == BlueStarPageMode.Martyrdom)
        {
            await AddMartyrdomCardOnPickup();
        }
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

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        Owner.RunState.CreateCard<BlueStarMartyrdomChoiceCard>(Owner),
        Owner.RunState.CreateCard<BlueStarAtonementChoiceCard>(Owner),
        Owner.RunState.CreateCard<BlueStarVoiceOfRemembranceChoiceCard>(Owner)
    ];

    [AbnormalityPagePostObtainEffect((int)BlueStarPageMode.Martyrdom)]
    private async Task AddMartyrdomCardOnPickup()
    {
        CardModel card = Owner.RunState.CreateCard<BlueStarMartyrdomCard>(
            Owner);
        CardCmd.PreviewCardPileAdd(
            await CardPileCmd.Add(card, PileType.Deck));
        SaveManager.Instance.MarkCardAsSeen(card);
    }

    protected override void ResetStateOnModeSet(BlueStarPageMode mode)
    {
        _voiceTriggerTurn = false;
        if (mode != BlueStarPageMode.VoiceOfRemembrance)
        {
            VoiceTurnsSeen = 0;
        }
    }

    protected override void ResetStateOnFallback()
    {
        VoiceTurnsSeen = 0;
        _voiceTriggerTurn = false;
    }

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == BlueStarPageMode.VoiceOfRemembrance
                 && CombatManager.Instance.IsInProgress
                 && _voiceTriggerTurn
            ? RelicStatus.Active
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }
}

public enum BlueStarPageMode
{
    None = 0,
    Martyrdom = 1,
    Atonement = 2,
    VoiceOfRemembrance = 3
}

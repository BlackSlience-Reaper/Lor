using System;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.LittleRedMercenary;
using LibraryOfRuina.compat;
using LibraryOfRuina.powers.LittleRedMercenary;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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

namespace LibraryOfRuina.relics.LittleRedMercenary;

public sealed class LittleRedMercenaryPageRelic : RelicModel
{
    internal const int ScarHpPercentHigh = 90;
    internal const int ScarHpPercentMid = 70;
    internal const int ScarHpPercentLow = 50;
    internal const int ScarStrengthBelowHigh = 2;
    internal const int ScarStrengthBelowMid = 3;
    internal const int ScarStrengthBelowLow = 4;
    internal const int RevengeHpLossPerTrigger = 4;
    internal const int RevengeStrengthPerTrigger = 3;
    internal const int RevengeMaxTriggersPerCombat = 3;
    internal const int PreyDamageBonus = 5;

    protected override string IconBaseName => Mode switch
    {
        LittleRedMercenaryPageMode.Scar => "little_red_mercenary_page_scar_relic",
        LittleRedMercenaryPageMode.Revenge => "little_red_mercenary_page_revenge_relic",
        LittleRedMercenaryPageMode.Prey => "little_red_mercenary_page_prey_relic",
        _ => "little_red_mercenary_page_relic"
    };

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<LittleRedMercenaryPageRelic>(runState);

    public override bool ShowCounter =>
        CombatManager.Instance.IsInProgress
        && Mode == LittleRedMercenaryPageMode.Revenge;

    public override int DisplayAmount =>
        Mode == LittleRedMercenaryPageMode.Revenge ? RevengeTriggersRemainingThisCombat : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)LittleRedMercenaryPageMode.None),
        new DynamicVar("ScarHpHigh", ScarHpPercentHigh),
        new DynamicVar("ScarHpMid", ScarHpPercentMid),
        new DynamicVar("ScarHpLow", ScarHpPercentLow),
        new DynamicVar("ScarStrengthHigh", ScarStrengthBelowHigh),
        new DynamicVar("ScarStrengthMid", ScarStrengthBelowMid),
        new DynamicVar("ScarStrengthLow", ScarStrengthBelowLow),
        new DynamicVar("HpLoss", RevengeHpLossPerTrigger),
        new DynamicVar("Strength", RevengeStrengthPerTrigger),
        new DynamicVar("MaxTriggers", RevengeMaxTriggersPerCombat),
        new DamageVar(PreyDamageBonus, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        LittleRedMercenaryPageMode.Scar =>
        [
            HoverTipFactory.FromPower<StrengthPower>()
        ],
        LittleRedMercenaryPageMode.Revenge =>
        [
            HoverTipFactory.FromPower<StrengthPower>()
        ],
        LittleRedMercenaryPageMode.Prey =>
        [
            HoverTipFactory.FromPower<LittleRedPreyPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public LittleRedMercenaryPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RevengeHpLossRemainderThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RevengeTriggersRemainingThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool PreyMarkedTargetThisCombat { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != LittleRedMercenaryPageMode.None)
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
        UpdateModeUiState();

        switch (Mode)
        {
            case LittleRedMercenaryPageMode.Scar:
                await ApplyScarStartEffect();
                break;
        }
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (Mode != LittleRedMercenaryPageMode.Revenge
            || creature != Owner.Creature
            || delta >= 0m
            || RevengeTriggersRemainingThisCombat <= 0
            || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        int wholeHpLoss = (int)Math.Floor(-delta);
        if (wholeHpLoss <= 0)
        {
            return;
        }

        int pendingTriggers = TrackRevengeHpLoss(wholeHpLoss);
        if (pendingTriggers <= 0)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<StrengthPower>(
            Owner.Creature,
            pendingTriggers * RevengeStrengthPerTrigger,
            Owner.Creature,
            null);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode != LittleRedMercenaryPageMode.Prey
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || result.UnblockedDamage <= 0m
            || PreyMarkedTargetThisCombat
            || !IsOwnerDamageSource(dealer, cardSource, props))
        {
            return;
        }

        PreyMarkedTargetThisCombat = true;
        await LittleRedPreyPower.ApplyMark(Owner.Creature, target, cardSource);
        Flash([target]);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTransientCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private async Task ApplyScarStartEffect()
    {
        Creature ownerCreature = Owner.Creature;
        if (ownerCreature.MaxHp <= 0)
        {
            return;
        }

        decimal hpRatio = (decimal)ownerCreature.CurrentHp / ownerCreature.MaxHp;
        int strength = hpRatio < ScarHpPercentLow / 100m
            ? ScarStrengthBelowLow
            : hpRatio < ScarHpPercentMid / 100m
                ? ScarStrengthBelowMid
                : hpRatio < ScarHpPercentHigh / 100m
                    ? ScarStrengthBelowHigh
                    : 0;
        if (strength <= 0)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<StrengthPower>(ownerCreature, strength, ownerCreature, null);
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<LittleRedScarChoiceCard>(Owner),
            Owner.RunState.CreateCard<LittleRedRevengeChoiceCard>(Owner),
            Owner.RunState.CreateCard<LittleRedPreyChoiceCard>(Owner)
        ];
    }

    private static LittleRedMercenaryPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            LittleRedScarChoiceCard => LittleRedMercenaryPageMode.Scar,
            LittleRedRevengeChoiceCard => LittleRedMercenaryPageMode.Revenge,
            LittleRedPreyChoiceCard => LittleRedMercenaryPageMode.Prey,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(LittleRedMercenaryPageMode mode)
    {
        return mode is LittleRedMercenaryPageMode.None
            or LittleRedMercenaryPageMode.Scar
            or LittleRedMercenaryPageMode.Revenge
            or LittleRedMercenaryPageMode.Prey;
    }

    private static bool IsConcreteMode(LittleRedMercenaryPageMode mode)
    {
        return mode is LittleRedMercenaryPageMode.Scar
            or LittleRedMercenaryPageMode.Revenge
            or LittleRedMercenaryPageMode.Prey;
    }

    private void SetMode(LittleRedMercenaryPageMode mode)
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
        LittleRedMercenaryPageMode oldMode = Mode;
        Mode = LittleRedMercenaryPageMode.Scar;
        ResetTransientCombatState();
        Log.Warn("[LibraryOfRuina.PageRelic] LittleRedMercenaryPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Scar.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void ResetTransientCombatState()
    {
        RevengeHpLossRemainderThisCombat = 0;
        RevengeTriggersRemainingThisCombat = Mode == LittleRedMercenaryPageMode.Revenge
            ? RevengeMaxTriggersPerCombat
            : 0;
        PreyMarkedTargetThisCombat = false;
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            LittleRedMercenaryPageMode.Revenge when CombatManager.Instance.IsInProgress =>
                RevengeTriggersRemainingThisCombat > 0 ? RelicStatus.Active : RelicStatus.Disabled,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }

    private bool IsOwnerDamageSource(Creature? dealer, CardModel? cardSource, ValueProp props)
    {
        if (dealer == null)
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        if (cardSource == null)
        {
            return true;
        }

        return cardSource.Owner == Owner;
    }

    private int TrackRevengeHpLoss(int wholeHpLoss)
    {
        int total = RevengeHpLossRemainderThisCombat + wholeHpLoss;
        int triggers = Math.Min(RevengeTriggersRemainingThisCombat, total / RevengeHpLossPerTrigger);
        RevengeTriggersRemainingThisCombat -= triggers;
        RevengeHpLossRemainderThisCombat = total - triggers * RevengeHpLossPerTrigger;
        UpdateModeUiState();
        return triggers;
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

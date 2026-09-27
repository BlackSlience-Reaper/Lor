using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.QueenOfHatred;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.QueenOfHatred;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers.QueenOfHatred;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using LibraryLib.Commands;
using LibraryLib.Powers;
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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.QueenOfHatred;

public sealed class QueenOfHatredPageRelic : RelicModel
{
    internal const int PhilanthropyHealAmount = 4;
    internal const int PhilanthropyTriggersPerTurn = 1;
    internal const int JusticeTargetCount = 1;
    internal const int JusticeDamageIncreasePercent = 50;
    internal const int HatredHpLossBonus = 1;
    internal const int HatredStrongGain = 1;
    internal const int HatredStrongTurns = 3;
    internal const int HatredNextTurnStrengthGain = HatredStrongGain;
    internal const int HatredTriggersPerTurn = 2;

    private int _pendingHatredStrongTriggers;

    protected override string IconBaseName => Mode switch
    {
        QueenOfHatredPageMode.Philanthropy => "queen_of_hatred_page_philanthropy_relic",
        QueenOfHatredPageMode.Justice => "queen_of_hatred_page_justice_relic",
        QueenOfHatredPageMode.Hatred => "queen_of_hatred_page_hatred_relic",
        _ => "queen_of_hatred_page_hatred_relic"
    };

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<QueenOfHatredPageRelic>(runState);

    public override bool ShowCounter =>
        CombatManager.Instance.IsInProgress
        && Mode is QueenOfHatredPageMode.Philanthropy or QueenOfHatredPageMode.Hatred;

    public override int DisplayAmount => Mode switch
    {
        QueenOfHatredPageMode.Philanthropy => PhilanthropyTriggersRemainingThisTurn,
        QueenOfHatredPageMode.Hatred => HatredTriggersRemainingThisTurn,
        _ => 0
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)QueenOfHatredPageMode.None),
        new HealVar(PhilanthropyHealAmount),
        new DynamicVar("PhilanthropyTriggers", PhilanthropyTriggersPerTurn),
        new DynamicVar("Targets", JusticeTargetCount),
        new DynamicVar("DamageIncrease", JusticeDamageIncreasePercent),
        new DynamicVar("HpLoss", HatredHpLossBonus),
        new PowerVar<LibraryStrongPower>("Strong", HatredStrongGain),
        new DynamicVar("StrongTurns", HatredStrongTurns),
        new DynamicVar("HatredTriggers", HatredTriggersPerTurn)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        QueenOfHatredPageMode.Justice =>
        [
            HoverTipFactory.FromPower<LibraryOfRuinaQueenBadGuyPower>()
        ],
        QueenOfHatredPageMode.Hatred =>
        [
            HoverTipFactory.FromPower<LibraryStrongPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public QueenOfHatredPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PhilanthropyTriggersRemainingThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HatredTriggersRemainingThisTurn { get; private set; }

    
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int JusticeTurnsSeenThisCombat { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != QueenOfHatredPageMode.None)
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
        ResetPerTurnCounters();

        if (Mode == QueenOfHatredPageMode.Justice)
        {
            await ApplyJusticeMark();
        }
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return Task.CompletedTask;
        }

        ResetPerTurnCounters();
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
        if (Mode != QueenOfHatredPageMode.Philanthropy
            || PhilanthropyTriggersRemainingThisTurn <= 0
            || !result.WasBlockBroken
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !IsOwnerAttackSource(dealer, cardSource, props))
        {
            return;
        }

        PhilanthropyTriggersRemainingThisTurn--;
        Flash([target]);
        UpdateModeUiState();
        await CreatureCmd.Heal(Owner.Creature, PhilanthropyHealAmount);
    }

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Mode != QueenOfHatredPageMode.Hatred
            || target != Owner.Creature
            || amount <= 0m
            || HatredTriggersRemainingThisTurn <= 0)
        {
            return amount;
        }

        // ModifyHpLost is also evaluated for client-side previews. Keep all
        // synchronized state untouched until the real HP-loss callback commits.
        _pendingHatredStrongTriggers = 1;
        return amount + HatredHpLossBonus;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        if (_pendingHatredStrongTriggers <= 0)
        {
            return;
        }

        int pendingTriggers = Math.Min(
            _pendingHatredStrongTriggers,
            HatredTriggersRemainingThisTurn);
        _pendingHatredStrongTriggers = 0;
        if (pendingTriggers <= 0)
        {
            return;
        }

        HatredTriggersRemainingThisTurn -= pendingTriggers;
        UpdateModeUiState();

        Flash();
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            Owner.Creature,
            pendingTriggers * HatredStrongGain,
            HatredStrongTurns - 1,
            Owner.Creature,
            null);
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
            Owner.RunState.CreateCard<QueenOfHatredPhilanthropyChoiceCard>(Owner),
            Owner.RunState.CreateCard<QueenOfHatredJusticeChoiceCard>(Owner),
            Owner.RunState.CreateCard<QueenOfHatredHatredChoiceCard>(Owner)
        ];
    }

    private static QueenOfHatredPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            QueenOfHatredPhilanthropyChoiceCard => QueenOfHatredPageMode.Philanthropy,
            QueenOfHatredJusticeChoiceCard => QueenOfHatredPageMode.Justice,
            QueenOfHatredHatredChoiceCard => QueenOfHatredPageMode.Hatred,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(QueenOfHatredPageMode mode)
    {
        return mode is QueenOfHatredPageMode.None
            or QueenOfHatredPageMode.Philanthropy
            or QueenOfHatredPageMode.Justice
            or QueenOfHatredPageMode.Hatred;
    }

    private static bool IsConcreteMode(QueenOfHatredPageMode mode)
    {
        return mode is QueenOfHatredPageMode.Philanthropy
            or QueenOfHatredPageMode.Justice
            or QueenOfHatredPageMode.Hatred;
    }

    private void SetMode(QueenOfHatredPageMode mode)
    {
        Mode = mode;
        ResetTransientCombatState();
        RelicIconChanged();
        RefreshInventoryIcon();
        UpdateModeUiState();
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
        QueenOfHatredPageMode oldMode = Mode;
        Mode = QueenOfHatredPageMode.Philanthropy;
        ResetTransientCombatState();
        Log.Warn("[LibraryOfRuina.PageRelic] QueenOfHatredPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Philanthropy.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void ResetTransientCombatState()
    {
        _pendingHatredStrongTriggers = 0;
        PhilanthropyTriggersRemainingThisTurn = 0;
        HatredTriggersRemainingThisTurn = 0;
        JusticeTurnsSeenThisCombat = 0;
    }

    private void ResetPerTurnCounters()
    {
        PhilanthropyTriggersRemainingThisTurn = Mode == QueenOfHatredPageMode.Philanthropy
            ? PhilanthropyTriggersPerTurn
            : 0;
        HatredTriggersRemainingThisTurn = Mode == QueenOfHatredPageMode.Hatred
            ? HatredTriggersPerTurn
            : 0;
        UpdateModeUiState();
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            QueenOfHatredPageMode.Philanthropy when CombatManager.Instance.IsInProgress =>
                PhilanthropyTriggersRemainingThisTurn > 0 ? RelicStatus.Active : RelicStatus.Disabled,
            QueenOfHatredPageMode.Hatred when CombatManager.Instance.IsInProgress =>
                HatredTriggersRemainingThisTurn > 0 ? RelicStatus.Active : RelicStatus.Disabled,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }

    private async Task ApplyJusticeMark()
    {
        IReadOnlyList<Creature> enemies = AllyTurnRegistry
            .FilterPlayerEnemyTargets(Owner.Creature.CombatState?.Enemies);
        if (enemies.Count == 0)
        {
            return;
        }

        List<Creature> livingEnemies = enemies.Where(enemy => enemy.IsAlive).ToList();
        if (livingEnemies.Count == 0)
        {
            return;
        }

        List<Creature> unmarkedEnemies = livingEnemies
            .Where(enemy => enemy.GetPower<LibraryOfRuinaQueenBadGuyPower>() == null
                && !enemy.HasPower<NihilBadGuyPower>())
            .ToList();
        if (unmarkedEnemies.Count == 0)
        {
            return;
        }

        Creature target = unmarkedEnemies[Owner.RunState.Rng.Niche.NextInt(unmarkedEnemies.Count)];
        if (target.GetPower<LibraryOfRuinaQueenBadGuyPower>() != null
            || target.HasPower<NihilBadGuyPower>())
        {
            return;
        }

        Flash([target]);
        await PowerCmdCompat.Apply<LibraryOfRuinaQueenBadGuyPower>(
            target,
            1m,
            Owner.Creature,
            null);
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

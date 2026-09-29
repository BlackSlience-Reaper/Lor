using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.HistoryFloorLiberation;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.enchantments.HistoryFloorLiberation;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using LibraryOfRuina.relics;
using LibraryLib.Entities.Creatures;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
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

namespace LibraryOfRuina.relics.HistoryFloorLiberation;

public sealed class SnowWhiteApplePageRelic : ModalPageRelic<SnowWhiteApplePageMode>
{
    internal const int StranglingVineSelectionMax = 3;
    internal const int StranglingVineBinding = 6;
    internal const int StranglingVineBindingTurns = 1;
    internal const int PoisonStingBarrierPoison = 7;
    internal const int PoisonStingBarrierTurnInterval = 5;
    internal const int PoisonStingBarrierHealPercent = 25;
    internal const int MaliceMinDamage = 60;
    internal const int MaliceMaxDamage = 120;
    internal const int MaliceMaxDamageHpThresholdPercent = 50;

    private const string HistoryFloorIconPath =
        "res://images/ui/run_history/history_floor_liberation_encounter.png";
    private const string HistoryFloorIconOutlinePath =
        "res://images/ui/run_history/history_floor_liberation_encounter_outline.png";

    private bool _poisonBarrierTriggerTurn;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override string PackedIconPath => HistoryFloorIconPath;

    protected override string PackedIconOutlinePath =>
        HistoryFloorIconOutlinePath;

    protected override string BigIconPath => HistoryFloorIconPath;

    public override bool ShowCounter =>
        Mode == SnowWhiteApplePageMode.PoisonStingBarrier;

    public override int DisplayAmount => _poisonBarrierTriggerTurn
        ? PoisonStingBarrierTurnInterval
        : PoisonBarrierTurnsSeenAcrossCombats;

    public override bool IsAllowed(IRunState runState)
    {
        _ = runState;
        return false;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)SnowWhiteApplePageMode.None),
        new CardsVar(StranglingVineSelectionMax),
        new DynamicVar("Binding", StranglingVineBinding),
        new DynamicVar("BindingTurns", StranglingVineBindingTurns),
        new DynamicVar("Poison", PoisonStingBarrierPoison),
        new DynamicVar("TurnInterval", PoisonStingBarrierTurnInterval),
        new DynamicVar("HealPercent", PoisonStingBarrierHealPercent),
        new DynamicVar("MinDamage", MaliceMinDamage),
        new DynamicVar("MaxDamage", MaliceMaxDamage),
        new DynamicVar("MaxHpThresholdPercent", MaliceMaxDamageHpThresholdPercent),
        new DynamicVar("FullHpPercent", 100)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        SnowWhiteApplePageMode.StranglingVine =>
        [
            ..HoverTipFactory.FromEnchantment<StranglingVineEnchantment>()
        ],
        SnowWhiteApplePageMode.PoisonStingBarrier =>
        [
            HoverTipFactory.FromPower<PoisonPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public SnowWhiteApplePageMode Mode { get; private set; }

    protected override SnowWhiteApplePageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshIconOnModeChange => false;

    protected override bool RefreshUiBeforeModeChoice => true;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PoisonBarrierTurnsSeenAcrossCombats { get; private set; }

    protected override async Task ApplyObtainedChoiceAsync(SnowWhiteApplePageMode mode)
    {
        SetMode(mode);
        if (Mode == SnowWhiteApplePageMode.StranglingVine)
        {
            await ApplyStranglingVineEnchantmentSelection();
        }
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        _poisonBarrierTriggerTurn = false;
        UpdateModeUiState();

        if (Mode != SnowWhiteApplePageMode.Malice
            || Owner.Creature is not { IsAlive: true } ownerCreature)
        {
            return;
        }

        IReadOnlyList<Creature> enemies = AllyTurnRegistry
            .FilterPlayerEnemyTargets(ownerCreature.CombatState?.Enemies)
            .Where(static enemy => enemy.IsAlive)
            .ToArray();
        if (enemies.Count == 0)
        {
            return;
        }

        int amount = ResolveMaliceAmount(ownerCreature);
        BlockingPlayerChoiceContext context = new();
        Flash(enemies);

        IReadOnlyList<Creature> chaoTargets = enemies
            .OfType<LibraryCreature>()
            .Where(static enemy =>
                enemy.HasChaoResistance && enemy.MaxChaoValue > 0)
            .Cast<Creature>()
            .ToArray();
        if (chaoTargets.Count > 0)
        {
            await LibraryCreatureCmd.ChaoDamage(
                context,
                chaoTargets,
                amount,
                ValueProp.Unblockable | ValueProp.Unpowered,
                ownerCreature,
                null,
                null,
                LibraryDamageType.None);
        }

        await CreatureCmdCompat.Damage(
            context,
            enemies,
            amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            ownerCreature,
            null);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Mode != SnowWhiteApplePageMode.PoisonStingBarrier
            || target != Owner.Creature
            || result.UnblockedDamage <= 0
            || dealer == null
            || !dealer.IsAlive
            || dealer.Side == target.Side
            || AllyTurnRegistry.IsFriendlyAlly(dealer)
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        Flash([dealer]);
        await PowerCmdCompat.Apply<PoisonPower>(
            dealer,
            PoisonStingBarrierPoison,
            Owner.Creature,
            null);
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        _ = choiceContext;
        if (Mode != SnowWhiteApplePageMode.PoisonStingBarrier
            || player != Owner)
        {
            return;
        }

        bool shouldTrigger = PoisonBarrierTurnsSeenAcrossCombats
            >= PoisonStingBarrierTurnInterval - 1;
        PoisonBarrierTurnsSeenAcrossCombats = shouldTrigger
            ? 0
            : PoisonBarrierTurnsSeenAcrossCombats + 1;
        _poisonBarrierTriggerTurn = shouldTrigger;
        UpdateModeUiState();

        if (!shouldTrigger
            || Owner.Creature is not { IsAlive: true } ownerCreature)
        {
            return;
        }

        decimal totalPoison = AllyTurnRegistry
            .FilterPlayerEnemyTargets(ownerCreature.CombatState?.Enemies)
            .Where(static enemy => enemy.IsAlive)
            .Sum(static enemy => enemy.GetPowerAmount<PoisonPower>());
        decimal healAmount = decimal.Floor(
            totalPoison * PoisonStingBarrierHealPercent / 100m);
        if (healAmount <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.Heal(ownerCreature, healAmount);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _ = room;
        _poisonBarrierTriggerTurn = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<SnowWhiteStranglingVineChoiceCard>(Owner),
            Owner.RunState.CreateCard<SnowWhitePoisonStingBarrierChoiceCard>(Owner),
            Owner.RunState.CreateCard<SnowWhiteMaliceChoiceCard>(Owner)
        ];
    }

    protected override void ResetStateOnModeSet(SnowWhiteApplePageMode mode)
    {
        PoisonBarrierTurnsSeenAcrossCombats = 0;
        _poisonBarrierTriggerTurn = false;
    }

    protected override void ResetStateOnFallback() => ResetStateOnModeSet(FallbackMode);

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == SnowWhiteApplePageMode.PoisonStingBarrier
            && CombatManager.Instance.IsInProgress
            && _poisonBarrierTriggerTurn
                ? RelicStatus.Active
                : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private static int ResolveMaliceAmount(Creature ownerCreature)
    {
        decimal hpRatio = ownerCreature.MaxHp > 0
            ? ownerCreature.CurrentHp / (decimal)ownerCreature.MaxHp
            : 1m;
        hpRatio = Math.Clamp(
            hpRatio,
            MaliceMaxDamageHpThresholdPercent / 100m,
            1m);
        decimal intensity = (1m - hpRatio)
            / (1m - MaliceMaxDamageHpThresholdPercent / 100m);
        return (int)decimal.Round(
            MaliceMinDamage
            + (MaliceMaxDamage - MaliceMinDamage) * intensity,
            0,
            MidpointRounding.AwayFromZero);
    }

    [AbnormalityPagePostObtainEffect(
        (int)SnowWhiteApplePageMode.StranglingVine)]
    private async Task ApplyStranglingVineEnchantmentSelection()
    {
        EnchantmentModel enchantment =
            ModelDb.Enchantment<StranglingVineEnchantment>();
        CardSelectorPrefs prefs = new(
            SelectionScreenPrompt,
            0,
            StranglingVineSelectionMax);

        IEnumerable<CardModel> selectedCards =
            await CardSelectCmd.FromDeckForEnchantment(
                Owner,
                enchantment,
                amount: 1,
                additionalFilter: static card =>
                    card?.Type == CardType.Attack,
                prefs: prefs);

        foreach (CardModel card in selectedCards)
        {
            CardCmd.Enchant<StranglingVineEnchantment>(card, 1);
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

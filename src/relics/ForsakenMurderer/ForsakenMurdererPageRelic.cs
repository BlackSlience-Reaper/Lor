using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.ForsakenMurderer;
using LibraryOfRuina.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.ForsakenMurderer;

public sealed class ForsakenMurdererPageRelic : ModalPageRelic<ForsakenMurdererPageMode>
{
    internal const int IronEchoStrengthLoss = 1;
    internal const int BoundWrathDamage = 16;
    internal const int ExtremeViolenceStrength = 2;
    internal const int ExtremeViolenceRapidWear = 1;

    protected override string IconBaseName => "forsaken_murderer_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<ForsakenMurdererPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)ForsakenMurdererPageMode.None),
        new DynamicVar("StrengthLoss", IronEchoStrengthLoss),
        new DamageVar(BoundWrathDamage, ValueProp.Unpowered),
        new DynamicVar("Strength", ExtremeViolenceStrength),
        new DynamicVar("RapidWear", ExtremeViolenceRapidWear)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        ForsakenMurdererPageMode.IronEcho =>
        [
            HoverTipFactory.FromPower<StrengthPower>()
        ],
        ForsakenMurdererPageMode.ExtremeViolence =>
        [
            HoverTipFactory.FromPower<StrengthPower>(),
            HoverTipFactory.FromPower<LibraryVulnerablePower>()
        ],
        _ => []
    };

    [SavedProperty]
    public ForsakenMurdererPageMode Mode { get; private set; }

    protected override ForsakenMurdererPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool IronEchoAvailableThisCombat { get; private set; }

    private bool _boundWrathPendingThisCombat;

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        IronEchoAvailableThisCombat = Mode == ForsakenMurdererPageMode.IronEcho;
        _boundWrathPendingThisCombat = Mode == ForsakenMurdererPageMode.BoundWrath;
        UpdateModeUiState();

        switch (Mode)
        {
            case ForsakenMurdererPageMode.BoundWrath:
                break;
            case ForsakenMurdererPageMode.ExtremeViolence:
                Flash();
                await PowerCmdCompat.Apply<StrengthPower>(Owner.Creature, ExtremeViolenceStrength, Owner.Creature, null);
                await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                    Owner.Creature,
                    ExtremeViolenceRapidWear,
                    turns: -1,
                    Owner.Creature,
                    null);
                break;
        }
    }

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Mode != ForsakenMurdererPageMode.BoundWrath
            || !_boundWrathPendingThisCombat
            || Owner.Creature.CombatState?.RoundNumber != 1)
        {
            return;
        }

        _boundWrathPendingThisCombat = false;
        CombatStateLike? combatState = Owner.Creature.CombatState;
        if (combatState == null || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        List<Creature> aliveEnemies = combatState.HittableEnemies
            .Where(static enemy => enemy.IsAlive)
            .ToList();

        if (aliveEnemies.Count == 0)
        {
            return;
        }

        Flash(aliveEnemies);
        await CreatureCmdCompat.Damage(
            choiceContext,
            aliveEnemies,
            BoundWrathDamage,
            ValueProp.Unpowered,
            Owner.Creature);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode != ForsakenMurdererPageMode.IronEcho
            || !IronEchoAvailableThisCombat
            || result.UnblockedDamage <= 0m
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !IsOwnerAttackSource(dealer, cardSource, props))
        {
            return;
        }

        IronEchoAvailableThisCombat = false;
        Flash([target]);
        UpdateModeUiState();
        await PowerCmdCompat.Apply<StrengthPower>(
            choiceContext,
            target,
            -IronEchoStrengthLoss,
            Owner.Creature,
            cardSource);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        IronEchoAvailableThisCombat = false;
        _boundWrathPendingThisCombat = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<ForsakenMurdererIronEchoChoiceCard>(Owner),
            Owner.RunState.CreateCard<ForsakenMurdererBoundWrathChoiceCard>(Owner),
            Owner.RunState.CreateCard<ForsakenMurdererExtremeViolenceChoiceCard>(Owner)
        ];
    }

    protected override void ResetStateOnModeSet(ForsakenMurdererPageMode mode)
    {
        IronEchoAvailableThisCombat = false;
        _boundWrathPendingThisCombat = false;
    }

    protected override void ResetStateOnFallback() => ResetStateOnModeSet(FallbackMode);

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            ForsakenMurdererPageMode.IronEcho when CombatManager.Instance.IsInProgress =>
                IronEchoAvailableThisCombat ? RelicStatus.Active : RelicStatus.Disabled,
            _ => RelicStatus.Normal
        };
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
}

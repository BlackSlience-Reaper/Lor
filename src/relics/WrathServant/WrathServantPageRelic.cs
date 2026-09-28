using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.WrathServant;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers.WrathServant;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.WrathServant;

public sealed class WrathServantPageRelic : ModalPageRelic<WrathServantPageMode>
{
    internal const int WrathEnergy = 3;
    internal const int WrathCards = 3;
    internal const int WrathStrong = 5;
    internal const int WrathTurns = 5;
    internal const int WrathSelfTargetTurns = 3;
    internal const int FriendEnergy = 2;
    internal const int VenomCorrosion = 7;

    protected override string IconBaseName => "wrath_servant_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<WrathServantPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)WrathServantPageMode.None),
        new EnergyVar(WrathEnergy),
        new CardsVar(WrathCards),
        new PowerVar<LibraryStrongPower>("Strong", WrathStrong),
        new DynamicVar("Turns", WrathTurns),
        new DynamicVar("SelfTargetTurns", WrathSelfTargetTurns),
        new EnergyVar("FriendEnergy", FriendEnergy),
        new DynamicVar("Corrosion", VenomCorrosion)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        WrathServantPageMode.Wrath =>
        [
            HoverTipFactory.ForEnergy(this),
            HoverTipFactory.FromPower<LibraryStrongPower>()
        ],
        WrathServantPageMode.Friend =>
        [
            HoverTipFactory.ForEnergy(this),
            HoverTipFactory.FromPower<WrathServantFriendPower>()
        ],
        WrathServantPageMode.Venom =>
        [
            HoverTipFactory.FromPower<WrathServantCorrosionPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public WrathServantPageMode Mode { get; private set; }

    protected override WrathServantPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FriendCombatId { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool MarkedFriendThisTurn { get; private set; }

    private uint? FriendPendingKillByOwnerCombatId { get; set; }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        FriendCombatId = 0;
        MarkedFriendThisTurn = false;
        FriendPendingKillByOwnerCombatId = null;
        UpdateModeUiState();

        if (Mode == WrathServantPageMode.Wrath)
        {
            Flash();
            await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), WrathCards, Owner);
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                new ThrowingPlayerChoiceContext(),
                Owner.Creature,
                WrathStrong,
                WrathTurns - 1,
                IsPermanent: false,
                Owner.Creature,
                null,
                silent: true);
        }
        else if (Mode == WrathServantPageMode.Venom)
        {
            CombatStateLike? combatState = Owner.Creature.CombatState;
            if (combatState == null)
            {
                return;
            }

            Flash();
            await PowerCmdCompat.Apply<WrathServantCorrosionPower>(
                AllyTurnRegistry.FilterPlayerEnemyTargets(combatState.Enemies)
                    .Where(static enemy => enemy.IsAlive)
                    .ToList(),
                VenomCorrosion,
                Owner.Creature,
                null);
        }
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner
            || Mode != WrathServantPageMode.Wrath
            || Owner.Creature.CombatState?.RoundNumber != 1)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            MarkedFriendThisTurn = false;
            FriendPendingKillByOwnerCombatId = null;
        }

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
        if (Mode != WrathServantPageMode.Friend || result.UnblockedDamage <= 0m)
        {
            return;
        }

        bool isOwnerAttackSource = IsOwnerAttackSource(dealer, cardSource, props);
        if (isOwnerAttackSource
            && HasOwnFriendMark(target)
            && result.WasTargetKilled)
        {
            FriendPendingKillByOwnerCombatId = target.CombatId;
        }

        if (MarkedFriendThisTurn
            || Owner.Creature.CombatState?.Creatures.Any(HasOwnFriendMark) == true
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !target.IsAlive
            || !isOwnerAttackSource)
        {
            return;
        }

        MarkedFriendThisTurn = true;
        FriendCombatId = (int?)target.CombatId ?? 0;
        Flash([target]);
        await PowerCmdCompat.Apply<WrathServantFriendPower>(target, 1m, Owner.Creature, cardSource, silent: true);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (Mode != WrathServantPageMode.Friend
            || (!HasOwnFriendMark(creature)
                && FriendPendingKillByOwnerCombatId != creature.CombatId))
        {
            return;
        }

        bool wasKilledByOwner = FriendPendingKillByOwnerCombatId == creature.CombatId;
        FriendPendingKillByOwnerCombatId = null;

        if (wasRemovalPrevented)
        {
            return;
        }

        FriendCombatId = 0;

        if (!wasKilledByOwner)
        {
            return;
        }

        Flash();

        CombatStateLike? combatState = Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        foreach (Player player in combatState.Players)
        {
            await PlayerCmd.GainEnergy(FriendEnergy, player);
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        FriendCombatId = 0;
        MarkedFriendThisTurn = false;
        FriendPendingKillByOwnerCombatId = null;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<WrathServantWrathChoiceCard>(Owner),
            Owner.RunState.CreateCard<WrathServantFriendChoiceCard>(Owner),
            Owner.RunState.CreateCard<WrathServantVenomChoiceCard>(Owner)
        ];
    }

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private bool HasOwnFriendMark(Creature target) =>
        target.GetPowerInstances<WrathServantFriendPower>()
            .Any(power => power.Applier == Owner.Creature);

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

using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.WrathServant;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers.NaturalFloorLiberation;
using LibraryOfRuina.powers.WrathServant;
using LibraryOfRuina.relics.WrathServant;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.NaturalFloorLiberation;

public sealed class WrathServantEnhancedPageRelic : EnhancedMagicalGirlPageRelic<WrathServantPageMode>
{
    // 愤怒+：首回合额外能量，沿用原书页。
    internal const int WrathEnergy = 3;

    // 愤怒+：战斗开始额外抽牌，沿用原书页。
    internal const int WrathCards = 3;

    // 愤怒+：战斗开始获得的永久强壮层数。
    internal const int WrathStrong = 5;

    // 愤怒+：永久强壮使用永久持续时间标记。
    internal const int WrathTurns = -1;

    // 愤怒+：随机和群体攻击不分敌我的开场回合数。
    internal const int WrathSelfTargetTurns = 3;

    // 朋友+：每个目标的朋友标记层数。
    internal const int FriendMarkAmount = 1;

    // 朋友+：击杀朋友时，所有玩家获得的能量。
    internal const int FriendEnergy = 3;

    // 朋友+：击杀朋友时，所有玩家抽取的牌数。
    internal const int FriendCards = 3;

    // 毒液+：战斗开始时对所有敌人施加的腐蚀层数。
    internal const int VenomCorrosion = 10;

    protected override string IconBaseName => "wrath_servant_page_relic";

    [SavedProperty]
    public WrathServantPageMode Mode { get; private set; }

    protected override WrathServantPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)WrathServantPageMode.None),
        new EnergyVar(WrathEnergy),
        new CardsVar(WrathCards),
        new PowerVar<LibraryStrongPower>("Strong", WrathStrong),
        new DynamicVar("Turns", WrathTurns),
        new DynamicVar("SelfTargetTurns", WrathSelfTargetTurns),
        new EnergyVar("FriendEnergy", FriendEnergy),
        new CardsVar("FriendCards", FriendCards),
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
            HoverTipFactory.FromPower<NihilFriendPower>()
        ],
        WrathServantPageMode.Venom =>
        [
            HoverTipFactory.FromPower<WrathServantCorrosionPower>()
        ],
        _ => []
    };

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int FriendCombatId { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool MarkedFriendThisTurn { get; private set; }

    [SavedProperty]
    public bool WrathOpeningEnergyGranted { get; private set; }

    private uint? FriendPendingKillByOwnerCombatId { get; set; }

    public override async Task BeforeCombatStart()
    {
        EnsureMode();
        WrathOpeningEnergyGranted = false;
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
                WrathTurns,
                IsPermanent: true,
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
            || WrathOpeningEnergyGranted
            || Owner.Creature.CombatState?.RoundNumber != 1)
        {
            return;
        }

        Flash();
        WrathOpeningEnergyGranted = true;
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Combat.CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == Owner.Creature.Side && participants.Contains(Owner.Creature))
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
        if (Mode != WrathServantPageMode.Friend || result.UnblockedDamage <= 0m
            || dealer == null
            || (dealer != Owner.Creature && dealer != Owner.Osty))
        {
            return;
        }

        if (!MarkedFriendThisTurn && target.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target))
        {
            MarkedFriendThisTurn = true;
            // 按施加者清理全部旧目标，避免记录失配后遗留多个朋友标记。
            NihilFriendPower[] previousMarks = Owner.Creature.CombatState!.Creatures
                .SelectMany(creature => creature.GetPowerInstances<NihilFriendPower>())
                .Where(power => power.Applier == Owner.Creature && power.Owner != target)
                .ToArray();
            foreach (NihilFriendPower power in previousMarks)
            {
                await PowerCmd.Remove(power);
            }

            FriendCombatId = (int?)target.CombatId ?? 0;
            if (target.IsAlive && !HasOwnFriendMark(target))
            {
                Flash([target]);
                await PowerCmdCompat.Apply<NihilFriendPower>(target, FriendMarkAmount,
                    Owner.Creature, cardSource, silent: true);
            }
        }

        if (target.CombatId == (uint)FriendCombatId && result.WasTargetKilled)
        {
            FriendPendingKillByOwnerCombatId = target.CombatId;
        }
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
            if (!player.Creature.IsAlive)
            {
                continue;
            }

            await PlayerCmd.GainEnergy(FriendEnergy, player);
            await CardPileCmd.Draw(choiceContext, FriendCards, player);
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

    private bool HasOwnFriendMark(Creature target) =>
        target.GetPowerInstances<NihilFriendPower>()
            .Any(power => power.Applier == Owner.Creature);

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        CreateUpgradedChoice<WrathServantWrathChoiceCard>(),
        CreateUpgradedChoice<WrathServantFriendChoiceCard>(),
        CreateUpgradedChoice<WrathServantVenomChoiceCard>()
    ];

}

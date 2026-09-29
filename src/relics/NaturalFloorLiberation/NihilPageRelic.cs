using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.NaturalFloorLiberation;
using LibraryOfRuina.combat;
using LibraryOfRuina.content.abnormalities.DespairKnight;
using LibraryOfRuina.content.abnormalities.KingOfGreed;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.relics.NaturalFloorLiberation;

public enum NihilPageMode
{
    None,
    MagicalGirls,
    Emptiness,
    Nihility
}

public sealed class NihilPageRelic : EnhancedMagicalGirlPageRelic<NihilPageMode>
{
    // 虚无：每次对所有敌人施加的虚弱层数。
    public const int NihilityWeak = 9;

    // 虚无：每次对所有敌人施加的破绽层数。
    public const int NihilityDisarm = 9;

    // 虚无：每次对所有敌人施加的易损层数。
    public const int NihilityVulnerable = 9;

    // 虚无：每次对所有敌人施加的混乱易伤层数。
    public const int NihilityBreakVulnerable = 9;

    // 虚无：四种减益持续的回合数。
    public const int NihilityTurns = 1;

    // 虚无：每个自身回合最多发动的次数。
    public const int NihilityTurnLimit = 1;

    // 虚无：每场战斗最多发动的次数。
    public const int NihilityCombatLimit = 12;

    public override string PackedIconPath =>
        "res://images/ui/run_history/natural_floor_liberation_encounter.png";

    protected override string PackedIconOutlinePath =>
        "res://images/ui/run_history/natural_floor_liberation_encounter_outline.png";

    protected override string BigIconPath => PackedIconPath;

    public override bool HasRightClick => Mode == NihilPageMode.Nihility;

    public override bool ShowCounter => Mode == NihilPageMode.Nihility;

    public override int DisplayAmount => NihilityCombatLimit - UsesThisCombat;

    [SavedProperty]
    public bool PickupEffectApplied { get; private set; }

    [SavedProperty]
    public bool NihilityEnabled { get; private set; }

    [SavedProperty]
    public int UsesThisCombat { get; private set; }

    [SavedProperty]
    public int UsesThisTurn { get; private set; }

    [SavedProperty]
    public NihilPageMode Mode { get; private set; }

    protected override NihilPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", 0),
        new DynamicVar("Weak", NihilityWeak),
        new DynamicVar("Disarm", NihilityDisarm),
        new DynamicVar("Vulnerable", NihilityVulnerable),
        new DynamicVar("BreakVulnerable", NihilityBreakVulnerable),
        new DynamicVar("Turns", NihilityTurns),
        new DynamicVar("TurnLimit", NihilityTurnLimit),
        new DynamicVar("CombatLimit", NihilityCombatLimit),
        new DynamicVar("Enabled", 0)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        NihilPageMode.Emptiness => [HoverTipFactory.FromCard<AllReturnsToVoidCard>()],
        NihilPageMode.Nihility =>
        [
            HoverTipFactory.FromPower<LibraryWeakPower>(),
            HoverTipFactory.FromPower<LibraryDisarmPower>(),
            HoverTipFactory.FromPower<LibraryVulnerablePower>(),
            HoverTipFactory.FromPower<LibraryBreakVulnerablePower>()
        ],
        _ => []
    };

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        Owner.RunState.CreateCard<NihilMagicalGirlsChoiceCard>(Owner),
        Owner.RunState.CreateCard<NihilEmptinessChoiceCard>(Owner),
        Owner.RunState.CreateCard<NihilNihilityChoiceCard>(Owner)
    ];

    protected override Task OnModeObtained() => ApplyPickupEffect();

    [AbnormalityPagePostObtainEffect]
    private async Task ApplyPickupEffect()
    {
        if (PickupEffectApplied)
        {
            return;
        }

        PickupEffectApplied = true;
        if (Mode == NihilPageMode.MagicalGirls)
        {
            foreach (RelicModel relic in Owner.Relics.Where(relic => relic is
                QueenOfHatredPageRelic or KingOfGreedPageRelic or WrathServantPageRelic or DespairKnightPageRelic)
                .ToArray())
            {
                switch (relic)
                {
                    case QueenOfHatredPageRelic hatred:
                        await ReplaceWithEnhancedPage<QueenOfHatredEnhancedPageRelic, QueenOfHatredPageMode>(hatred, hatred.Mode);
                        break;
                    case KingOfGreedPageRelic greed:
                        await ReplaceWithEnhancedPage<KingOfGreedEnhancedPageRelic, KingOfGreedPageMode>(greed, greed.Mode);
                        break;
                    case WrathServantPageRelic wrath:
                        await ReplaceWithEnhancedPage<WrathServantEnhancedPageRelic, WrathServantPageMode>(wrath, wrath.Mode);
                        break;
                    case DespairKnightPageRelic despair:
                        await ReplaceWithEnhancedPage<DespairKnightEnhancedPageRelic, DespairKnightPageMode>(despair, despair.Mode);
                        break;
                }
            }
        }
        else if (Mode == NihilPageMode.Emptiness)
        {
            CardModel card = Owner.RunState.CreateCard<AllReturnsToVoidCard>(Owner);
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
            SaveManager.Instance.MarkCardAsSeen(card);
        }
    }

    private static async Task ReplaceWithEnhancedPage<TRelic, TMode>(RelicModel original, TMode mode)
        where TRelic : EnhancedMagicalGirlPageRelic<TMode>
        where TMode : struct, System.Enum
    {
        TRelic enhanced = (TRelic)ModelDb.Relic<TRelic>().ToMutable();
        enhanced.InheritMode(mode);
        await RelicCmd.Replace(original, enhanced);
    }

    public override Task BeforeCombatStart()
    {
        EnsureMode();
        UsesThisCombat = 0;
        UsesThisTurn = 0;
        RefreshToggle();
        return Task.CompletedTask;
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        RefreshToggle();
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            UsesThisTurn = 0;
            await TryApplyNihility();
        }
    }

    public override bool CanHandleRightClickLocal(LibraryRightClickContext context) => HasRightClick;

    public override bool CanExecuteRightClick(LibraryRightClickExecutionContext context) => HasRightClick;

    public override Task OnRightClick(LibraryRightClickExecutionContext context)
    {
        if (!HasRightClick)
        {
            return Task.CompletedTask;
        }

        NihilityEnabled = !NihilityEnabled;
        RefreshToggle();
        return Task.CompletedTask;
    }

    private void RefreshToggle()
    {
        DynamicVars["Enabled"].BaseValue = NihilityEnabled ? 1 : 0;
        if (!NihilityEnabled)
        {
            Status = MegaCrit.Sts2.Core.Entities.Relics.RelicStatus.Normal;
        }
        else if (!HasAllMagicalGirlPages(Owner))
        {
            Status = MegaCrit.Sts2.Core.Entities.Relics.RelicStatus.Disabled;
        }
        else
        {
            Status = MegaCrit.Sts2.Core.Entities.Relics.RelicStatus.Active;
        }

        UpdateModeUiState();
    }

    internal static bool HasAllMagicalGirlPages(Player? player)
    {
        return player != null
            && player.Relics.Any(relic => relic is QueenOfHatredPageRelic or QueenOfHatredEnhancedPageRelic)
            && player.Relics.Any(relic => relic is KingOfGreedPageRelic or KingOfGreedEnhancedPageRelic)
            && player.Relics.Any(relic => relic is WrathServantPageRelic or WrathServantEnhancedPageRelic)
            && player.Relics.Any(relic => relic is DespairKnightPageRelic or DespairKnightEnhancedPageRelic);
    }

    private async Task TryApplyNihility()
    {
        RefreshToggle();
        if (Mode != NihilPageMode.Nihility || !NihilityEnabled
            || !HasAllMagicalGirlPages(Owner)
            || UsesThisTurn >= NihilityTurnLimit || UsesThisCombat >= NihilityCombatLimit
            || !CombatManager.Instance.IsInProgress || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState?.CurrentSide != Owner.Creature.Side)
        {
            return;
        }

        Creature[] targets = AllyTurnRegistry.FilterPlayerEnemyTargets(Owner.Creature.CombatState.Enemies)
            .Where(enemy => enemy.IsAlive).ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        UsesThisTurn++;
        UsesThisCombat++;
        RefreshToggle();
        Flash();
        foreach (Creature target in targets)
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(target, NihilityWeak,
                NihilityTurns - 1, Owner.Creature, null);
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(target, NihilityDisarm,
                NihilityTurns - 1, Owner.Creature, null);
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(target, NihilityVulnerable,
                NihilityTurns - 1, Owner.Creature, null);
            await LibraryPowerCmd.Apply<LibraryBreakVulnerablePower>(target, NihilityBreakVulnerable,
                NihilityTurns - 1, Owner.Creature, null);
        }
    }
}

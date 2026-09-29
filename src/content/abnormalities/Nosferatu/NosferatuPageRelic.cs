using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Nosferatu;

public enum NosferatuPageMode
{
    None = 0,
    Hydrophobia = 1,
    Vampirism = 2,
    Wine = 3
}

public sealed class NosferatuPageRelic : ModalPageRelic<NosferatuPageMode>
{
    internal const int HydrophobiaBleed = 3;
    internal const int VampirismDamageBonus = 5;
    internal const int VampirismHeal = 2;
    internal const int WineHeal = 12;

    protected override string IconBaseName => "nosferatu_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<NosferatuPageRelic>(runState);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)NosferatuPageMode.None),
        new DynamicVar("HydrophobiaBleed", HydrophobiaBleed),
        new DynamicVar("DamageBonus", VampirismDamageBonus),
        new HealVar("VampirismHeal", VampirismHeal),
        new HealVar("WineHeal", WineHeal)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    [SavedProperty]
    public NosferatuPageMode Mode { get; private set; }

    protected override NosferatuPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshUiBeforeModeChoice => true;

    private HashSet<uint> _wineHealedCombatIds = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _wineHealedCombatIds = [.. _wineHealedCombatIds];
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        _wineHealedCombatIds.Clear();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        return IsVampirismDamageBonusActive(target, props, dealer, cardSource)
            ? VampirismDamageBonus
            : 0m;
    }

    public override decimal ModifyChaoDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        return IsVampirismDamageBonusActive(target, props, dealer, cardSource)
            ? VampirismDamageBonus
            : 0m;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (result.UnblockedDamage <= 0m
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !IsOwnerAttackSource(dealer, cardSource, props))
        {
            return;
        }

        switch (Mode)
        {
            case NosferatuPageMode.Hydrophobia:
                Flash([target]);
                await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                    target,
                    HydrophobiaBleed,
                    Owner.Creature,
                    cardSource);
                break;
            case NosferatuPageMode.Vampirism when HasBleeding(target):
                Flash([target]);
                await CreatureCmd.Heal(Owner.Creature, VampirismHeal);
                break;
            case NosferatuPageMode.Wine when result.WasTargetKilled && HasBleeding(target):
                await HealAllPlayersForWine(target);
                break;
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _wineHealedCombatIds.Clear();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private async Task HealAllPlayersForWine(Creature killedTarget)
    {
        uint? combatId = killedTarget.CombatId;
        if (combatId == null || !_wineHealedCombatIds.Add(combatId.Value))
        {
            return;
        }

        IReadOnlyList<Creature> players = Owner.Creature.CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .OrderBy(static player => player.CombatId ?? uint.MaxValue)
            .ToArray() ?? [];
        if (players.Count == 0)
        {
            return;
        }

        Flash(players);
        foreach (Creature player in players)
        {
            await CreatureCmd.Heal(player, WineHeal);
        }
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<NosferatuHydrophobiaChoiceCard>(Owner),
            Owner.RunState.CreateCard<NosferatuVampirismChoiceCard>(Owner),
            Owner.RunState.CreateCard<NosferatuWineChoiceCard>(Owner)
        ];
    }

    protected override void ResetStateOnModeSet(NosferatuPageMode mode) => _wineHealedCombatIds.Clear();

    protected override void ResetStateOnFallback() => _wineHealedCombatIds.Clear();

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = RelicStatus.Normal;
    }

    private bool IsOwnerAttackSource(Creature? dealer, CardModel? cardSource, ValueProp props)
    {
        if (dealer == null || !ValuePropCompat.IsPoweredAttack(props))
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        return cardSource == null || cardSource.Owner == Owner;
    }

    private bool IsVampirismDamageBonusActive(
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return Mode == NosferatuPageMode.Vampirism
            && target != null
            && target.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target)
            && HasBleeding(target)
            && IsOwnerAttackSource(dealer, cardSource, props);
    }

    private static bool HasBleeding(Creature target) =>
        target.GetPower<LibraryBleedingPower>()?.Amount > 0;
}

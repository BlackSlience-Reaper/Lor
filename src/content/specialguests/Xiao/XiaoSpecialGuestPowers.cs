using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Combat.HealthBars;
using LibraryLib.Powers;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.specialguests.Xiao;

public abstract class XiaoGuestPowerBase : LibraryOfRuinaPowerModel
{
    protected abstract string PowerId { get; }

    protected abstract string IconFileName { get; }

    protected override string LegacyPowerId => PowerId;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/" + IconFileName);

    public override string ResolvedBigIconPath => PackedIconPath;
}

public sealed class XiaoStarfirePassivePower : XiaoGuestPowerBase
{
    public const int BurnStacksOnHit = 1;
    public const int StarfireDurationTurns = 2;
    public const int StarfireSpreadPercent = 10;

    protected override string PowerId => "XIAO_STARFIRE_PASSIVE_POWER";

    protected override string IconFileName => "library_passive_orange.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BurnStacks", BurnStacksOnHit),
        new DynamicVar("StarfireTurns", StarfireDurationTurns),
        new DynamicVar("SpreadPercent", StarfireSpreadPercent),
    ];
}

public sealed class XiaoEmbraceFirePassivePower : XiaoGuestPowerBase
{
    protected override string PowerId => "XIAO_EMBRACE_FIRE_PASSIVE_POWER";

    protected override string IconFileName => "library_passive_purple.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class XiaoDragonBornPassivePower : XiaoGuestPowerBase
{
    public const int BurnStacksPerRound = 3;

    protected override string PowerId => "XIAO_DRAGON_BORN_PASSIVE_POWER";

    protected override string IconFileName => "library_passive_orange.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BurnStacks", BurnStacksPerRound),
    ];
}

public sealed class XiaoPulaoBellPassivePower : XiaoGuestPowerBase
{
    public const int FirstTriggerRound = 1;
    public const int RepeatIntervalRounds = 2;

    protected override string PowerId => "XIAO_PULAO_BELL_PASSIVE_POWER";

    protected override string IconFileName => "library_passive_orange.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("FirstTurn", FirstTriggerRound),
        new DynamicVar("Interval", RepeatIntervalRounds),
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        _ = participants;
        if (!TurnParticipants.IsRoundPlayerTurn(side)
            || (combatState.RoundNumber - FirstTriggerRound)
            % RepeatIntervalRounds != 0)
        {
            return;
        }

        Creature[] creatures = combatState.PlayerCreatures
            .Concat(combatState.Enemies)
            .Where(static creature => creature.IsAlive)
            .OrderBy(static creature => creature.CombatId)
            .ToArray();
        foreach (Creature creature in creatures)
        {
            await PowerCmdCompat.Ensure<NullifyPower>(
                choiceContext,
                creature,
                1m,
                Owner,
                null,
                silent: true);
        }
    }
}

public sealed class XiaoReverseScalePassivePower : XiaoGuestPowerBase
{
    private const int CardLimit = 12;
    public const int ChaoLossOnFullBlock = 160;
    public const int FullBlockBuffDurationTurns = 1;
    public const int FullBlockBuffStacks = 2;

    protected override string PowerId => "XIAO_REVERSE_SCALE_PASSIVE_POWER";

    protected override string IconFileName => "library_passive_orange.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => GetDisplayCount(LocalContext.NetId);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("CardLimit", CardLimit),
        new DynamicVar("ChaoLoss", ChaoLossOnFullBlock),
        new DynamicVar("BuffTurns", FullBlockBuffDurationTurns),
        new DynamicVar("BuffStacks", FullBlockBuffStacks),
    ];

    public string CardsPlayedByPlayerNetId { get; private set; } = string.Empty;

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            CardsPlayedByPlayerNetId = string.Empty;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        _ = autoPlayType;
        return GetCount(card.Owner.NetId) < CardLimit;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext context,
        CardPlay cardPlay)
    {
        ulong playerNetId = cardPlay.PlayerCompat().NetId;
        int count = Math.Min(CardLimit, GetCount(playerNetId) + 1);
        SetCount(playerNetId, count);
        if (count == CardLimit)
        {
            PlayerCmd.EndTurn(cardPlay.PlayerCompat(), canBackOut: false);
        }
    }

    internal int GetDisplayCount(ulong? playerNetId) =>
        playerNetId.HasValue ? GetCount(playerNetId.Value) : 0;

    private int GetCount(ulong playerNetId)
    {
        Dictionary<ulong, int> counts = ParseCounts();
        return counts.TryGetValue(playerNetId, out int count) ? count : 0;
    }

    private void SetCount(ulong playerNetId, int value)
    {
        Dictionary<ulong, int> counts = ParseCounts();
        counts[playerNetId] = Math.Clamp(value, 0, CardLimit);
        CardsPlayedByPlayerNetId = PlayerIntMapSerializer.Format(counts);
        InvokeDisplayAmountChanged();
    }

    private Dictionary<ulong, int> ParseCounts() =>
        PlayerIntMapSerializer.ParseClamped(CardsPlayedByPlayerNetId, 0, CardLimit);
}

public sealed class XiaoAmphibiousPassivePower : XiaoGuestPowerBase
{
    protected override string PowerId => "XIAO_AMPHIBIOUS_PASSIVE_POWER";

    protected override string IconFileName => "library_passive_orange.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        _ = applier;
        modifiedAmount = amount;
        if (target != Owner
            || amount <= 0m
            || canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            return false;
        }

        modifiedAmount = 0m;
        return true;
    }

    public override decimal ModifyHpLostAfterOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner
            && dealer is { IsPlayer: true }
            && cardSource == null
            && props.HasFlag(ValueProp.Unpowered)
            && props.HasFlag(ValueProp.SkipHurtAnim))
        {
            return 0m;
        }
        return amount;
    }
}

public sealed class XiaoStarfireStatusPower : LibraryDurationPowerModel
{
    protected override string LegacyPowerId => "XIAO_STARFIRE_STATUS_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/xiao_starfire_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        base.CanonicalVars.Append(
            new DynamicVar(
                "SpreadPercent",
                XiaoStarfirePassivePower.StarfireSpreadPercent));

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        _ = choiceContext;
        _ = props;
        _ = dealer;
        _ = cardSource;
        if (target != Owner || result.UnblockedDamage <= 0 || Owner.IsDead)
        {
            return;
        }

        int spread = CalculateSpreadStacks(
            Owner.GetPower<LibraryBurnPower>()?.Amount ?? 0);
        if (spread <= 0 || Owner.CombatState == null)
        {
            return;
        }

        foreach (Creature ally in Owner.CombatState.PlayerCreatures
                     .Where(ally => ally.IsAlive && ally != Owner)
                     .OrderBy(static ally => ally.CombatId))
        {
            await PowerCmdCompat.Apply<LibraryBurnPower>(
                ally,
                spread,
                Owner,
                null);
        }
    }

    protected override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants,
        object? _ = null)
    {
        _ = choiceContext;
        _ = participants;
        if (side == CombatSide.Player && !IsPermanent)
        {
            // The normal duration hook shares its pass with Burn. Defer the
            // tick so Starfire can observe Burn damage before it expires.
            SkipNextDurationTick = true;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!TurnParticipants.IsPlayerTurnFor(Owner, side, participants) || IsPermanent)
        {
            return;
        }

        int nextTurns = TurnsRemaining - 1;
        SetTurnsRemaining(nextTurns);
        if (nextTurns <= 0)
        {
            await OnExpired(choiceContext);
        }
    }

    internal static int CalculateSpreadStacks(int burnStacks) =>
        Math.Max(0, burnStacks) / 10;
}

public sealed class XiaoIgnitePower :
    XiaoGuestPowerBase,
    ILibraryHealthBarDamageForecastSource
{
    protected override string PowerId => "XIAO_IGNITE_POWER";

    protected override string IconFileName => "xiao_ignite_power.png";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public bool TookBurnDamageThisTurn { get; private set; }

    public int BurnStackBeforeDecay { get; private set; }

    public IEnumerable<LibraryHealthBarDamageForecast>
        GetLibraryHealthBarDamageForecasts(
            LibraryHealthBarForecastContext context)
    {
        if (context.CombatState?.CurrentSide != CombatSide.Player
            || Owner.GetPower<LibraryBurnPower>() is not { Amount: > 0 } burn)
        {
            return [];
        }

        int amountAfterFirstDecay = LibraryBurnPower
            .CalculateAmountAfterDecay(burn.Amount);
        if (amountAfterFirstDecay <= 0)
        {
            return [];
        }

        return
        [
            LibraryHealthBarDamageForecast.FromLibraryPower(
                burn,
                LibraryLib.Combat.HealthBars.LibraryHealthBarForecastColors.Burn,
                amountAfterFirstDecay,
                order: 1)
        ];
    }

    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!TurnParticipants.IsPlayerTurnFor(Owner, side, participants))
        {
            return;
        }

        if (Owner.IsAlive
            && Owner.GetPower<LibraryBurnPower>() is { Amount: > 0 } burn)
        {
            await burn.TriggerEffect(choiceContext, Owner, null);
            await burn.TriggerReduce(choiceContext, Owner, null);
        }
        await PowerCmd.Remove(this);
    }
}

internal static class XiaoSpecialGuestPassiveInstaller
{
    public static async Task EnsureFor(XiaoSpecialGuestMonsterBase guest)
    {
        Creature owner = guest.Creature;
        if (guest is XiaoStageOne)
        {
            await Ensure<XiaoStarfirePassivePower>(owner);
            await Ensure<XiaoEmbraceFirePassivePower>(owner);
        }
        else if (guest is Miris)
        {
            await Ensure<XiaoEmbraceFirePassivePower>(owner);
        }
        else if (guest is XiaoEgo)
        {
            await Ensure<XiaoDragonBornPassivePower>(owner);
            await Ensure<XiaoPulaoBellPassivePower>(owner);
            await Ensure<XiaoReverseScalePassivePower>(owner);
            await Ensure<XiaoAmphibiousPassivePower>(owner);
            await Ensure<XiaoStarfirePassivePower>(owner);
            await Ensure<XiaoEmbraceFirePassivePower>(owner);
        }
    }

    private static async Task Ensure<T>(Creature owner)
        where T : PowerModel
    {
        if (owner.GetPower<T>() == null)
        {
            await PowerCmdCompat.Apply<T>(owner, 1, owner, null, silent: true);
        }
    }
}

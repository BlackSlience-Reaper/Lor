using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Models;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.content.liberation.Religion;

public sealed class ReligionFloorRevelationPower : ReligionFloorPower
{
    [SavedProperty]
    public int CardsPlayed { get; private set; }

    public override bool AllowNegative => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StatLimit", ReligionFloorRules.RevelationStatLimit),
        new DynamicVar("CardLimit", ReligionFloorRules.RevelationCardLimit)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>()
    ];

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        card.Owner != Owner.Player || CardsPlayed < ReligionFloorRules.RevelationCardLimit;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.PlayerCompat() == Owner.Player && play.IsFirstInSeries)
        {
            CardsPlayed++;
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext context,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike state)
    {
        if (side == CombatSide.Player)
        {
            CardsPlayed = 0;
        }
        return Task.CompletedTask;
    }

    public override bool TryModifyPowerAmountReceived(
        PowerModel power, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        PowerModel limitedPower = power;
        // 同时限制临时属性的回收标记，确保回合末只扣回实际获得的层数。
        if (power is ITemporaryPower temporary && power.Type == PowerType.Buff)
        {
            limitedPower = temporary.InternallyAppliedPower;
        }
        if (target == Owner && amount > 0 && IsLimitedStat(limitedPower))
        {
            int current = Owner.Powers.Where(existing => existing.Id == limitedPower.Id).Sum(static existing => existing.Amount);
            modifiedAmount = Math.Min(amount, Math.Max(0, ReligionFloorRules.RevelationStatLimit - current));
        }
        return modifiedAmount != amount;
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (PowerModel power in Owner.Powers.Where(IsLimitedStat).ToArray())
        {
            await ClampStat(new ThrowingPlayerChoiceContext(), power);
        }
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource) =>
        power.Owner == Owner && IsLimitedStat(power) ? ClampStat(context, power) : Task.CompletedTask;

    private async Task ClampStat(PlayerChoiceContext context, PowerModel power)
    {
        int excess = power.Amount - ReligionFloorRules.RevelationStatLimit;
        if (excess <= 0)
        {
            return;
        }

        // 启示截去已有临时层数时同步删去到期计划，避免过量扣除仍然保留的层数。
        if (power is LibraryTurnsPowerModel timed)
        {
            int remaining = excess;
            foreach (var entry in timed.AmountPlan.Reverse().ToArray())
            {
                int removed = Math.Min(remaining, Math.Max(0, entry.Value));
                if (removed == 0)
                {
                    continue;
                }
                int retained = entry.Value - removed;
                if (retained == 0)
                {
                    timed.AmountPlan.Remove(entry.Key);
                }
                else
                {
                    timed.AmountPlan[entry.Key] = retained;
                }
                remaining -= removed;
            }
        }
        else
        {
            int remaining = excess;
            foreach (PowerModel marker in Owner.Powers
                .Where(candidate => candidate is ITemporaryPower temporary
                    && temporary.InternallyAppliedPower.Id == power.Id && candidate.Type == PowerType.Buff)
                .ToArray())
            {
                int removed = Math.Min(remaining, Math.Max(0, marker.Amount));
                if (removed == 0)
                {
                    continue;
                }
                // 属性在下面统一扣减，标记只更新其将来的回收数量。
                marker.SetAmount(marker.Amount - removed);
                remaining -= removed;
                if (marker.ShouldRemoveDueToAmount())
                {
                    await PowerCmd.Remove(marker);
                }
            }
        }
        await PowerCmd.ModifyAmount(context, power, -excess, Owner, null);
    }

    private static bool IsLimitedStat(PowerModel power) =>
        power is StrengthPower or DexterityPower or LibraryStrongPower or LibraryEndurancePower;
}

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using static LibraryOfRuina.content.reverberation.GearChurch.GearChurchRules;

namespace LibraryOfRuina.content.reverberation.GearChurch;

public abstract class GearChurchPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => Id.Entry;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool AllowNegative => false;

    public override PowerInstanceType InstanceType => PowerInstanceType.None;

    public override string PackedIconPath => "res://images/powers/library_passive_purple.png";

    public override string ResolvedBigIconPath => PackedIconPath;

    public override Task BeforeDamageReceived(PlayerChoiceContext context, Creature target,
        decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && ValuePropCompat.IsPoweredAttack(props)
            ? GearChurchHitContext.Commit(context, target, dealer, cardSource)
            : Task.CompletedTask;
}

public sealed class EileenNuovoFabricPower : GearChurchPassivePower
{
    private sealed class AttackPlay(CardPlay play, bool protectedHit)
    {
        internal CardPlay Play { get; } = play;
        internal bool ProtectedHit { get; set; } = protectedHit;
    }

    private List<AttackPlay> _plays = [];

    public int HitsReceived { get; private set; }

    public int LastResetRound { get; private set; } = -1;

    public override string PackedIconPath => "res://images/powers/library_passive_orange.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Reduction", FabricReduction), new DynamicVar("ProtectedHits", FabricProtectedHits)];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _plays = [];
    }

    internal void ResetForRound(int round)
    {
        if (LastResetRound == round)
        {
            return;
        }
        LastResetRound = round;
        HitsReceived = 0;
        _plays.Clear();
    }

    internal void RecordHit(bool protectedHit, CardModel? source)
    {
        HitsReceived = Math.Min(FabricProtectedHits, HitsReceived + 1);
        AttackPlay? play = _plays.LastOrDefault(item => item.Play.Card == source);
        if (play != null)
        {
            play.ProtectedHit = protectedHit;
        }
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        _plays.Add(new AttackPlay(cardPlay,
            cardPlay.Card.Type == CardType.Attack && HitsReceived < FabricProtectedHits
            && (cardPlay.Target == null || cardPlay.Target == Owner)));
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext context, CardPlay cardPlay)
    {
        _plays.RemoveAll(item => item.Play == cardPlay);
        return Task.CompletedTask;
    }

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target,
        decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner || canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            return false;
        }
        GearChurchHitContext.Hit? hit = GearChurchHitContext.Find(Owner);
        bool protectedHit = hit is { Protected: true } && hit.Dealer == applier;
        if (!protectedHit && _plays.LastOrDefault() is { } active)
        {
            protectedHit = active.Play.Card.Type == CardType.Attack
                && active.Play.Player.Creature == applier && active.ProtectedHit;
        }
        if (!protectedHit)
        {
            return false;
        }
        modifiedAmount = 0;
        return true;
    }

    public override bool ShouldClearBlock(Creature creature) => creature != Owner;

    private bool IsProtected(Creature? target, ValueProp props) =>
        target == Owner && ValuePropCompat.IsPoweredAttack(props)
        && (GearChurchHitContext.Find(Owner)?.Protected ?? HitsReceived < FabricProtectedHits);

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner && ValuePropCompat.IsPoweredAttack(props) ? -FabricReduction : 0m;

    public override decimal ModifyChaoDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay, LibraryDamageType type) =>
        target == Owner && ValuePropCompat.IsPoweredAttack(props) ? -FabricReduction : 0m;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        IsProtected(target, props) ? 0m : 1m;

    public override decimal ModifyChaoDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay, LibraryDamageType type) =>
        IsProtected(target, props) ? 0m : 1m;
}

internal sealed class EileenStateVar(string name, decimal fallback, Func<ReverberationEileen, decimal> value)
    : DynamicVar(name, fallback)
{
    protected override decimal GetBaseValueForIConvertible() =>
        _owner is GearChurchPassivePower { IsMutable: true } power
        && power.Owner?.Monster is ReverberationEileen eileen
            ? value(eileen)
            : BaseValue;

    public override string ToString() =>
        GetBaseValueForIConvertible().ToString(CultureInfo.InvariantCulture);
}

public sealed class EileenFleshRebirthPower : GearChurchPassivePower
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EileenStateVar("ChaoDamage", DeathChaoDamage, static eileen => eileen.ScaledDeathChaoDamage),
        new DynamicVar("Refill", OpeningFollowers),
        new DynamicVar("Maximum", MaximumFollowers)
    ];
}

public sealed class EileenPrestigePower : GearChurchPassivePower
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new EileenStateVar("Threshold", FirstHpFloorPercent, static eileen => eileen.HpFloorPercent)];
}

public sealed class GearChurchSmokeWreathPower : GearChurchPassivePower
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Smoke", RoundSmoke)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<GearChurchSmokePower>()];
}

public sealed class GearChurchSoberSmokePower : GearChurchPassivePower
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Reduction", SoberReductionPercent), new DynamicVar("Cost", SoberSmokeCost)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<GearChurchSmokePower>()];

    private decimal Multiplier(Creature? target, ValueProp props)
    {
        if (target != Owner || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 1m;
        }
        bool hasSmoke = GearChurchHitContext.Find(Owner)?.Sober
            ?? Owner.GetPower<GearChurchSmokePower>() is { Amount: > 0 };
        return hasSmoke ? 1m - SoberReductionPercent / 100m : 1m;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) => Multiplier(target, props);

    public override decimal ModifyChaoDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay, LibraryDamageType type) => Multiplier(target, props);
}

public sealed class GearChurchSmokePower : LibraryOfRuinaPowerModel
{
    private sealed class SmokePercentVar(string name, int percentPerStack) : DynamicVar(name, 0m)
    {
        protected override decimal GetBaseValueForIConvertible() =>
            _owner is GearChurchSmokePower { IsMutable: true } smoke
                ? PercentForStacks(smoke.Amount, percentPerStack)
                : 0m;

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString(CultureInfo.InvariantCulture);
    }

    protected override string LegacyPowerId => Id.Entry;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override PowerInstanceType InstanceType => PowerInstanceType.None;

    public override string PackedIconPath => "res://images/powers/gear_church_smoke_power.png";

    public override string ResolvedBigIconPath => PackedIconPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new SmokePercentVar("Incoming", SmokeIncomingPercent),
        new SmokePercentVar("Outgoing", SmokeOutgoingPercent),
        new DynamicVar("Threshold", SmokeBonusThreshold),
        new SmokePercentVar("Bonus", SmokeBonusPercent)
    ];

    private static decimal PercentForStacks(int stacks, int percentPerStack) =>
        stacks * (decimal)percentPerStack;

    internal static decimal OutgoingMultiplier(int smoke)
    {
        int percent = SmokeOutgoingPercent;
        if (smoke >= SmokeBonusThreshold)
        {
            percent += SmokeBonusPercent;
        }
        return 1m + PercentForStacks(smoke, percent) / 100m;
    }

    // 连续命中的预览使用模拟层数；其余预览和实战读取本次命中快照或实时层数。
    private int CalculationStacks => GearChurchHitContext.Find(Owner)?.SmokeStacks ?? Amount;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!ValuePropCompat.IsPoweredAttack(props))
        {
            return 1m;
        }
        decimal multiplier = dealer == Owner ? OutgoingMultiplier(CalculationStacks) : 1m;
        if (target == Owner && Owner.GetPower<GearChurchSmokeWreathPower>() == null)
        {
            multiplier *= 1m + PercentForStacks(CalculationStacks, SmokeIncomingPercent) / 100m;
        }
        return multiplier;
    }

    public override decimal ModifyChaoDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay, LibraryDamageType type) =>
        dealer == Owner && ValuePropCompat.IsPoweredAttack(props) ? OutgoingMultiplier(CalculationStacks) : 1m;
}

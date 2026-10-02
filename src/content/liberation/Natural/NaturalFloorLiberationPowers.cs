using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

public abstract class NaturalFloorGreenPassivePower : LibraryOfRuinaPowerModel
{
    public override string PackedIconPath => ImageHelper.GetImagePath("powers/library_passive_green.png");

    public override string ResolvedBigIconPath => PackedIconPath;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

internal sealed class NaturalFloorNameVar(string name, string table, string key) : StringVar(name)
{
    public override string ToString() => new LocString(table, key).GetFormattedText();
}

public sealed class NaturalFloorHatredPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_HATRED_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DamageIncrease", 50)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<NaturalFloorBadGuyPower>()];
}

public sealed class NaturalFloorBadGuyPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_BAD_GUY_POWER";

    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("AttackerName", ModelDb.Monster<NaturalFloorLoveAndHatredBoss>().Title);
            return description;
        }
    }

    public const string CustomIconPath = NaturalFloorAssets.BadGuyPowerIcon;

    public override string PackedIconPath => CustomIconPath;

    public override string ResolvedBigIconPath => CustomIconPath;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DamageIncrease", 50)];

#if STS2_0_111_0
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    =>
        target == Owner && ValuePropCompat.IsPoweredAttack(props) ? 1.5m : 1m;
}

public sealed class NaturalFloorInversionPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_INVERSION_POWER";

    public const int MaxHysteria = 100;

    public const int BlockedGain = 2;

    public const int UnblockedLoss = 3;

    public const int MarkedCardGain = 15;

    private sealed class Data
    {
        public List<CardPlay> ActiveCards = [];
        public HashSet<CardPlay> CountedCards = [];
    }

    protected override object InitInternalData() => new Data();

    // Amount stays positive so the passive remains present at zero displayed hysteria.

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Math.Clamp(Amount - 1, 0, MaxHysteria);

    private bool IsCounting => Owner.IsAlive && Owner.Monster is NaturalFloorLoveAndHatredBoss { IsSnakeForm: false };

    private void Change(int delta)
    {
        if (IsCounting)
        {
            SetAmount(Math.Clamp(DisplayAmount + delta, 0, MaxHysteria) + 1, silent: true);
        }
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (IsCounting)
        {
            GetInternalData<Data>().ActiveCards.Add(cardPlay);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Data data = GetInternalData<Data>();
        data.ActiveCards.Remove(cardPlay);
        data.CountedCards.Remove(cardPlay);
        return Task.CompletedTask;
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer == Owner && ValuePropCompat.IsPoweredAttack(props))
        {
            Change(result.BlockedDamage * BlockedGain - result.UnblockedDamage * UnblockedLoss);
        }

        return Task.CompletedTask;
    }

    public override Task AfterDamageReceivedLate(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || !IsCounting || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props) || cardSource == null
            || dealer is not { IsPlayer: true } || !dealer.HasPower<NaturalFloorBadGuyPower>())
        {
            return Task.CompletedTask;
        }

        Data data = GetInternalData<Data>();
        CardPlay? play = data.ActiveCards.LastOrDefault(p => ReferenceEquals(p.Card, cardSource));
        if (play != null && data.CountedCards.Add(play))
        {
            Change(MarkedCardGain);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && IsCounting && DisplayAmount == MaxHysteria
            && Owner.Monster is NaturalFloorLoveAndHatredBoss boss)
        {
            await boss.TransformToSnake();
        }
    }
}

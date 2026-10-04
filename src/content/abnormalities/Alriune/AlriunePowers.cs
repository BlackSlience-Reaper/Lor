using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Alriune;

public abstract class AlriuneGreenPassive : LibraryPowerModel
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerInstanceType InstanceType => PowerInstanceType.None;

    public override bool AllowNegative => false;

    public override string PackedIconPath => ImageHelper.GetImagePath("powers/art_floor_green_passive_power.png");
}

public sealed class AlriuneAtonementCrownPower : LibraryPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.None;

    public override bool AllowNegative => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Cost", AlriuneNumbers.CrownCost)
    ];
}

public sealed class AlriuneSuffocatingAtonementPower : AlriuneGreenPassive, ILibraryAbstractModel
{
    // 每次打出及其全部 Replay 共用一次授权；嵌套打牌拥有独立序列。
    private sealed class PlaySeries(CardModel card, Player player)
    {
        public CardModel Card { get; } = card;

        public Player Player { get; } = player;

        public bool Authorized { get; set; }
    }

    private List<PlaySeries> _series = [];
    private List<(CardPlay Play, PlaySeries Series)> _active = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _series = [];
        _active = [];
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Reduction", AlriuneNumbers.NonAttackReductionPercent)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<AlriuneAtonementCrownPower>()
    ];

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Type != CardType.Attack)
        {
            return Task.CompletedTask;
        }
        PlaySeries? series = null;
        if (!cardPlay.IsFirstInSeries)
        {
            series = _series.LastOrDefault(entry => ReferenceEquals(entry.Card, cardPlay.Card) && entry.Player == cardPlay.PlayerCompat());
        }
        if (series == null)
        {
            series = new PlaySeries(cardPlay.Card, cardPlay.PlayerCompat());
            _series.Add(series);
        }
        _active.Add((cardPlay, series));
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int index = _active.FindLastIndex(entry => ReferenceEquals(entry.Play, cardPlay));
        if (index >= 0)
        {
            PlaySeries series = _active[index].Series;
            _active.RemoveAt(index);
            if (cardPlay.IsLastInSeries)
            {
                _series.Remove(series);
            }
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        // 异常中断的打牌不能将授权带入下个回合。
        _active.Clear();
        _series.Clear();
        return Task.CompletedTask;
    }

    private PlaySeries? FindSeries(CardModel card) =>
        _active.LastOrDefault(entry => ReferenceEquals(entry.Play.Card, card)).Series;

    private bool MayDamage(Creature? target, CardModel? card)
    {
        if (target != Owner || card?.Type != CardType.Attack)
        {
            return true;
        }
        PlaySeries? series = FindSeries(card);
        if (series?.Authorized == true)
        {
            return true;
        }
        Creature? player = series?.Player.Creature ?? (card.IsMutable ? card.Owner?.Creature : null);
        return player?.GetPower<AlriuneAtonementCrownPower>()?.Amount >= AlriuneNumbers.CrownCost;
    }

    private async Task Authorize(PlayerChoiceContext context, Creature target, CardModel? card)
    {
        if (target != Owner || card?.Type != CardType.Attack)
        {
            return;
        }
        PlaySeries? series = FindSeries(card);
        if (series == null || series.Authorized)
        {
            return;
        }
        AlriuneAtonementCrownPower? crown = series.Player.Creature.GetPower<AlriuneAtonementCrownPower>();
        if (crown == null || crown.Amount < AlriuneNumbers.CrownCost)
        {
            return;
        }
        // 先建立本次资格，扣完最后一层后，当前牌后续伤害和混乱伤害仍可通过。
        series.Authorized = true;
        await PowerCmdCompat.ModifyAmount(context, crown, -AlriuneNumbers.CrownCost, series.Player.Creature, card);
    }

    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        Authorize(choiceContext, target, cardSource);

    public Task BeforeChaoDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, LibraryDamageType type) =>
        Authorize(choiceContext, target, cardSource);

#if STS2_0_111_0
    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer,
        CardModel? cardSource)
#endif
    =>
        MayDamage(target, cardSource) ? decimal.MaxValue : 0m;

    public decimal ModifyChaoDamageCap(Creature? target, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay, LibraryDamageType type) =>
        MayDamage(target, cardSource) ? decimal.MaxValue : 0m;

    // LibraryHooks 会同时经过普通接口与带类型接口；仅在普通入口减一次生命伤害。
    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && cardSource?.Type != CardType.Attack)
        {
            return amount * (100 - AlriuneNumbers.NonAttackReductionPercent) / 100m;
        }
        return amount;
    }
}

public sealed class AlriuneFlowerTearsPower : AlriuneGreenPassive, ILibraryAbstractModel
{
    [SavedProperty]
    public bool TriggeredForCurrentStagger { get; set; }

    private sealed class CrownTotalVar() : DynamicVar("Crowns", AlriuneNumbers.CrownPerLivingPlayer + AlriuneNumbers.CrownExtra)
    {
        protected override decimal GetBaseValueForIConvertible() =>
            _owner is AlriuneFlowerTearsPower { IsMutable: true } power
                ? Alriune.CrownTotal(power.Owner.CombatState)
                : base.GetBaseValueForIConvertible();

        public override string ToString() => GetBaseValueForIConvertible().ToString();
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpPercent", AlriuneNumbers.FlowerTearsHpPercent),
        new CrownTotalVar()
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<AlriuneAtonementCrownPower>()
    ];

    public Task AfterCurrentChaoValueChanged(Creature target, decimal amount, LibraryDamageType type)
    {
        if (target == Owner && Owner is LibraryCreature { CurrentChaoValue: > 0 })
        {
            TriggeredForCurrentStagger = false;
        }
        return Task.CompletedTask;
    }

    public Task BeforeStun(Creature creature)
    {
        if (creature == Owner && Owner is LibraryCreature { IsChaoed: false })
        {
            TriggeredForCurrentStagger = false;
        }
        return Task.CompletedTask;
    }

    public async Task AfterStun(Creature creature)
    {
        if (creature != Owner || Owner.IsDead || TriggeredForCurrentStagger
            || Owner is not LibraryCreature { IsChaoed: true }
            || Owner.Monster is not Alriune alriune)
        {
            return;
        }
        TriggeredForCurrentStagger = true;
        Flash();
        decimal loss = Math.Floor(Owner.MaxHp * AlriuneNumbers.FlowerTearsHpPercent / 100m);
        // 直接失去体力：Cmd 负责生命变化与死亡，绕过格挡、抗性和自身减伤。
        await CreatureCmd.SetCurrentHp(Owner, Math.Max(0m, Owner.CurrentHp - loss));
        if (Owner.IsAlive)
        {
            await alriune.DistributeCrowns(new ThrowingPlayerChoiceContext());
        }
    }
}

public sealed class AlriuneDustToDustPower : LibraryFakeDeathPowerModel, ILibraryAbstractModel
{
    protected override string LegacyPowerId => "ALRIUNE_DUST_TO_DUST_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string PackedIconPath => ImageHelper.GetImagePath("powers/art_floor_green_passive_power.png");

    protected override bool HoldCombatOpenWhileFakeDead => false;

    protected override bool IsOwnerFakeDead =>
        Owner.Monster is AlriuneDustborn { AwaitingRevival: true, HasLivingAlriune: true };

    protected override bool CanEnterFakeDeath(Creature creature) =>
        creature == Owner && Owner.Monster is AlriuneDustborn { AwaitingRevival: false, HasLivingAlriune: true };

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is AlriuneDustborn dustborn
            ? dustborn.EnterFakeDeath()
            : Task.CompletedTask;

    public Task AfterStun(Creature creature)
    {
        if (creature == Owner && Owner.IsAlive && Owner is LibraryCreature { IsChaoed: true })
        {
            Flash();
            return CreatureCmd.Kill(Owner, force: true);
        }
        return Task.CompletedTask;
    }
}

public sealed class AlriuneClayDollPower : AlriuneGreenPassive, ILibraryAbstractModel
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>(),
        HoverTipFactory.FromPower<LibraryBleedingPower>()
    ];

    public decimal ModifyEffectiveAmountMultiplicative(LibraryBasePowerModel power,
        decimal amount, Creature? dealer, CardModel? cardSource) =>
        power.Owner == Owner && power is LibraryBurnPower or LibraryBleedingPower ? 0m : 1m;
}

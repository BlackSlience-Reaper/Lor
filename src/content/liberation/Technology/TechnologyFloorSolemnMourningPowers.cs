using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using ISecondaryDisplayAmountPower = LibraryOfRuina.framework.powers.ISecondaryDisplayAmountPower;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed class SolemnMourningSealPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SOLEMN_MOURNING_SEAL_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromAffliction<FuneralSealAffliction>()
            .Concat(HoverTipFactory.FromAffliction<SolemnMourningPersistentSealAffliction>());

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner.CombatState is { } combatState)
        {
            SolemnMourningPersistentSealAffliction.ClearAllPersistentSeals(combatState);
        }

        return Task.CompletedTask;
    }
}

public sealed class SolemnMourningRedemptionHandPower : LibraryOfRuinaPowerModel, ISecondaryDisplayAmountPower
{
    private sealed class Data
    {
        public int StrengthPerTrigger = 1;
        public int BuffUseCount;
        public int TriggerProgress;
    }

    // 文案的 {MaxCards}、本能力的计数上限、封印附魔的可打出判定共用这一个值；计数到上限后，
    // 同一回合里再有封印牌就不能打出。
    internal const int MaxSealedCardsPerTurn = 4;

    protected override string LegacyPowerId => "SOLEMN_MOURNING_REDEMPTION_HAND_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Amount;

    protected override object InitInternalData() => new Data();

    public bool ShowSecondaryDisplayAmount => true;

    public int SecondaryDisplayAmount => GetInternalData<Data>()?.StrengthPerTrigger ?? 1;

    public Color SecondaryDisplayAmountLabelColor => new(1f, 0.3f, 0.3f);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", 0),
        new DynamicVar("StrengthGain", 1),
        new DynamicVar("MaxCards", MaxSealedCardsPerTurn)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        DynamicVars["Threshold"].BaseValue = amount;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 封印牌开始结算时占用本回合的出牌名额（见 <see cref="SolemnMourningPersistentSealAffliction"/> 的说明）。
    /// 只在第一次打出（<c>PlayIndex == 0</c>）时占用：重放属于同一次打出，原版也不再询问 ShouldPlay。
    /// 不看救赎之手的持有者是否存活：名额限制的是玩家，能力还在场上就生效。
    /// </summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.PlayIndex != 0 || !SolemnMourningPersistentSealAffliction.IsAnySeal(cardPlay.Card))
        {
            return Task.CompletedTask;
        }

        CombatStateLike? combatState = Owner.CombatState ?? cardPlay.Card.CombatState;
        if (combatState != null)
        {
            SolemnMourningPersistentSealAffliction.SetSealedCardsAdmittedThisTurn(
                combatState,
                SolemnMourningPersistentSealAffliction.GetSealedCardsAdmittedThisTurn(combatState) + 1);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Owner.IsDead)
        {
            return;
        }

        bool isSeal = SolemnMourningPersistentSealAffliction.IsAnySeal(cardPlay.Card);
        GD.Print($"[RedemptionHand] AfterCardPlayed: card={cardPlay.Card.Id}, affliction={cardPlay.Card.Affliction?.GetType().Name ?? "null"}, isSeal={isSeal}");

        if (!isSeal)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        CombatStateLike? combatState = Owner.CombatState ?? cardPlay.Card.CombatState;
        if (combatState == null)
        {
            return;
        }

        int sealedCardsPlayed = SolemnMourningPersistentSealAffliction.GetSealedCardsPlayedThisTurn(combatState);
        if (sealedCardsPlayed >= MaxSealedCardsPerTurn)
        {
            return;
        }

        SolemnMourningPersistentSealAffliction.SetSealedCardsPlayedThisTurn(combatState, sealedCardsPlayed + 1);
        data.TriggerProgress++;

        //GD.Print($"[RedemptionHand] Sealed card played! progress={data.TriggerProgress}/{Amount}, totalThisTurn={sealedCardsPlayed + 1}");

        if (data.TriggerProgress >= Amount)
        {
            data.TriggerProgress = 0;
            Flash();
            await PowerCmdCompat.Apply<StrengthPower>(Owner, data.StrengthPerTrigger, Owner, null);
            // GD.Print($"[RedemptionHand] Triggered! Granted {data.StrengthPerTrigger} Strength to boss");
        }

        InvokeDisplayAmountChanged();
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            SolemnMourningPersistentSealAffliction.SetSealedCardsPlayedThisTurn(combatState, 0);
            SolemnMourningPersistentSealAffliction.SetSealedCardsAdmittedThisTurn(combatState, 0);
        }

        return Task.CompletedTask;
    }

    internal void EscalateFromRestingPlace()
    {
        Data data = GetInternalData<Data>();
        data.BuffUseCount++;

        switch (data.BuffUseCount)
        {
            case 1:
                data.StrengthPerTrigger++;
                break;
            case 2:
                SetAmount(Math.Max(1, Amount - 1));
                break;
            default:
                data.StrengthPerTrigger++;
                break;
        }

        DynamicVars["Threshold"].BaseValue = Amount;
        DynamicVars["StrengthGain"].BaseValue = data.StrengthPerTrigger;
        Flash();
        InvokeDisplayAmountChanged();
    }
}

public sealed class SolemnMourningSerenityPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SOLEMN_MOURNING_SERENITY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Amount;

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (amount <= 0m || power is not SolemnMourningSealOnEnemyPower || power.Owner != Owner)
        {
            return;
        }

        await TryTriggerFromSealThreshold();
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != Owner.Side || Owner.IsDead)
        {
            return;
        }

        await TryTriggerFromSealThreshold();
    }

    private async Task TryTriggerFromSealThreshold()
    {
        if (Owner.IsDead)
        {
            return;
        }

        var sealOnSelf = Owner.GetPower<SolemnMourningSealOnEnemyPower>();
        if (sealOnSelf == null || sealOnSelf.Amount < Amount)
        {
            return;
        }

        Flash();

        bool queued = false;
        if (Owner.Monster is TechnologyFloorSolemnMourningBoss boss)
        {
            queued = await boss.QueueEgoSequence();
        }

        if (queued)
        {
            await PowerCmd.Remove(sealOnSelf);
        }
    }
}

public sealed class SolemnMourningSealOnEnemyPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SOLEMN_MOURNING_SEAL_ON_ENEMY_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;
}

using System.Threading.Tasks;
using LibraryLib.Models;
using LibraryLib.Utils.Resistance;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Ozma;

public enum OzmaPageMode
{
    None = 0,
    OldPower = 1,
    Forget = 2,
    LifePowder = 3
}

public sealed class OzmaPageRelic : ModalPageRelic<OzmaPageMode>
{
    // 旧日之力：攻击牌费用增加量
    public const int CostIncrease = 1;

    // 旧日之力：攻击牌伤害与混乱伤害倍率
    public const int DamageMultiplier = 3;

    // 生命之粉：每场战斗首次免死后的恢复量
    public const int LifePowderHeal = 14;

    protected override string IconBaseName => "ozma_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<OzmaPageRelic>(runState);

    public override bool IsUsedUp =>
        Mode == OzmaPageMode.LifePowder && LifePowderUsedThisCombat;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)OzmaPageMode.None),
        new EnergyVar(CostIncrease),
        new DynamicVar("DamageMultiplier", DamageMultiplier),
        new HealVar(LifePowderHeal)
    ];

    [SavedProperty]
    public OzmaPageMode Mode { get; private set; }

    protected override OzmaPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool LifePowderUsedThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ForgetCardGranted { get; private set; }

    protected override async Task ApplyObtainedChoiceAsync(OzmaPageMode mode)
    {
        SetMode(mode);
        if (Mode == OzmaPageMode.Forget)
        {
            await GrantForgetCard();
        }
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        LifePowderUsedThisCombat = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        LifePowderUsedThisCombat = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Mode != OzmaPageMode.OldPower
            || card.Owner != Owner
            || card.Type != CardType.Attack
            || card.EnergyCost.CostsX
            || originalCost < 0m
            || !CombatManager.Instance.IsInProgress)
        {
            return false;
        }

        modifiedCost = originalCost + CostIncrease;
        return true;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return IsOldPowerAttackSource(props, dealer, cardSource)
            ? DamageMultiplier
            : 1m;
    }

    public override decimal ModifyChaoDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        return IsOldPowerAttackSource(props, dealer, cardSource)
            ? DamageMultiplier
            : 1m;
    }

    private bool IsOldPowerAttackSource(
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return Mode == OzmaPageMode.OldPower
               && (dealer == Owner.Creature || dealer == Owner.Osty)
               && cardSource?.Owner == Owner
               && cardSource.Type == CardType.Attack
               && ValuePropCompat.IsPoweredAttack(props);
    }

    public override bool ShouldDieLate(Creature creature)
    {
        return creature != Owner.Creature
               || Mode != OzmaPageMode.LifePowder
               || LifePowderUsedThisCombat;
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner.Creature
            || Mode != OzmaPageMode.LifePowder
            || LifePowderUsedThisCombat)
        {
            return;
        }

        Flash();
        LifePowderUsedThisCombat = true;
        UpdateModeUiState();
        await CreatureCmd.Heal(creature, LifePowderHeal);
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<OzmaOldPowerChoiceCard>(Owner),
            Owner.RunState.CreateCard<OzmaForgetChoiceCard>(Owner),
            Owner.RunState.CreateCard<OzmaLifePowderChoiceCard>(Owner)
        ];
    }

    [AbnormalityPagePostObtainEffect((int)OzmaPageMode.Forget)]
    private async Task GrantForgetCard()
    {
        if (ForgetCardGranted)
        {
            return;
        }

        ForgetCardGranted = true;
        CardModel forgetCard = Owner.RunState.CreateCard<OzmaForgetCard>(Owner);
        SaveManager.Instance.MarkCardAsSeen(forgetCard);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(forgetCard, PileType.Deck));
    }

    protected override void ResetStateOnFallback() => LifePowderUsedThisCombat = false;

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = IsUsedUp ? RelicStatus.Disabled : RelicStatus.Normal;
    }
}

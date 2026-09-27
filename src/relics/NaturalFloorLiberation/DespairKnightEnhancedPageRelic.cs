using System.Linq;
using System.Threading.Tasks;
using System;
using LibraryOfRuina.cards.DespairKnight;
using LibraryOfRuina.compat;
using LibraryOfRuina.combat;
using LibraryOfRuina.interop;
using LibraryOfRuina.relics.DespairKnight;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.NaturalFloorLiberation;

public sealed class DespairKnightEnhancedPageRelic : EnhancedMagicalGirlPageRelic<DespairKnightPageMode>
{
    // 加护+：第一次受到未格挡攻击后获得的永久守护层数。
    internal const int BlessingGuard = 3;

    // 加护+：守护的永久持续时间标记。
    internal const int BlessingTurns = -1;

    // 绝望+：首名敌人死亡时自身获得的永久强壮层数。
    internal const int DespairStrength = 5;

    // 绝望+：首名敌人死亡时自身获得的永久守护层数。
    internal const int DespairGuard = 3;

    // 绝望+：自身的强壮与守护使用永久持续时间。
    internal const int DespairPermanentTurns = -1;

    // 泪剑+：拾取时选择附魔的攻击牌数量，沿用原书页。
    internal const int TearSwordEnchantCards = 2;

    // 泪剑+：锋利附魔层数。
    internal const int TearSwordSharpAmount = 6;

    // 泪剑+：追加目标最大生命值伤害的百分比。
    internal const int TearSwordMaxHpPercent = 5;

    // 泪剑+：结算复用显示的最大生命值百分比。
    private const decimal TearSwordMaxHpRatio = TearSwordMaxHpPercent / 100m;

    protected override string IconBaseName => "despair_knight_page_relic";

    [SavedProperty]
    public DespairKnightPageMode Mode { get; private set; }

    [SavedProperty]
    public bool TearSwordEnchantmentApplied { get; private set; }

    protected override DespairKnightPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)DespairKnightPageMode.None),
        new DynamicVar("Guard", BlessingGuard),
        new DynamicVar("Turns", BlessingTurns),
        new DynamicVar("Strength", DespairStrength),
        new DynamicVar("PermanentGuard", DespairGuard),
        new CardsVar(TearSwordEnchantCards),
        new DynamicVar("SharpAmount", TearSwordSharpAmount),
        new DynamicVar("MaxHpPercent", TearSwordMaxHpPercent)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        DespairKnightPageMode.Blessing =>
        [
            HoverTipFactory.FromPower<LibraryProtectionPower>()
        ],
        DespairKnightPageMode.Despair =>
        [
            HoverTipFactory.FromPower<LibraryStrongPower>(),
            HoverTipFactory.FromPower<LibraryProtectionPower>()
        ],
        DespairKnightPageMode.TearSword =>
        [
            ..HoverTipFactory.FromEnchantment<Sharp>(TearSwordSharpAmount)
        ],
        _ => []
    };

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BlessingTriggeredThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool DespairTriggeredThisCombat { get; private set; }

    public override Task BeforeCombatStart()
    {
        EnsureMode();
        BlessingTriggeredThisCombat = false;
        DespairTriggeredThisCombat = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Mode != DespairKnightPageMode.Blessing
            || BlessingTriggeredThisCombat
            || target != Owner.Creature
            || result.UnblockedDamage <= 0m
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        BlessingTriggeredThisCombat = true;
        Flash();
        UpdateModeUiState();
        await LibraryPowerCmd.Apply<LibraryProtectionPower>(
            Owner.Creature, BlessingGuard, BlessingTurns, Owner.Creature, null);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (Mode != DespairKnightPageMode.Despair
            || DespairTriggeredThisCombat
            || wasRemovalPrevented
            || creature.Side == Owner.Creature.Side
            || creature.IsPlayer
            || AllyTurnRegistry.IsFriendlyAlly(creature))
        {
            return;
        }

        DespairTriggeredThisCombat = true;
        Flash();

        Creature ownerCreature = Owner.Creature;
        if (ownerCreature.IsAlive)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                ownerCreature,
                DespairStrength,
                turns: DespairPermanentTurns,
                Owner.Creature,
                null,
                silent: true);
            await LibraryPowerCmd.Apply<LibraryProtectionPower>(
                ownerCreature,
                DespairGuard,
                turns: DespairPermanentTurns,
                Owner.Creature,
                null,
                silent: true);
        }

        UpdateModeUiState();
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode != DespairKnightPageMode.TearSword
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || target.IsDead
            || result.UnblockedDamage <= 0m
            || cardSource?.Enchantment is not Sharp
            || !IsOwnerAttackCardSource(dealer, cardSource, props))
        {
            return;
        }

        int extraDamage = (int)Math.Ceiling(target.MaxHp * TearSwordMaxHpRatio);
        Flash([target]);
        await CreatureCmdCompat.Damage(
            choiceContext,
            target,
            extraDamage,
            ValueProp.Unpowered,
            Owner.Creature,
            null);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        BlessingTriggeredThisCombat = false;
        DespairTriggeredThisCombat = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        CreateUpgradedChoice<DespairKnightBlessingChoiceCard>(),
        CreateUpgradedChoice<DespairKnightDespairChoiceCard>(),
        CreateUpgradedChoice<DespairKnightTearSwordChoiceCard>()
    ];

    private static DespairKnightPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            DespairKnightBlessingChoiceCard => DespairKnightPageMode.Blessing,
            DespairKnightDespairChoiceCard => DespairKnightPageMode.Despair,
            DespairKnightTearSwordChoiceCard => DespairKnightPageMode.TearSword,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    protected override DespairKnightPageMode ResolveChoice(CardModel card) =>
        ResolveModeFromChoiceCard(card);

    protected override Task OnModeObtained() => ApplyTearSwordOnPickup();

    [AbnormalityPagePostObtainEffect((int)DespairKnightPageMode.TearSword)]
    private async Task ApplyTearSwordOnPickup()
    {
        if (Mode != DespairKnightPageMode.TearSword || TearSwordEnchantmentApplied)
        {
            return;
        }

        // 继承泪剑模式时同样执行强化附魔；获得钩子与奖励后处理共用一次性标记。
        TearSwordEnchantmentApplied = true;
        await ApplyTearSwordEnchantmentSelection();
    }

    private async Task ApplyTearSwordEnchantmentSelection()
    {
        Sharp canonicalSharp = ModelDb.Enchantment<Sharp>();
        int targetCount = PileType.Deck.GetPile(Owner).Cards.Count(canonicalSharp.CanEnchant);
        int selectionCount = Math.Min(TearSwordEnchantCards, targetCount);
        if (selectionCount <= 0)
        {
            return;
        }

        CardSelectorPrefs prefs = new(CardSelectorPrefs.EnchantSelectionPrompt, selectionCount)
        {
            Cancelable = false
        };
        IEnumerable<CardModel> selectedCards = await CardSelectCmd.FromDeckForEnchantment(
            Owner,
            canonicalSharp,
            TearSwordSharpAmount,
            prefs);

        foreach (CardModel card in selectedCards)
        {
            CardCmd.Enchant(canonicalSharp.ToMutable(), card, TearSwordSharpAmount);
            CardCmd.Preview(card);
        }
    }

    private bool IsOwnerAttackCardSource(Creature? dealer, CardModel? cardSource, ValueProp props)
    {
        if (dealer == null || cardSource == null || !ValuePropCompat.IsPoweredAttack(props))
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        return cardSource.Owner == Owner && cardSource.Type == CardType.Attack;
    }
}

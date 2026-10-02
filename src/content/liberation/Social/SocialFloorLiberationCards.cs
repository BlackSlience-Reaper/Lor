using System;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.content.liberation.Social;

/// <summary>
/// Minimal boss seam for Magical Powder. False Throne implements this interface;
/// the combat-only card stays independent from the encounter implementation.
/// </summary>
public interface ISocialFloorMagicalPowderTarget
{
    Task TransformFromMagicalPowder(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay);
}

[CardPool(typeof(TokenCardPool))]
public sealed class SocialFloorCourageCard() : CardModel(0,
    CardType.Skill,
    CardRarity.Rare,
    TargetType.Self,
    shouldShowInCardLibrary: false)
{
    public override CardPoolModel VisualCardPool =>
        ModelDb.CardPool<ColorlessCardPool>();

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            "packed/card_portraits/colorless/social_floor_courage_card.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar("EnergyMaximum", SocialFloorCouragePower.EnergyMaximum),
        new PowerVar<LibraryStrongPower>(
            "Strong",
            SocialFloorCouragePower.StrongStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromPower<SocialFloorCouragePower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.PlayerCompat().Creature.CombatState?.Encounter is
            SocialFloorLiberationEncounter encounter)
        {
            encounter.MarkCouragePlayed(cardPlay.PlayerCompat().NetId);
        }
        SocialFloorCouragePower? power =
            await SocialFloorPlayerMechanics.ApplyCourage(
                cardPlay.PlayerCompat(),
                this);
        if (power != null)
        {
            await power.ActivateImmediately(choiceContext, this);
        }
    }

}

[CardPool(typeof(TokenCardPool))]
public sealed class SocialFloorMagicalPowderCard() : CardModel(DefaultInternalCost,
    CardType.Attack,
    CardRarity.Rare,
    TargetType.AnyEnemy,
    shouldShowInCardLibrary: false)
{
    public const int DefaultInternalCost = 10;

    [SavedProperty]
    public int InternalCost { get; private set; } = DefaultInternalCost;

    [SavedProperty]
    public string SerializedHolderNetId { get; private set; } = "0";

    public ulong HolderNetId =>
        SocialFloorPlayerMechanics.ParseHolderNetId(SerializedHolderNetId);

    [SavedProperty]
    public bool IsHolderValid { get; private set; } = true;

    public bool IsPowderReady => IsHolderValid && InternalCost == 0;

    public override CardPoolModel VisualCardPool =>
        ModelDb.CardPool<ColorlessCardPool>();

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            "packed/card_portraits/colorless/social_floor_magical_powder_card.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar("InternalCost", DefaultInternalCost)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
        
    ];

    protected override bool IsPlayable => IsPowderReady;

    protected override bool ShouldGlowGoldInternal => IsPowderReady;

    internal static bool IsPowderTarget(Creature? target) =>
        target is { IsAlive: true }
        && target.Monster is ISocialFloorMagicalPowderTarget;

    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        if (ReferenceEquals(card, this))
        {
            modifiedCost = InternalCost;
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    public void ResetForHolder(Player holder, int initialCost = DefaultInternalCost)
    {
        ArgumentNullException.ThrowIfNull(holder);
        SerializedHolderNetId =
            SocialFloorPlayerMechanics.FormatHolderNetId(holder.NetId);
        IsHolderValid = true;
        SetInternalCost(initialCost);
    }

    public void AdjustInternalCost(int offset)
    {
        if (!IsHolderValid || offset == 0)
        {
            return;
        }

        long adjusted = (long)InternalCost + offset;
        SetInternalCost((int)Math.Clamp(adjusted, 0L, int.MaxValue));
    }

    public void Invalidate()
    {
        if (!IsHolderValid)
        {
            return;
        }

        IsHolderValid = false;
        InvokeEnergyCostChanged();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (!IsPowderReady
            || cardPlay.PlayerCompat().NetId != HolderNetId
            || cardPlay.Target?.Monster is not ISocialFloorMagicalPowderTarget target)
        {
            return;
        }

        await target.TransformFromMagicalPowder(choiceContext, cardPlay);
    }

    public override Task AfterCardGeneratedForCombat(
        CardModel card,
        Player? creator)
    {
        if (card is SocialFloorMagicalPowderCard powder)
        {
            powder.RefreshInternalCostDisplay();
        }

        return Task.CompletedTask;
    }

    protected override void AfterDeserialized()
    {
        base.AfterDeserialized();
        RefreshInternalCostDisplay();
    }

    private void SetInternalCost(int value)
    {
        InternalCost = Math.Max(0, value);
        RefreshInternalCostDisplay();
    }

    private void RefreshInternalCostDisplay()
    {
        DynamicVars["InternalCost"].BaseValue = InternalCost;
        InvokeEnergyCostChanged();
    }
}

/// <summary>
/// 魔法粉末显示和支付的费用始终是 InternalCost。卡牌自己的 TryModifyEnergyCostInCombatLate 已经压过原版的
/// 玩家侧 Late 修正；这个后缀再压过排在这张牌之后的监听者和其他模组的后缀。能否打出由 IsPlayable 单独把关。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyCostInCombat))]
[HarmonyPriority(Priority.Last)]
[LibraryPatch(Reason = "卡面规则是费用只能由奥兹玛计数改变，原版 Late 一遍按监听者顺序执行，不能保证本卡排在最后；只作用于 SocialFloorMagicalPowderCard。")]
internal static class SocialFloorMagicalPowderCostPatch
{
    private static void Postfix(CardModel card, ref decimal __result)
    {
        if (card is SocialFloorMagicalPowderCard powder)
        {
            __result = powder.InternalCost;
        }
    }
}

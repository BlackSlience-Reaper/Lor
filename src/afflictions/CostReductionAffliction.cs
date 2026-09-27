using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.afflictions;

public sealed class LibraryOfRuinaCostReductionAffliction : AfflictionModel
{
    // 费用降低侵蚀：每层减少的基础攻击伤害。
    public const decimal DamageReductionPerStack = 3m;

    // 费用降低侵蚀：每层减少的基础格挡。
    public const decimal BlockReductionPerStack = 3m;

    public override bool IsStackable => true;

    public override bool HasExtraCardText => true;

    public override bool CanAfflictCardType(CardType cardType) =>
        cardType is CardType.Attack or CardType.Skill;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card != Card)
        {
            modifiedCost = originalCost;
            return false;
        }

        modifiedCost = originalCost - Amount;
        return true;
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (cardSource != Card)
        {
            return 0m;
        }

        return -DamageReductionPerStack * Amount;
    }

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (cardSource != Card)
        {
            return 0m;
        }

        return -BlockReductionPerStack * Amount;
    }
}

[HarmonyPatch(typeof(AfflictionModel), nameof(AfflictionModel.DynamicDescription), MethodType.Getter)]
internal static class CostReductionDescriptionVarsPatch
{
    private static void Postfix(AfflictionModel __instance, LocString __result)
    {
        AddReductionVars(__instance, __result);
    }

    internal static void AddReductionVars(AfflictionModel affliction, LocString? description)
    {
        if (affliction is LibraryOfRuinaCostReductionAffliction && description != null)
        {
            description.Add("DamageReduction", LibraryOfRuinaCostReductionAffliction.DamageReductionPerStack);
            description.Add("BlockReduction", LibraryOfRuinaCostReductionAffliction.BlockReductionPerStack);
        }
    }
}

[HarmonyPatch(typeof(AfflictionModel), nameof(AfflictionModel.DynamicExtraCardText), MethodType.Getter)]
internal static class CostReductionExtraCardTextVarsPatch
{
    private static void Postfix(AfflictionModel __instance, LocString? __result)
    {
        CostReductionDescriptionVarsPatch.AddReductionVars(__instance, __result);
    }
}

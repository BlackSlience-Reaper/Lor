using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Literature;

public abstract class LiteratureFloorGreenPassivePower :
    LibraryOfRuinaPowerModel
{
    private const string GreenPassiveIcon =
        "powers/library_passive_green.png";

    public override string PackedIconPath =>
        ImageHelper.GetImagePath(GreenPassiveIcon);

    public override string ResolvedBigIconPath => PackedIconPath;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class LiteratureFloorLaetitiaSuperGiftPassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int Interval = 3;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_LAETITIA_SUPER_GIFT_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Interval", Interval)];
}

public sealed class LiteratureFloorLaetitiaPlayWithMePassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int BlockReductionPercentPerGift = 20;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_LAETITIA_PLAY_WITH_ME_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar(
                "BlockReductionPercent",
                BlockReductionPercentPerGift)
        ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromCardWithCardHoverTips<LeticiaGift>();
}

public sealed class LiteratureFloorLaetitiaLonelyPassivePower :
    LiteratureFloorGreenPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_LAETITIA_LONELY_PASSIVE_POWER";
}

public sealed class LiteratureFloorGiftBoxBoomPassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int SurvivalTurns = 3;
    public const int GiftCards = 3;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_GIFT_BOX_BOOM_PASSIVE_POWER";

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("Turns", SurvivalTurns),
            new DynamicVar("GiftCards", GiftCards)
        ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromCardWithCardHoverTips<LeticiaGift>();

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy
            || Owner.IsDead
            || Owner.Monster is not LiteratureFloorSurpriseGiftBox)
        {
            return Task.CompletedTask;
        }

        SetAmount(Math.Max(0, Amount - 1));
        return Task.CompletedTask;
    }
}

public sealed class LiteratureFloorSurpriseAppearancePassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int SuppressedFriendHpLossPercent = 50;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_SURPRISE_APPEARANCE_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar(
                "HpLossPercent",
                SuppressedFriendHpLossPercent)
        ];

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}

public sealed class LiteratureFloorLittleWitchFriendHandItOverPassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int DamagePerGift = 1;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_LITTLE_WITCH_FRIEND_HAND_IT_OVER_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("DamagePerGift", DamagePerGift)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromCardWithCardHoverTips<LeticiaGift>();

#if STS2_0_111_0
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
#else
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        if (dealer != Owner || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 0m;
        }

        return LiteratureFloorGiftHandMetrics.MaxGiftCount(Owner.CombatState)
            * DamagePerGift;
    }
}

internal static class LiteratureFloorGiftHandMetrics
{
    internal static int MinGiftCountFromCounts(IEnumerable<int> counts)
    {
        int[] normalized = counts.Select(static count =>
            Math.Max(0, count)).ToArray();
        return normalized.Length == 0 ? 0 : normalized.Min();
    }

    internal static int MaxGiftCountFromCounts(IEnumerable<int> counts)
    {
        int[] normalized = counts.Select(static count =>
            Math.Max(0, count)).ToArray();
        return normalized.Length == 0 ? 0 : normalized.Max();
    }

    internal static int MinGiftCount(CombatStateLike? combatState)
    {
        return MinGiftCountFromCounts(GiftCounts(combatState));
    }

    internal static int MaxGiftCount(CombatStateLike? combatState)
    {
        return MaxGiftCountFromCounts(GiftCounts(combatState));
    }

    private static IEnumerable<int> GiftCounts(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            yield break;
        }

        foreach (Creature player in combatState.LivingPlayerCreatures())
        {
            if (player.Player == null)
            {
                continue;
            }

            yield return PileType.Hand
                .GetPile(player.Player)
                .Cards
                .Count(static card => card is LeticiaGift);
        }
    }
}

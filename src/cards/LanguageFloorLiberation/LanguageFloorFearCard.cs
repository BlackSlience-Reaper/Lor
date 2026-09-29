using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.LanguageFloorLiberation;

[CardPool(typeof(StatusCardPool))]
public sealed class LanguageFloorFearCard : CardModel
{
    public const int HpLoss = 3;

    public LanguageFloorFearCard()
        : base(
            -1,
            CardType.Status,
            CardRarity.Status,
            TargetType.None)
    {
    }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override bool HasTurnEndInHandEffect => true;

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            "packed/card_portraits/status/language_floor_fear_card.png");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Unplayable];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new HpLossVar(HpLoss)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromKeyword(CardKeyword.Unplayable)];

    protected override async Task OnTurnEndInHand(
        PlayerChoiceContext choiceContext)
    {
        await CreatureCmdCompat.Damage(
            choiceContext,
            Owner.Creature,
            DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature,
            this);
    }

    public override Task AfterCardGeneratedForCombat(
        CardModel card,
        Player? creator)
    {
        if (card is LanguageFloorFearCard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}

[HarmonyPatch(
    typeof(CardModel),
    nameof(CardModel.OnTurnEndInHandWrapper))]
internal static class LanguageFloorFearCardTurnEndPilePatch
{
    private static void Postfix(
        CardModel __instance,
        ref Task __result)
    {
        if (__instance is LanguageFloorFearCard fear)
        {
            __result = ShuffleAfterNativeTurnEnd(
                fear,
                __result);
        }
    }

    private static async Task ShuffleAfterNativeTurnEnd(
        LanguageFloorFearCard fear,
        Task original)
    {
        await original;
        if (fear.Pile?.Type != PileType.Discard
            || fear.Owner.Creature.IsDead
            || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        await CardPileCmd.Add(
            fear,
            PileType.Draw,
            CardPilePosition.Random,
            clonedBy: null,
            skipVisuals: false);
    }
}

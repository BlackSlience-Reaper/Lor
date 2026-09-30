using LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;
using LibraryOfRuina.infra.lifecycle;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Technology;

public sealed class SolemnMourningPersistentSealAffliction : AfflictionModel
{
    // 本回合已计入救赎之手的封印牌数，全队共用一个计数，玩家回合开始时由救赎之手清零。
    // 按战斗状态存放：同一场战斗里两端按同样的出牌顺序累加；读档重开的战斗、之后的战斗和另一局都从 0 开始，
    // 不会读到上一场没有清零（例如战斗中途退出）留下的值。
    private static readonly CombatScoped<CombatStateLike, int> SealedCardsPlayedThisTurn = new();

    internal static int GetSealedCardsPlayedThisTurn(CombatStateLike? combatState) =>
        SealedCardsPlayedThisTurn.GetValueOrDefault(combatState, 0);

    internal static void SetSealedCardsPlayedThisTurn(CombatStateLike combatState, int count) =>
        SealedCardsPlayedThisTurn.Set(combatState, count);

    // 本回合已获准打出的封印牌数（出牌名额），全队共用，玩家回合开始时由救赎之手清零。
    // 名额在出牌开始时（BeforeCardPlayed）就占用，而不是等结算后：原版的嵌套出牌（例如被封印的破灭自动打出抽牌堆顶的封印牌）
    // 在外层牌结算前就会问内层牌能不能打，只按结算后的计数判断会让两张牌都看到同一个旧值，一回合打出第 5 张。
    // 力量进度仍按结算后的 SealedCardsPlayedThisTurn 累加。
    private static readonly CombatScoped<CombatStateLike, int> SealedCardsAdmittedThisTurn = new();

    internal static int GetSealedCardsAdmittedThisTurn(CombatStateLike? combatState) =>
        SealedCardsAdmittedThisTurn.GetValueOrDefault(combatState, 0);

    internal static void SetSealedCardsAdmittedThisTurn(CombatStateLike combatState, int count) =>
        SealedCardsAdmittedThisTurn.Set(combatState, count);

    public override bool HasExtraCardText => true;

    public override bool CanAfflict(CardModel card) => base.CanAfflict(card);

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card != Card) return true;
        // 原版 CanPlay 与自动打出都经 Hook.ShouldPlay 问到这里；名额由救赎之手在出牌开始时占用、玩家回合开始时清零，
        // 两端按同样的出牌顺序得到同样的结果。原版的多次打出（重放）不再询问 ShouldPlay，也不另占名额；
        // 直接调用 OnPlayWrapper 的强制打出同样绕过这里。
        return GetSealedCardsAdmittedThisTurn(card.CombatState)
               < SolemnMourningRedemptionHandPower.MaxSealedCardsPerTurn;
    }

    internal static bool IsPersistentSeal(CardModel card) =>
        card.Affliction is SolemnMourningPersistentSealAffliction;

    internal static void ClearPersistentSeal(CardModel card)
    {
        if (IsPersistentSeal(card))
        {
            CardCmd.ClearAffliction(card);
        }
    }

    internal static void ClearAllPersistentSeals(CombatStateLike combatState)
    {
        foreach (CardModel card in GetAllPlayerCards(combatState))
        {
            ClearPersistentSeal(card);
        }
    }

    internal static bool IsAnySeal(CardModel card) =>
        FuneralSealAffliction.IsSeal(card) || IsPersistentSeal(card);

    private static IEnumerable<CardModel> GetAllPlayerCards(CombatStateLike combatState)
    {
        foreach (var player in combatState.Players)
        {
            if (player.PlayerCombatState == null)
            {
                continue;
            }

            foreach (CardModel card in player.PlayerCombatState.Hand.Cards)
            {
                yield return card;
            }

            foreach (CardModel card in player.PlayerCombatState.DrawPile.Cards)
            {
                yield return card;
            }

            foreach (CardModel card in player.PlayerCombatState.DiscardPile.Cards)
            {
                yield return card;
            }

            foreach (CardModel card in player.PlayerCombatState.ExhaustPile.Cards)
            {
                yield return card;
            }
        }
    }
}

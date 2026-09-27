using System;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.compat;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.monsters.CosmicFragment;

[CardPool(typeof(StatusCardPool))]
public sealed class CosmicFragmentEpiphanyCard : CardModel
{
    private const int BaseEffectAmount = 1;

    private static readonly Dictionary<CombatStateLike, int> TriggeredCombatRounds = [];

    [ThreadStatic]
    private static bool _cosmicFragmentUpgradeInProgress;

    public override int MaxUpgradeLevel => int.MaxValue;

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/status/cosmic_fragment_epiphany_card.png");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Unplayable
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<DexterityPower>("Dexterity", BaseEffectAmount),
        new PowerVar<StrengthPower>("StrengthLoss", BaseEffectAmount)
    ];

    public override bool HasTurnEndInHandEffect => true;

    internal static bool IsCosmicFragmentUpgradeInProgress => _cosmicFragmentUpgradeInProgress;

    public CosmicFragmentEpiphanyCard()
        : base(-1, CardType.Status, CardRarity.Status, TargetType.None)
    {
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int dexterityAmount = DynamicVars["Dexterity"].IntValue;
        int strengthLossAmount = DynamicVars["StrengthLoss"].IntValue;
        await PowerCmdCompat.Apply<DexterityPower>(choiceContext, Owner.Creature, dexterityAmount, Owner.Creature, this);
        await PowerCmdCompat.Apply<StrengthPower>(choiceContext, Owner.Creature, -strengthLossAmount, Owner.Creature, this);

        CombatStateLike? combatState = Owner.Creature.CombatState;
        if (combatState != null)
        {
            TriggeredCombatRounds[combatState] = combatState.RoundNumber;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Dexterity"].UpgradeValueBy(1m);
        DynamicVars["StrengthLoss"].UpgradeValueBy(1m);
    }

    public static bool ConsumeTriggeredThisTurn(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return false;
        }

        if (!TriggeredCombatRounds.TryGetValue(combatState, out int triggeredRound))
        {
            return false;
        }

        TriggeredCombatRounds.Remove(combatState);
        return triggeredRound == combatState.RoundNumber;
    }

    public static void ResetTriggeredThisTurn(CombatStateLike? combatState)
    {
        if (combatState != null)
        {
            TriggeredCombatRounds.Remove(combatState);
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTriggeredThisTurn(room.CombatState);
        return Task.CompletedTask;
    }

    public static void UpgradeFromCosmicFragment(CardModel card)
    {
        if (card is not CosmicFragmentEpiphanyCard)
        {
            return;
        }

        bool previous = _cosmicFragmentUpgradeInProgress;
        _cosmicFragmentUpgradeInProgress = true;
        try
        {
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
        }
        finally
        {
            _cosmicFragmentUpgradeInProgress = previous;
        }
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is CosmicFragmentEpiphanyCard)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }

        return Task.CompletedTask;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsUpgradable), MethodType.Getter)]
internal static class CosmicFragmentEpiphanyCardUpgradeFilterPatch
{
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (__instance is CosmicFragmentEpiphanyCard
            && !CosmicFragmentEpiphanyCard.IsCosmicFragmentUpgradeInProgress)
        {
            __result = false;
        }
    }
}

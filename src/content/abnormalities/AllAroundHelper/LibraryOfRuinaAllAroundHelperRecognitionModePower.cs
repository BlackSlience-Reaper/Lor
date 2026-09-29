using System.Threading.Tasks;
using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.abnormalities.AllAroundHelper;

public sealed class LibraryOfRuinaAllAroundHelperRecognitionModePower : LibraryOfRuinaPowerModel
{
    private sealed class Data
    {
        public int CardsPlayedForTrigger;
        public int DisplayedCardsPlayed;
    }

    private sealed class CardsVar : DynamicVar
    {
        public CardsVar() : base("Cards", BaseCardsPerTrigger)
        {
        }
    }

    private const int BaseCardsPerTrigger = 6;
    private const int NextTurnStrengthGain = 1;
    

    protected override string LegacyPowerId => "ALL_AROUND_HELPER_RECOGNITION_MODE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount =>
        IsMutable
            ? GetInternalData<Data>().DisplayedCardsPlayed
            : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(),
        new PowerVar<LibraryOfRuinaNextTurnStrength>("NextTurnStrength", NextTurnStrengthGain)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaNextTurnStrength>()
    ];

    private int CardsRequiredForCurrentCombat => ResolveCardsRequiredForCombatState(CombatState);

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        int cardsRequired = ResolveCardsRequiredForCombatState(target.CombatState);
        UpdateCardsRequiredText(cardsRequired);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Owner.IsDead || !cardPlay.Card.Owner.Creature.IsPlayer)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        data.CardsPlayedForTrigger++;
        int cardsRequired = CardsRequiredForCurrentCombat;
        data.DisplayedCardsPlayed = (data.DisplayedCardsPlayed + 1) % cardsRequired;
        InvokeDisplayAmountChanged();
        UpdateCardsRequiredText(cardsRequired);
        if (data.CardsPlayedForTrigger < cardsRequired)
        {
            return;
        }

        data.CardsPlayedForTrigger -= cardsRequired;
        Flash();
        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Owner, NextTurnStrengthGain, Owner, null);

        if (Owner.Monster is AllAroundHelper helper)
        {
            helper.QueueRecognizedMoonText();
        }
    }

    private void UpdateCardsRequiredText(int cardsRequired)
    {
        DynamicVars["Cards"].BaseValue = cardsRequired;
    }

    private static int ResolveCardsRequiredForCombatState(CombatStateLike? combatState)
    {
        int playerCount = combatState?.Players.Count ?? 1;
        return ResolveCardsRequiredForPlayerCount(playerCount);
    }

    private static int ResolveCardsRequiredForPlayerCount(int playerCount)
    {
        return playerCount switch
        {
            <= 1 => 6,
            2 => 7,
            3 => 8,
            _ => 10
        };
    }
}

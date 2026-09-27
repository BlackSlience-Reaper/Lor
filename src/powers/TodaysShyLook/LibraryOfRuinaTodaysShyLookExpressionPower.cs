using System.Threading.Tasks;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.powers.TodaysShyLook;

public sealed class LibraryOfRuinaTodaysShyLookExpressionPower : LibraryOfRuinaPowerModel
{
    private sealed class Data
    {
        public int CardsPlayedRemainder;
    }

    private sealed class CardsVar : DynamicVar
    {
        public CardsVar() : base("Cards", CardsPerSwitch)
        {
        }
    }

    private const int CardsPerSwitch = 3;

    protected override string LegacyPowerId => "TODAYS_SHY_LOOK_EXPRESSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount =>
        IsMutable ? GetInternalData<Data>().CardsPlayedRemainder : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar()
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        int cardsRequired = ResolveCardsRequiredForPlayerCount(
            MultiplayerScalingPatchHelper.ResolveRunPlayerCount(target.CombatState));
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
        int cardsRequired = ResolveCardsRequiredForPlayerCount(
            MultiplayerScalingPatchHelper.ResolveRunPlayerCount(CombatState));
        UpdateCardsRequiredText(cardsRequired);

        data.CardsPlayedRemainder = (data.CardsPlayedRemainder + 1) % cardsRequired;
        InvokeDisplayAmountChanged();

        if (data.CardsPlayedRemainder != 0)
        {
            return;
        }

        Flash();
        if (Owner.Monster is monsters.TodaysShyLook.TodaysShyLook todaysShyLook)
        {
            await todaysShyLook.SwitchExpressionFromPower();
        }
    }

    private void UpdateCardsRequiredText(int cardsRequired)
    {
        DynamicVars["Cards"].BaseValue = cardsRequired;
    }

    private static int ResolveCardsRequiredForPlayerCount(int playerCount)
    {
        decimal multiplier = playerCount switch
        {
            <= 1 => 1m,
            2 => 1.5m,
            3 => 2m,
            _ => 3m
        };

        return (int)decimal.Ceiling(CardsPerSwitch * multiplier);
    }
}

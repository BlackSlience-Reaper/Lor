using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class AllReturnsToVoidPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ALL_RETURNS_TO_VOID_POWER";

    public override string PackedIconPath => ModelDb.Power<NaturalFloorNihilPower>().PackedIconPath;

    public override string ResolvedBigIconPath => PackedIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", AllReturnsToVoidCard.BuffTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || !Owner.IsAlive)
        {
            return;
        }

        int handCount = PileType.Hand.GetPile(player).Cards.Count;
        if (handCount == 0)
        {
            return;
        }

        CardSelectorPrefs prefs = new(CardSelectorPrefs.DiscardSelectionPrompt, 0, handCount);
        CardModel[] selected = (await CardSelectCmd.FromHandForDiscard(
            choiceContext, player, prefs, null, this)).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        await CardCmd.DiscardAndDraw(choiceContext, selected, selected.Length);
        int stacks = selected.Length * Amount;
        await LibraryPowerCmd.Apply<LibraryStrongPower>(Owner, stacks,
            AllReturnsToVoidCard.BuffTurns - 1, Owner, null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(Owner, stacks,
            AllReturnsToVoidCard.BuffTurns - 1, Owner, null);
    }
}

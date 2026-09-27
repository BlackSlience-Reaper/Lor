using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.afflictions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace LibraryOfRuina.powers;

public sealed class LibraryOfRuinaCostReductionPower : LibraryOfRuinaPowerModel
{
    private const int AfflictionHoverTipAmount = 1;
    private const decimal AfflictionAmountPerCard = 1m;

    protected override string LegacyPowerId => "COST_REDUCTION_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromAffliction<LibraryOfRuinaCostReductionAffliction>();

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (Amount <= 0 || card.Owner != Owner.Player || CombatState.CurrentSide != Owner.Side)
        {
            return;
        }

        
        if (card.Type is not (CardType.Attack or CardType.Skill))
        {
            return;
        }

        if (!ModelDb.Affliction<LibraryOfRuinaCostReductionAffliction>().CanAfflict(card))
        {
            return;
        }

        int afflictedThisTurn = CombatManager.Instance.History.Entries
            .OfType<CardAfflictedEntry>()
            .Count(entry =>
                entry.HappenedThisTurn(CombatState) &&
                entry.Actor == Owner &&
                entry.Affliction is LibraryOfRuinaCostReductionAffliction);

        if (afflictedThisTurn >= Amount)
        {
            return;
        }

        await CardCmd.AfflictAndPreview<LibraryOfRuinaCostReductionAffliction>(
            new[] { card },
            AfflictionAmountPerCard,
            CardPreviewStyle.None);
    }
}

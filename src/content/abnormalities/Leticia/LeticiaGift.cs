using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Leticia;

[CardPool(typeof(StatusCardPool))]
public sealed class LeticiaGift : CardModel
{
    private const int GiftDamage = 1;

    public override int MaxUpgradeLevel => 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(GiftDamage, ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move)
    ];

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/status/leticia_gift.png");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust
    ];

    public LeticiaGift()
        : base(1, CardType.Status, CardRarity.Status, TargetType.None)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null || CombatState == null)
        {
            return;
        }

        IReadOnlyList<Creature> targets = CombatState.Creatures
            .Where(creature => creature.IsAlive)
            .ToArray();

        if (targets.Count == 0)
        {
            return;
        }

        await CreatureCmdCompat.Damage(
            choiceContext,
            targets,
            DynamicVars.Damage.BaseValue,
            DynamicVars.Damage.Props,
            Owner.Creature,
            this,
            cardPlay);
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        MarkGiftAsSeen(card);
        return Task.CompletedTask;
    }

    private static void MarkGiftAsSeen(CardModel card)
    {
        if (card is LeticiaGift)
        {
            SaveManager.Instance.MarkCardAsSeen(card);
        }
    }
}

using System;
using System.Threading.Tasks;
using LibraryOfRuina.interop;
using LibraryOfRuina.relics.Yanami;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.StandaloneRelics;

public sealed class FriesKnucklesRelic : YanamiRelicModel
{
    private const int MaxBoostedAttackCards = 4;

    private HashSet<CardModel> _boostedCards = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _boostedCards = [.. _boostedCards];
    }

    protected override string IconBaseName => "fries_knuckles_relic";

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool ShowCounter => true;

    public override int DisplayAmount => Math.Max(0, MaxBoostedAttackCards - BoostedAttackCardsThisCombat);

    [SavedProperty]
    public int BoostedAttackCardsThisCombat { get; private set; }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MaxAttacks", MaxBoostedAttackCards),
        new DynamicVar("DamageMultiplier", 2m)
    ];

    public override Task BeforeCombatStart()
    {
        BoostedAttackCardsThisCombat = 0;
        _boostedCards.Clear();
        Status = RelicStatus.Active;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || cardPlay.Card.Type != CardType.Attack)
        {
            return Task.CompletedTask;
        }

        if (BoostedAttackCardsThisCombat >= MaxBoostedAttackCards)
        {
            return Task.CompletedTask;
        }

        BoostedAttackCardsThisCombat++;
        _boostedCards.Add(cardPlay.Card);
        Flash();
        Status = (BoostedAttackCardsThisCombat >= MaxBoostedAttackCards) ? RelicStatus.Disabled : RelicStatus.Active;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        _boostedCards.Remove(cardPlay.Card);
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        CardModel? card = cardSource;
        if (!ValuePropCompat.IsPoweredAttack(props) || card == null)
        {
            return 1m;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return 1m;
        }

        if (!ShouldBoostCardDamage(card))
        {
            return 1m;
        }

        return DynamicVars["DamageMultiplier"].BaseValue;
    }

    private bool ShouldBoostCardDamage(CardModel card)
    {
        if (_boostedCards.Contains(card))
        {
            return true;
        }

        if (!card.IsMutable)
        {
            return false;
        }

        if (BoostedAttackCardsThisCombat >= MaxBoostedAttackCards)
        {
            return false;
        }

        if (card.Owner != Owner || card.Type != CardType.Attack)
        {
            return false;
        }

        CardPile? pile = card.Pile;
        return pile == null || pile.Type != PileType.Play;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _boostedCards.Clear();
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }
}

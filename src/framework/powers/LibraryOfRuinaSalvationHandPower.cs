using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.framework.powers;

public sealed class LibraryOfRuinaSalvationHandPower : LibraryOfRuinaPowerModel
{
    private const int BaseSealCount = 2;

    protected override string LegacyPowerId => "LIBRARY_OF_RUINA_SALVATION_HAND_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new SealCountVar()];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromAffliction<FuneralSealAffliction>();

    private sealed class SealCountVar : DynamicVar
    {
        public SealCountVar() : base("Seals", BaseSealCount)
        {
        }
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Owner.IsDead || side != CombatSide.Player)
        {
            return;
        }

        foreach (var player in combatState.Players.Where(player => participants.Contains(player.Creature)))
        {
            IReadOnlyList<CardModel> candidates = player.PlayerCombatState?.Hand.Cards
                .Where(card => ModelDb.Affliction<FuneralSealAffliction>().CanAfflict(card))
                .ToArray() ?? [];

            foreach (CardModel card in candidates.TakeRandom(BaseSealCount, combatState.RunState.Rng.CombatCardSelection))
            {
                await CardCmd.Afflict<FuneralSealAffliction>(card, 1);
            }
        }

        await Task.CompletedTask;
    }

    public override Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner.CombatState is { } combatState)
        {
            FuneralSealAffliction.ClearPlayerHandSeals(combatState);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (oldPileType == PileType.Hand || card.Pile?.Type != PileType.Hand)
        {
            FuneralSealAffliction.ClearSeal(card);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        FuneralSealAffliction.ClearSeal(card);
        return Task.CompletedTask;
    }

    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        FuneralSealAffliction.ClearSeal(card);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner.CombatState is { } combatState)
        {
            FuneralSealAffliction.ClearPlayerHandSeals(combatState);
        }

        return Task.CompletedTask;
    }
}

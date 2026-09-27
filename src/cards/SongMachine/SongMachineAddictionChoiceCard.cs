using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SongMachine;

[CardPool(typeof(TokenCardPool))]
public sealed class SongMachineAddictionChoiceCard : SongMachineChoiceCardBase
{
    protected override string PortraitFileName => "song_machine_addiction_choice_card.png";
}


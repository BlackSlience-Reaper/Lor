using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SongMachine;

[CardPool(typeof(TokenCardPool))]
public sealed class SongMachineMelodyChoiceCard : SongMachineChoiceCardBase
{
    protected override string PortraitFileName => "song_machine_melody_choice_card.png";
}


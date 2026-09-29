using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.SongMachine;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SongMachine;

[CardPool(typeof(TokenCardPool))]
public sealed class SongMachineMelodyChoiceCard : SongMachineChoiceCardBase
{
    public override SongMachinePageMode PageMode => SongMachinePageMode.Melody;

    protected override string PortraitFileName => "song_machine_melody_choice_card.png";
}


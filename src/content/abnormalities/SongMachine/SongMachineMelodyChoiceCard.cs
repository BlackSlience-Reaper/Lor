using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.SongMachine;

[CardPool(typeof(TokenCardPool))]
public sealed class SongMachineMelodyChoiceCard : SongMachineChoiceCardBase
{
    public override SongMachinePageMode PageMode => SongMachinePageMode.Melody;

    protected override string PortraitFileName => "song_machine_melody_choice_card.png";
}


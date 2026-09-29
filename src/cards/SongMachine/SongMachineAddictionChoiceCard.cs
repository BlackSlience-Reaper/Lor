using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.SongMachine;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SongMachine;

[CardPool(typeof(TokenCardPool))]
public sealed class SongMachineAddictionChoiceCard : SongMachineChoiceCardBase
{
    public override SongMachinePageMode PageMode => SongMachinePageMode.Addiction;

    protected override string PortraitFileName => "song_machine_addiction_choice_card.png";
}


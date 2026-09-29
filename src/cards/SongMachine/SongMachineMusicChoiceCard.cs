using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.SongMachine;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SongMachine;

[CardPool(typeof(TokenCardPool))]
public sealed class SongMachineMusicChoiceCard : SongMachineChoiceCardBase
{
    public override SongMachinePageMode PageMode => SongMachinePageMode.Music;

    protected override string PortraitFileName => "song_machine_music_choice_card.png";
}


using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.SongMachine;

[CardPool(typeof(TokenCardPool))]
public sealed class SongMachineMusicChoiceCard : SongMachineChoiceCardBase
{
    public override SongMachinePageMode PageMode => SongMachinePageMode.Music;

    protected override string PortraitFileName => "song_machine_music_choice_card.png";
}


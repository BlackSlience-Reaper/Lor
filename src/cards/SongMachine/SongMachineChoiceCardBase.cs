using LibraryOfRuina.relics.SongMachine;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.SongMachine;

public abstract class SongMachineChoiceCardBase : CardModel
{
    public const string MusicChoiceId = "SONG_MACHINE_MUSIC_CHOICE_CARD";
    public const string MelodyChoiceId = "SONG_MACHINE_MELODY_CHOICE_CARD";
    public const string AddictionChoiceId = "SONG_MACHINE_ADDICTION_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromCard<Dazed>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(SongMachinePageRelic.MusicKillHeal),
        new PowerVar<StrengthPower>("MusicStrength", SongMachinePageRelic.MusicStartStrength),
        new PowerVar<StrengthPower>("MelodyStrength", SongMachinePageRelic.MelodyStartStrength),
        new PowerVar<LibraryStrongPower>("AddictionStrong", SongMachinePageRelic.AddictionStartStrong),
        new PowerVar<DexterityPower>("AddictionDexterity", SongMachinePageRelic.AddictionDexterityLoss),
        new DynamicVar("Dazed", SongMachinePageRelic.MelodyDazedCount),
    ];

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected SongMachineChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsSongMachineChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is MusicChoiceId or MelodyChoiceId or AddictionChoiceId;
    }
}


using LibraryOfRuina.relics.SongMachine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.SongMachine;

public abstract class SongMachineChoiceCardBase : PageChoiceCard<SongMachinePageMode>
{
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
}


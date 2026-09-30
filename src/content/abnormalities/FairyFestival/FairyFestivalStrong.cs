using System.Linq;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

public sealed class FairyFestivalStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "FairyFestivalBGM",
        new[]
        {
            FairyFestivalAssets.HistoryLayer1Bgm,
            FairyFestivalAssets.HistoryLayer2Bgm,
            FairyFestivalAssets.HistoryLayer3Bgm
        },
        volumeScale: 0.85f,
        4,
        7);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "left_mass", "queen", "right_mass" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<FairyMass>(),
        ModelDb.Monster<FairyQueen>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        new[]
        {
            FairyFestivalAssets.Background1,
            FairyFestivalAssets.Background2,
            FairyFestivalAssets.HistoryLayer1Bgm,
            FairyFestivalAssets.HistoryLayer2Bgm,
            FairyFestivalAssets.HistoryLayer3Bgm
        }
        .Concat(ModelDb.Monster<FairyMass>().AssetPaths)
        .Concat(ModelDb.Monster<FairyQueen>().AssetPaths)
        .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var leftMass = (FairyMass)ModelDb.Monster<FairyMass>().ToMutable();
        leftMass.SetVariant(FairyMassVariant.Left);

        var rightMass = (FairyMass)ModelDb.Monster<FairyMass>().ToMutable();
        rightMass.SetVariant(FairyMassVariant.Right);

        return
        [
            (leftMass, "left_mass"),
            (ModelDb.Monster<FairyQueen>().ToMutable(), "queen"),
            (rightMass, "right_mass")
        ];
    }
}

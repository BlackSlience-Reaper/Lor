using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

public sealed class LittleRedMercenaryElite : EncounterModel
{
    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "little_red", "wolf" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<LittleRedRidingHoodedMercenary>(),
        ModelDb.Monster<WolfInHerNightmares>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<LittleRedRidingHoodedMercenary>().AssetPaths
            .Concat(ModelDb.Monster<WolfInHerNightmares>().AssetPaths)
            .Concat(new[]
            {
                LittleRedMercenaryAssets.Background1,
                LittleRedMercenaryAssets.Background2,
                LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_attack.ogg",
                LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_fire.ogg",
                LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_rage.ogg",
                LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_throw.ogg",
                LittleRedMercenaryEncounterHelper.SfxRoot + "little_red_unrelieved.ogg",
                LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_bite.ogg",
                LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_claw.ogg",
                LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_howl.ogg",
                LittleRedMercenaryEncounterHelper.SfxRoot + "wolf_phase_two.ogg"
            })
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<LittleRedRidingHoodedMercenary>().ToMutable(), "little_red"),
            (ModelDb.Monster<WolfInHerNightmares>().ToMutable(), "wolf")
        ];
    }
}

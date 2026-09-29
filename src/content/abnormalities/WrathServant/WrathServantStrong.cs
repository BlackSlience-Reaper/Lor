using System.Linq;
using Godot;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

public sealed class WrathServantStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "WrathServantBGM",
        GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    private const string EndedByServantDeathKey = "endedByServantDeath";

    public const string StaffSlotLeft = "staff_left";
    public const string ServantSlot = "wrath_servant";
    public const string HermitSlot = "green_stem_hermit";
    public const string StaffSlotRight = "staff_right";

    private const float CameraScaling = 0.88f;
    private static readonly Vector2 CameraOffset = Vector2.Down * 80f + Vector2.Left * 90f;

    public override RoomType RoomType => RoomType.Monster;

    public override bool ShouldGiveRewards => !EndedByServantDeath;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots =>
    [
        ServantSlot,
        StaffSlotLeft,
        StaffSlotRight,
        HermitSlot
    ];

    public override float GetCameraScaling() => CameraScaling;

    public override Vector2 GetCameraOffset() => CameraOffset;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<WrathServant>(),
        ModelDb.Monster<GreenStemHermit>(),
        ModelDb.Monster<HermitStaff>()
    ];

    public bool EndedByServantDeath { get; private set; }

    public override IEnumerable<string> ExtraAssetPaths =>
        WrathServant.AssetPathsStatic
            .Concat(GreenStemHermit.AssetPathsStatic)
            .Concat(HermitStaff.AssetPathsStatic)
            .Concat(WrathServantEncounterHelper.PowerIconPaths)
            .Concat(GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks)
            .Concat(new[]
            {
                "res://images/backgrounds/wrath_servant_strong/background.png",
                "res://scenes/backgrounds/wrath_servant_strong/wrath_servant_strong_background.tscn",
                "res://scenes/backgrounds/wrath_servant_strong/layers/wrath_servant_strong_bg_00_a.tscn"
            })
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<WrathServant>().ToMutable(), ServantSlot),
            (ModelDb.Monster<HermitStaff>().ToMutable(), StaffSlotLeft),
            (ModelDb.Monster<HermitStaff>().ToMutable(), StaffSlotRight),
            (ModelDb.Monster<GreenStemHermit>().ToMutable(), HermitSlot)
        ];
    }

    public void MarkEndedByServantDeath()
    {
        EndedByServantDeath = true;
    }

    public override Dictionary<string, string> SaveCustomState()
    {
        return new Dictionary<string, string>
        {
            [EndedByServantDeathKey] = EndedByServantDeath.ToString()
        };
    }

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        EndedByServantDeath =
            new EncounterStateBag(state).ReadBool(EndedByServantDeathKey);
    }
}

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.CosmicFragment;

public sealed class CosmicFragmentWeak : EncounterModel
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new[] { ModelDb.Monster<monsters.CosmicFragment.CosmicFragment>() };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<monsters.CosmicFragment.CosmicFragment>().ToMutable(), null)
        };
    }
}

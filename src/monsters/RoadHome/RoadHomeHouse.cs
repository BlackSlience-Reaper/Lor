using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.RoadHome;
using LibraryOfRuina.powers.RoadHome;
using LibraryOfRuina.visuals.RoadHome;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.RoadHome;

public sealed class RoadHomeHouse : LibraryMonsterModel
{
    internal const string WaitMoveId = "ROAD_HOME_HOUSE_WAIT";
    public const string IdleTexturePath = RoadHomeEncounterHelper.TextureRoot + "house.png";

    public static readonly string[] StaticAssetPaths =
        RoadHomeHouseCreatureVisuals
            .Profile.AssetPaths.ToArray();

    public override int MinInitialHp => 290;

    public override int MaxInitialHp => 290;

    public override int DefaultChaoResistance => 220;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => NormalResistance();

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => NormalResistance();

    public override IEnumerable<string> AssetPaths => StaticAssetPaths;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmdCompat.Apply<RoadHomeHouseProtectionPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1, Creature, null, silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState wait = new(WaitMoveId, _ => Task.CompletedTask, new HiddenIntent());
        wait.FollowUpState = wait;
        return new MonsterMoveStateMachine([wait], wait);
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        return creature == Creature
            ? RoadHomeEncounterHelper.OnHouseDefeated(choiceContext, creature, wasRemovalPrevented)
            : Task.CompletedTask;
    }

    private static LibraryCreatureResistanceData.Resistance NormalResistance() => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Fatal
    };
}

using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.powers.ArtFloorLiberation;
using LibraryOfRuina.visuals.ArtFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.ArtFloorLiberation;

public sealed class ArtFloorFirstPerformer : LibraryMonsterModel
{
    private const string SilentMoveId = "SILENT_PERFORMANCE";
    private const int StaggerResistanceMax = 40;

    public const string IdleTexturePath = "res://images/monsters/art_floor/first_performer.png";

    public override int DefaultChaoResistance => StaggerResistanceMax;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 37, 34);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 40, 36);

    public override IEnumerable<string> AssetPaths =>
        ArtFloorFirstPerformerCreatureVisuals.Profile.AssetPaths;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<SilentPerformancePower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var silent = new MoveState(
            SilentMoveId,
            static _ => Task.CompletedTask,
            new UnknownIntent());

        silent.FollowUpState = silent;
        return new MonsterMoveStateMachine(new MonsterState[] { silent }, silent);
    }
}

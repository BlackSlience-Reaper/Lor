using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.visuals.SocialFloorLiberation;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.SocialFloorLiberation;

public sealed class EmeraldCrystal : LibraryMonsterModel
{
    public const int Hp = 50;
    public const int ChaoResistance = 50;
    private const string HiddenMoveId = "EMERALD_CRYSTAL_HIDDEN";

    public override int MinInitialHp => Hp;

    public override int MaxInitialHp => Hp;

    public override int DefaultChaoResistance => ChaoResistance;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => NormalResistance();

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => NormalResistance();

    public override IEnumerable<string> AssetPaths =>
        EmeraldCrystalCreatureVisuals
            .Profile.AssetPaths.Distinct();

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var hidden = new MoveState(
            HiddenMoveId,
            _ => Task.CompletedTask,
            new HiddenIntent());
        hidden.FollowUpState = hidden;
        return new MonsterMoveStateMachine([hidden], hidden);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        await base.AfterDeath(
            choiceContext,
            creature,
            wasRemovalPrevented,
            deathAnimLength);
        if (!wasRemovalPrevented
            && creature == Creature
            && Creature.CombatState?.Encounter
                is SocialFloorLiberationEncounter encounter)
        {
            await encounter.NotifyCrystalDestroyed(this);
        }
    }

    private static LibraryCreatureResistanceData.Resistance
        NormalResistance() => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };
}

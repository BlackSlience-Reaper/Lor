using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.visuals.SocialFloorLiberation;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.SocialFloorLiberation;

public sealed class ScowlingFace : LorMonsterModel
{
    public const int BaseHp = 50;
    public const int ChaoResistance = 50;
    private const string HiddenMoveId = "SCOWLING_FACE_HIDDEN";
    private int _configuredInitialHp = BaseHp;

    public override int MinInitialHp => _configuredInitialHp;

    public override int MaxInitialHp => _configuredInitialHp;

    public override int DefaultChaoResistance => ChaoResistance;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => NormalResistance();

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => NormalResistance();

    public override IEnumerable<string> AssetPaths =>
        ScowlingFaceCreatureVisuals
            .Profile.AssetPaths.Distinct();

    internal void ConfigureInitialHp(int hp)
    {
        AssertMutable();
        _configuredInitialHp = Math.Max(1, hp);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        int exactHp = SocialFloorLiberationEncounter
            .ResolveFaceHpForPlayerCount(
                Creature.CombatState?.Players.Count ?? 1);
        int currentHp = Creature.CurrentHp;
        Creature.SetMaxHpInternal(exactHp);
        Creature.SetCurrentHpInternal(Math.Min(currentHp, exactHp));

        if (Creature is LibraryCreature libraryCreature)
        {
            int currentChao = libraryCreature.CurrentChaoValue;
            libraryCreature.SetMaxChaoValueInternal(ChaoResistance);
            libraryCreature.SetCurrentChaoValueInternal(
                Math.Min(currentChao, ChaoResistance));
        }
    }

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
            await encounter.NotifyFaceDestroyed(this);
        }
    }

    private static LibraryCreatureResistanceData.Resistance
        NormalResistance() => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };
}

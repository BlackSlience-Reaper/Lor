using System;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.monsters;

internal sealed class DelegatingMonsterRouterState(
    string id,
    Func<Creature, Rng, string> resolveNextState,
    bool shouldAppearInLogs = false)
    : MonsterState
{
    public override string Id => id;

    public override bool ShouldAppearInLogs => shouldAppearInLogs;

    public override string GetNextState(Creature owner, Rng rng) =>
        resolveNextState(owner, rng);

    public override void RegisterStates(
        Dictionary<string, MonsterState> monsterStates) =>
        monsterStates.Add(Id, this);
}

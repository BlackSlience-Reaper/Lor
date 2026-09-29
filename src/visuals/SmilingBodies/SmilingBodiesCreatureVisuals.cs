using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.SmilingBodies;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.SmilingBodies;

[MonsterVisual(typeof(monsters.SmilingBodies.SmilingBodies), ScenePath = SmilingBodiesCreatureVisuals.ScenePath)]
internal sealed partial class SmilingBodiesCreatureVisuals
    : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/smiling_bodies.tscn";

    protected override string ResolveCurrentAnimationLibrary() =>
        SmilingBodiesAnimationContract.LibraryForPhase(ResolvePhase());

    internal static string ResolveAnimationName(
        SmilingBodiesPhase phase,
        string triggerName) =>
        SmilingBodiesAnimationContract.QualifiedAnimationName(
            phase,
            triggerName);

    private SmilingBodiesPhase ResolvePhase() =>
        (GetParent() as NCreature)?.Entity?.Monster
            is monsters.SmilingBodies.SmilingBodies boss
                ? boss.Phase
                : SmilingBodiesPhase.Second;
}

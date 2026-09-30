using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.SmilingBodies;

[MonsterVisual(typeof(SmilingBodies), ScenePath = SmilingBodiesCreatureVisuals.ScenePath)]
internal sealed partial class SmilingBodiesCreatureVisuals
    : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        SmilingBodiesAssets.SmilingBodiesScene;

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
            is SmilingBodies boss
                ? boss.Phase
                : SmilingBodiesPhase.Second;
}

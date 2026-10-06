using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.SmilingBodies;

[MonsterVisual(typeof(SmilingBodies), ScenePath = SmilingBodiesCreatureVisuals.ScenePath)]
internal sealed partial class SmilingBodiesCreatureVisuals
    : SceneAnimatedCreatureVisuals
{
    // 整块的 Spine 身体按动画库（phase_1/2/3 三个形态）各一副（照场景摆放，各姿势按标注点对齐），加载失败时退回场景动画。
    // 转阶段（Phase）在场景里就是新形态的待机图，不映射：换库时会切到新形态的骨架并回待机
    internal static readonly RuntimeSpineBody.Spec Phase1Spine = LayeredBossSpine.Create(
        "smiling_bodies",
        "smiling_bodies_phase1",
        "absorb",
        new Dictionary<string, string>
        {
            ["Absorb"] = "absorb",
        });

    internal static readonly RuntimeSpineBody.Spec Phase2Spine = LayeredBossSpine.Create(
        "smiling_bodies",
        "smiling_bodies_phase2",
        "scream",
        new Dictionary<string, string>
        {
            ["Absorb"] = "absorb",
            ["Scream"] = "scream",
        });

    internal static readonly RuntimeSpineBody.Spec Phase3Spine = LayeredBossSpine.Create(
        "smiling_bodies",
        "smiling_bodies_phase3",
        "vomit",
        new Dictionary<string, string>
        {
            ["Absorb"] = "absorb",
            ["Sit"] = "sit",
            ["Vomit"] = "vomit",
        });

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        [Phase1Spine, Phase2Spine, Phase3Spine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) => library switch
    {
        "phase_1" => Phase1Spine,
        "phase_3" => Phase3Spine,
        _ => Phase2Spine,
    };

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

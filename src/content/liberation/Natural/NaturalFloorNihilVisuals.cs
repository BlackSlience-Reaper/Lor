using System;
using System.Linq;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Natural;

[MonsterVisual(typeof(NaturalFloorNihilBoss), ScenePath = NaturalFloorNihilVisuals.SceneRoot + "boss.tscn")]
[MonsterVisual(typeof(NaturalFloorLoveGirl), ScenePath = NaturalFloorNihilVisuals.SceneRoot + "love.tscn")]
[MonsterVisual(typeof(NaturalFloorJusticeGirl), ScenePath = NaturalFloorNihilVisuals.SceneRoot + "justice.tscn")]
[MonsterVisual(typeof(NaturalFloorHappinessGirl), ScenePath = NaturalFloorNihilVisuals.SceneRoot + "happiness.tscn")]
[MonsterVisual(typeof(NaturalFloorCourageGirl), ScenePath = NaturalFloorNihilVisuals.SceneRoot + "courage.tscn")]
[MonsterVisual(typeof(NaturalFloorLoveStatue), ScenePath = NaturalFloorNihilVisuals.SceneRoot + "love_statue.tscn")]
[MonsterVisual(typeof(NaturalFloorJusticeStatue), ScenePath = NaturalFloorNihilVisuals.SceneRoot + "justice_statue.tscn")]
[MonsterVisual(typeof(NaturalFloorHappinessStatue), ScenePath = NaturalFloorNihilVisuals.SceneRoot + "happiness_statue.tscn")]
[MonsterVisual(typeof(NaturalFloorCourageStatue), ScenePath = NaturalFloorNihilVisuals.SceneRoot + "courage_statue.tscn")]
internal sealed partial class NaturalFloorNihilVisuals : SceneAnimatedCreatureVisuals
{
    // Spine 身体按动画库（形态）各一副，见 tools/spine_from_layers/build_boss_configs.py 的自然层配置
    internal static readonly RuntimeSpineBody.Spec NihilSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nihil",
        "attack",
        new Dictionary<string, string>
        {
            ["Attack"] = "attack",
            ["Slash"] = "attack",
            ["Fire"] = "attack",
            ["Pierce"] = "attack",
            ["Strike"] = "attack",
            ["Special"] = "special",
            ["Cast"] = "guard",
            ["Defend"] = "guard",
            ["Evade"] = "evade",
            ["Stunned"] = "stunned",
        });

    internal static readonly RuntimeSpineBody.Spec DespairSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nihil_despair",
        "attack",
        new Dictionary<string, string>
        {
            ["Attack"] = "attack",
            ["Slash"] = "attack",
            ["Fire"] = "attack",
            ["Pierce"] = "attack",
            ["Strike"] = "attack",
            ["Special"] = "special",
            ["Cast"] = "guard",
            ["Defend"] = "guard",
            ["Evade"] = "evade",
            ["Stunned"] = "stunned",
        });

    internal static readonly RuntimeSpineBody.Spec GreedSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nihil_greed",
        "slash",
        new Dictionary<string, string>
        {
            ["Attack"] = "slash",
            ["Slash"] = "slash",
            ["Fire"] = "slash",
            ["Pierce"] = "strike",
            ["Strike"] = "strike",
            ["Special"] = "special",
            ["Cast"] = "guard",
            ["Defend"] = "guard",
            ["Evade"] = "evade",
            ["Stunned"] = "stunned",
        });

    internal static readonly RuntimeSpineBody.Spec HatredSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nihil_hatred",
        "slash",
        new Dictionary<string, string>
        {
            ["Attack"] = "slash",
            ["Slash"] = "slash",
            ["Fire"] = "fire",
            ["Pierce"] = "slash",
            ["Strike"] = "slash",
            ["Special"] = "special",
            ["Cast"] = "guard",
            ["Defend"] = "guard",
            ["Evade"] = "evade",
            ["Stunned"] = "stunned",
        });

    internal static readonly RuntimeSpineBody.Spec WrathSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nihil_wrath",
        "slash",
        new Dictionary<string, string>
        {
            ["Attack"] = "slash",
            ["Slash"] = "slash",
            ["Fire"] = "slash",
            ["Pierce"] = "slash",
            ["Strike"] = "strike",
            ["Special"] = "special",
            ["Cast"] = "guard",
            ["Defend"] = "guard",
            ["Evade"] = "evade",
            ["Stunned"] = "stunned",
        });

    internal static readonly RuntimeSpineBody.Spec LoveSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nihil_love",
        "attack",
        new Dictionary<string, string>
        {
            ["Attack"] = "attack",
            ["Slash"] = "attack",
            ["Fire"] = "fire",
            ["Pierce"] = "attack",
            ["Strike"] = "attack",
            ["Special"] = "special",
            ["Cast"] = "guard",
            ["Defend"] = "guard",
            ["Evade"] = "guard",
        });

    internal static readonly RuntimeSpineBody.Spec JusticeSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nihil_justice",
        "attack",
        new Dictionary<string, string>
        {
            ["Attack"] = "attack",
            ["Slash"] = "attack",
            ["Fire"] = "attack",
            ["Pierce"] = "attack",
            ["Strike"] = "attack",
            ["Special"] = "attack",
            ["Stunned"] = "stunned",
        });

    internal static readonly RuntimeSpineBody.Spec HappinessSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nihil_happiness",
        "slash",
        new Dictionary<string, string>
        {
            ["Attack"] = "slash",
            ["Slash"] = "slash",
            ["Fire"] = "slash",
            ["Pierce"] = "pierce",
            ["Strike"] = "slash",
            ["Special"] = "special",
            ["Cast"] = "guard",
            ["Defend"] = "guard",
            ["Evade"] = "guard",
        });

    internal static readonly RuntimeSpineBody.Spec CourageSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nihil_courage",
        "slash",
        new Dictionary<string, string>
        {
            ["Attack"] = "slash",
            ["Slash"] = "slash",
            ["Fire"] = "slash",
            ["Pierce"] = "slash",
            ["Strike"] = "strike",
            ["Special"] = "special",
        });

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        ((GetParent() as NCreature)?.Entity?.Monster is NaturalFloorNihilBoss)
            ? [NihilSpine, DespairSpine, GreedSpine, HatredSpine, WrathSpine]
            : base.AllSpineSpecs;

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) =>
        (GetParent() as NCreature)?.Entity?.Monster switch
        {
            NaturalFloorNihilBoss => library switch
            {
                "despair" => DespairSpine,
                "greed" => GreedSpine,
                "hatred" => HatredSpine,
                "wrath" => WrathSpine,
                _ => NihilSpine,
            },
            NaturalFloorLoveGirl => LoveSpine,
            NaturalFloorJusticeGirl => JusticeSpine,
            NaturalFloorHappinessGirl => HappinessSpine,
            NaturalFloorCourageGirl => CourageSpine,
            _ => null,
        };

    internal const float HitTime = 0.48f; // 虚无与魔法少女：参照自然层普通动作的打击时点。

    // 特性参数只能是常量，所以登记处写成 SceneRoot + "<id>.tscn"，与 ScenePath(id) 拼出的路径相同。
    internal const string SceneRoot = NaturalFloorAssets.NaturalFloorNihilScenePrefix;

    internal static string ScenePath(string id) => SceneRoot + id + ".tscn";

    internal static IEnumerable<string> Assets(string id) =>
        new[] { ScenePath(id) }.Concat(Enum.GetValues<NaturalFloorNihilAction>()
            .Select(action => NaturalFloorNihilMonster.SoundPath(NaturalFloorNihilMoves.Get(action).Sound)))
        .Append(NaturalFloorNihilEffects.ScenePath)
        .Append(NaturalFloorNihilMonster.SoundPath("MagicalGirl_LaserLoop"))
        .Append(NaturalFloorNihilMonster.SoundPath("MagicalGirl_CastEnd"))
        .Append(NaturalFloorNihilMonster.SoundPath("Angry_StrongAtk2"))
        .Distinct();

    protected override string ResolveCurrentAnimationLibrary()
    {
        return (GetParent() as NCreature)?.Entity?.Monster is NaturalFloorNihilBoss boss
            ? boss.Form.ToString().ToLowerInvariant() : "default";
    }

    protected override string NormalizeTriggerName(string name) => name == "Dead" ? "Hit" : name;
}

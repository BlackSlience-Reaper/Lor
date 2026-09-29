using System;
using System.Linq;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.NaturalFloorLiberation;

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
    internal const float HitTime = 0.48f; // 虚无与魔法少女：参照自然层普通动作的打击时点。

    // 特性参数只能是常量，所以登记处写成 SceneRoot + "<id>.tscn"，与 ScenePath(id) 拼出的路径相同。
    internal const string SceneRoot = "res://scenes/creature_visuals/natural_floor_nihil_";

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

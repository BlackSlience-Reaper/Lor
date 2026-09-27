using System;
using System.Linq;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.NaturalFloorLiberation;

internal sealed partial class NaturalFloorNihilVisuals : SceneAnimatedCreatureVisuals
{
    internal const float HitTime = 0.48f; // 虚无与魔法少女：参照自然层普通动作的打击时点。

    internal static string ScenePath(string id) => "res://scenes/creature_visuals/natural_floor_nihil_" + id + ".tscn";

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

using System.Linq;
using HarmonyLib;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Combat;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.NaturalFloorLiberation;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
[LibraryPatch(Reason = "原版 StartDeathAnim 非虚，会禁用交互、冻结意图并播放死亡演出，没有跳过的扩展点；只作用于自然层遗忘之剑不移除的假死。")]
internal static class NaturalFloorSwordFalseDeathVisualPatch
{
    private static bool Prefix(NCreature __instance, bool shouldRemove, ref float __result)
    {
        if (shouldRemove || __instance.Entity.Monster is not NaturalFloorForgottenSword)
        {
            return true;
        }

        // 假死保留场景与复活意图，隐藏血条及其附属状态显示。
        if (__instance.Visuals is NaturalFloorForgottenSwordVisuals visuals)
        {
            visuals.SetFalseDeathHealthBarHidden(true);
        }

        __result = 0f;
        return false;
    }
}

internal abstract partial class NaturalFloorDespairVisuals : SceneAnimatedCreatureVisuals
{
    private string _lastForm = "";

    public override void _Ready()
    {
        base._Ready();
        _lastForm = ResolveCurrentAnimationLibrary();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        string form = ResolveCurrentAnimationLibrary();
        if (_lastForm == form)
        {
            return;
        }

        _lastForm = form;
        TryPlayTrigger("Idle");
    }
}

[MonsterVisual(typeof(NaturalFloorTearEdgeBoss), ScenePath = NaturalFloorTearEdgeVisuals.ScenePath)]
internal sealed partial class NaturalFloorTearEdgeVisuals : NaturalFloorDespairVisuals
{
    internal const string ScenePath = "res://scenes/creature_visuals/natural_floor_tear_edge_boss.tscn";
    internal static readonly string[] AssetPaths = new[] { ScenePath }
        .Concat(new[] { "normal", "despair", "stabbed1", "stabbed2", "stabbed3" }
            .Select(form => "res://scenes/creature_visuals/natural_floor_tear_edge_" + form + "_animations.tres"))
        .Concat(new[] { "idle", "hit", "special", "s1", "s2", "s3" }
            .Select(frame => "res://images/monsters/natural_floor_liberation/tear_edge/" + frame + ".png")).ToArray();

    protected override string ResolveCurrentAnimationLibrary() => GetParent() is NCreature { Entity.Monster: NaturalFloorTearEdgeBoss boss }
        ? boss.VisualForm : "normal";

    protected override string NormalizeTriggerName(string name) => name == "Dead" ? "Hit" : name;
}

[MonsterVisual(typeof(NaturalFloorForgottenSword), ScenePath = NaturalFloorForgottenSwordVisuals.ScenePath)]
internal sealed partial class NaturalFloorForgottenSwordVisuals : NaturalFloorDespairVisuals
{
    private bool _healthBarHiddenForFalseDeath;
    private bool _healthBarWasVisible;

    internal const string ScenePath = "res://scenes/creature_visuals/natural_floor_forgotten_sword.tscn";
    internal static readonly string[] AssetPaths = new[] { ScenePath }
        .Concat(new[] { "normal", "teardrop", "despair", "dead" }
            .Select(form => "res://scenes/creature_visuals/natural_floor_forgotten_sword_" + form + "_animations.tres"))
        .Concat(new[] { "normal_idle", "normal_blunt", "normal_pierce", "normal_slash", "normal_hit", "normal_guard", "normal_evade",
            "teardrop_idle", "teardrop_blunt", "teardrop_pierce", "teardrop_slash", "teardrop_hit", "teardrop_guard", "teardrop_evade", "despair_idle", "despair_attack" }
            .Select(frame => "res://images/monsters/natural_floor_liberation/forgotten_sword/" + frame + ".png")).ToArray();

    protected override string ResolveCurrentAnimationLibrary() => GetParent() is NCreature { Entity.Monster: NaturalFloorForgottenSword sword }
        ? sword.VisualForm : "normal";

    protected override string NormalizeTriggerName(string name) => name == "Dead" ? "Hit" : name;

    public override void _Ready()
    {
        base._Ready();
        RefreshFalseDeathHealthBar();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        RefreshFalseDeathHealthBar();
    }

    private void RefreshFalseDeathHealthBar()
    {
        if (GetParent() is NCreature { Entity.Monster: NaturalFloorForgottenSword sword })
        {
            SetFalseDeathHealthBarHidden(sword.IsFakeDead);
        }
    }

    internal void SetFalseDeathHealthBarHidden(bool hidden)
    {
        if (GetParent() is not NCreature creatureNode
            || creatureNode.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar") is not { } stateDisplay)
        {
            return;
        }

        if (hidden)
        {
            if (!_healthBarHiddenForFalseDeath)
            {
                _healthBarWasVisible = stateDisplay.Visible;
                _healthBarHiddenForFalseDeath = true;
            }

            // 同步出生动画与血条刷新，读档后的假死单位也保持隐藏。
            stateDisplay.Visible = false;
            return;
        }

        if (!_healthBarHiddenForFalseDeath)
        {
            return;
        }

        _healthBarHiddenForFalseDeath = false;
        if (_healthBarWasVisible && !stateDisplay.Visible && !NCombatUi.IsDebugHidingHpBar)
        {
            stateDisplay.AnimateIn(HealthBarAnimMode.FromHidden);
        }
    }
}

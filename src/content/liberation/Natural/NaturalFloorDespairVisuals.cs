using System.Linq;
using HarmonyLib;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Natural;

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
    // Spine 身体按动画库（形态）各一副，见 tools/spine_from_layers/build_boss_configs.py 的自然层配置
    internal static readonly RuntimeSpineBody.Spec NormalSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "tear_edge",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "attack",
            ["Evade"] = "guard",
            ["Guard"] = "guard",
        });

    internal static readonly RuntimeSpineBody.Spec DespairSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "tear_edge_despair",
        "attack",
        new Dictionary<string, string>
        {
            ["Cast"] = "attack",
            ["Evade"] = "guard",
            ["Guard"] = "guard",
        });

    internal static readonly RuntimeSpineBody.Spec Stabbed1Spine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "tear_edge_stabbed1",
        "hurt",
        new Dictionary<string, string>
        {
            ["Cast"] = "hurt",
            ["Evade"] = "hurt",
            ["Guard"] = "hurt",
        });

    internal static readonly RuntimeSpineBody.Spec Stabbed2Spine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "tear_edge_stabbed2",
        "hurt",
        new Dictionary<string, string>
        {
            ["Cast"] = "hurt",
            ["Evade"] = "hurt",
            ["Guard"] = "hurt",
        });

    internal static readonly RuntimeSpineBody.Spec Stabbed3Spine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "tear_edge_stabbed3",
        "hurt",
        new Dictionary<string, string>
        {
            ["Cast"] = "hurt",
            ["Evade"] = "hurt",
            ["Guard"] = "hurt",
        });

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        [NormalSpine, DespairSpine, Stabbed1Spine, Stabbed2Spine, Stabbed3Spine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) => library switch
    {
        "despair" => DespairSpine,
        "stabbed1" => Stabbed1Spine,
        "stabbed2" => Stabbed2Spine,
        "stabbed3" => Stabbed3Spine,
        _ => NormalSpine,
    };

    internal const string ScenePath = NaturalFloorAssets.TearEdgeBossScene;
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
    // 整块的 Spine 身体按动画库各一副（tools/spine_from_layers/sprite_layers.py 按场景摆放、按标注页的剑长统一大小），
    // 加载失败时退回场景动画。普通、泪滴库的招架和复活施法都是防御图；绝望库三种攻击都是攻击图，其余是待机图；
    // 倒下（假死）库只有一张受击图
    private static Dictionary<string, string> FormTriggers() => new()
    {
        ["Blunt"] = "blunt",
        ["Pierce"] = "pierce",
        ["Slash"] = "slash",
        ["Guard"] = "guard",
        ["Cast"] = "guard",
        ["Evade"] = "evade",
    };

    internal static readonly RuntimeSpineBody.Spec NormalSpine = LayeredBossSpine.Create(
        "natural_floor_liberation", "nf_forgotten_sword_normal", "slash", FormTriggers());

    internal static readonly RuntimeSpineBody.Spec TeardropSpine = LayeredBossSpine.Create(
        "natural_floor_liberation", "nf_forgotten_sword_teardrop", "slash", FormTriggers());

    internal static readonly RuntimeSpineBody.Spec DespairSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nf_forgotten_sword_despair",
        "attack",
        new Dictionary<string, string>
        {
            ["Blunt"] = "attack",
            ["Pierce"] = "attack",
            ["Slash"] = "attack",
            ["Guard"] = "guard",
            ["Cast"] = "guard",
            ["Evade"] = "guard",
        });

    internal static readonly RuntimeSpineBody.Spec DeadSpine = LayeredBossSpine.Create(
        "natural_floor_liberation",
        "nf_forgotten_sword_dead",
        "hurt",
        new Dictionary<string, string>
        {
            ["Blunt"] = "hurt",
            ["Pierce"] = "hurt",
            ["Slash"] = "hurt",
            ["Guard"] = "hurt",
            ["Cast"] = "hurt",
            ["Evade"] = "hurt",
        });

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        [NormalSpine, TeardropSpine, DespairSpine, DeadSpine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) => library switch
    {
        "teardrop" => TeardropSpine,
        "despair" => DespairSpine,
        "dead" => DeadSpine,
        _ => NormalSpine,
    };

    private bool _healthBarHiddenForFalseDeath;
    private bool _healthBarWasVisible;

    internal const string ScenePath = NaturalFloorAssets.ForgottenSwordScene;
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

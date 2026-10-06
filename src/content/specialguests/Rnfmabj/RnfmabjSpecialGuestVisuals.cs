using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.specialguests.Rnfmabj;

internal static class RnfmabjSpecialGuestPresentationAssets
{
    internal const string BackgroundTitle = "rnfmabj_special_guest";
    internal const string BackgroundScene =
        "res://scenes/backgrounds/rnfmabj_special_guest/rnfmabj_special_guest_background.tscn";
    internal const string BackgroundLayer =
        "res://scenes/backgrounds/rnfmabj_special_guest/layers/rnfmabj_special_guest_bg_00_a.tscn";
    internal const string BodyScene =
        "res://scenes/creature_visuals/rnfmabj.tscn";
    internal const string HandScene =
        "res://scenes/creature_visuals/rnfmabj_hand.tscn";
    internal const string BodyDistortAnimations =
        "res://scenes/creature_visuals/rnfmabj_distort_animations.tres";
    internal const string BodyUnionAnimations =
        "res://scenes/creature_visuals/rnfmabj_union_animations.tres";
    internal const string HandAnimations =
        "res://scenes/creature_visuals/rnfmabj_hand_animations.tres";

    private const string MonsterRoot =
        "res://images/special_guests/rnfmabj/monsters/";
    private const string VfxRoot =
        "res://images/special_guests/rnfmabj/vfx/";

    internal static IReadOnlyList<string> All { get; } =
    [
        RnfmabjSpecialGuestIds.EncounterScene,
        RnfmabjSpecialGuestIds.BattleBackground,
        RnfmabjSpecialGuestIds.BattleBgm,
        BackgroundScene,
        BackgroundLayer,
        BodyScene,
        HandScene,
        BodyDistortAnimations,
        BodyUnionAnimations,
        HandAnimations,
        RnfmabjDirectiveOverlay.CardHoverTipScenePath,
        RnfmabjDirectiveCard.PortraitAssetPath,
        MonsterRoot + "Yan_Distort_Default.png",
        MonsterRoot + "Yan_Distort_Damaged.png",
        MonsterRoot + "Yan_Distort_Evade.png",
        MonsterRoot + "Yan_Distort_Move.png",
        MonsterRoot + "Yan_Hand_Default.png",
        MonsterRoot + "Yan_Hand_Damaged.png",
        MonsterRoot + "Yan_Hand_Guard.png",
        MonsterRoot + "Yan_Hand_Move.png",
        MonsterRoot + "Yan_Hand_Penetrate.png",
        MonsterRoot + "Yan_Hand_Slash.png",
        MonsterRoot + "Yan_Hand_BrandS1.png",
        MonsterRoot + "Yan_Hand_TypingS2.png",
        MonsterRoot + "Yan_Union_Default.png",
        MonsterRoot + "Yan_Union_Damaged.png",
        MonsterRoot + "Yan_Union_Guard.png",
        VfxRoot + "FX_Tex_Mon_Yarn_Sword1.png",
        VfxRoot + "FX_Tex_Wave1.png",
        VfxRoot + "FX_Tex_Mon_Yarn_Spin1.png",
        VfxRoot + "FX_Tex_Mon_Yarn_Text.png",
        VfxRoot + "FX_Tex_Mon_Yarn_Chain.png",
        VfxRoot + "FX_Tex_Mon_Yarn_ChainBlur.png",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_GreatSword_Finish.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_GreatSword_Start.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_Guard.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_Lib_Hori.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_Lib_Vert.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_Stab.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_Stigma_Atk.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_Stigma_Start.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_Typing_Atk.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_Typing_Start.ogg",
        RnfmabjSpecialGuestIds.CombatAudioRoot + "Yan_Vert.ogg",
    ];
}

internal abstract partial class RnfmabjSceneCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName switch
        {
            "Damaged" => "Hit",
            "Block" => "Guard",
            "Dodge" => "Guard",
            _ => triggerName,
        };
}

internal sealed partial class RnfmabjCreatureVisuals :
    RnfmabjSceneCreatureVisuals
{
    private bool _isUnited;

    internal bool InitialUnited { get; set; }

    public override void _Ready()
    {
        _isUnited = InitialUnited;
        base._Ready();
    }

    // Spine 身体见 LayeredBossSpine（场景整图照着地点标注做成整块，只平移转动）：分离、合体两个库各一副。
    // 换形态的触发先在 NormalizeTriggerName 里改 _isUnited，再由新形态的骨架播 split / union
    internal static readonly RuntimeSpineBody.Spec DistortSpine = LayeredBossSpine.Create(
        "special_guests",
        "rnfmabj_distort",
        "twisted_blade",
        new Dictionary<string, string>
        {
            ["TwistedBlade"] = "twisted_blade",
            ["Move"] = "move",
            ["Guard"] = "guard",
            ["Split"] = "split",
        });

    internal static readonly RuntimeSpineBody.Spec UnionSpine = LayeredBossSpine.Create(
        "special_guests",
        "rnfmabj_union",
        "twisted_blade",
        new Dictionary<string, string>
        {
            ["TwistedBlade"] = "twisted_blade",
            ["Move"] = "move",
            ["Guard"] = "guard",
            ["Union"] = "union",
        });

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [DistortSpine, UnionSpine];

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) =>
        library == "union" ? UnionSpine : DistortSpine;

    protected override string ResolveCurrentAnimationLibrary() =>
        _isUnited ? "union" : "distort";

    protected override string NormalizeTriggerName(string triggerName)
    {
        string normalized = base.NormalizeTriggerName(triggerName);
        switch (normalized)
        {
            case "Union":
                _isUnited = true;
                break;
            case "Split":
                _isUnited = false;
                break;
        }

        return normalized;
    }
}

internal sealed partial class RnfmabjHandCreatureVisuals :
    RnfmabjSceneCreatureVisuals
{
    // Spine 身体同本体；左手由 MotionRoot 横向镜像，骨架挂在同一父节点下跟着镜像。
    // 合体时整只隐藏、假死时半透明照旧作用在外观根节点上，骨架一起受影响
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "special_guests",
        "rnfmabj_hand",
        "punch",
        new Dictionary<string, string>
        {
            ["Punch"] = "punch",
            ["MultiPunch"] = "multi_punch",
            ["Palm"] = "palm",
            ["Brand"] = "brand",
            ["Lock"] = "lock",
            ["Move"] = "move",
            ["Guard"] = "guard",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    protected override string ResolveCurrentAnimationLibrary() => "hand";

    internal void SetAvailability(bool isUnited, bool isFakeDead)
    {
        Visible = !isUnited;
        Modulate = isFakeDead
            ? new Color(0.58f, 0.68f, 0.78f, 0.48f)
            : Colors.White;
    }
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.CreateVisuals))]
[HarmonyPriority(Priority.Low)]
[LibraryPatch(Reason = "原版 CreateVisuals 非虚，外观需要额外初始化合体状态与手部偏移；只作用于 Rnfmabj 本体与双手。")]
internal static class RnfmabjSpecialGuestCreateVisualsPatch
{
    private const float HandIntentRightOffset = 60f;

    [HarmonyPrefix]
    private static bool Prefix(
        MonsterModel __instance,
        ref NCreatureVisuals __result)
    {
        switch (__instance)
        {
            case Rnfmabj boss:
            {
                var visuals = (RnfmabjCreatureVisuals)
                    WrappedMonsterVisualFactory
                        .CreateSceneBackedVisuals<RnfmabjCreatureVisuals>(
                            boss.Id.Entry,
                            RnfmabjSpecialGuestPresentationAssets.BodyScene);
                visuals.InitialUnited = boss.IsUnited;
                __result = visuals;
                return false;
            }
            case RnfmabjLeftHand leftHand:
                __result = CreateHandVisuals(leftHand, isLeft: true);
                return false;
            case RnfmabjRightHand rightHand:
                __result = CreateHandVisuals(rightHand, isLeft: false);
                return false;
            default:
                return true;
        }
    }

    private static RnfmabjHandCreatureVisuals CreateHandVisuals(
        RnfmabjHandBase hand,
        bool isLeft)
    {
        var visuals = (RnfmabjHandCreatureVisuals)
            WrappedMonsterVisualFactory
                .CreateSceneBackedVisuals<RnfmabjHandCreatureVisuals>(
                    hand.Id.Entry,
                    RnfmabjSpecialGuestPresentationAssets.HandScene);
        if (isLeft)
        {
            Node2D motionRoot = visuals.GetNode<Node2D>("MotionRoot");
            motionRoot.Scale = new Vector2(
                -Mathf.Abs(motionRoot.Scale.X),
                motionRoot.Scale.Y);
            MirrorMarker(visuals, "CenterPos");
            MirrorMarker(visuals, "IntentPos");
            MirrorMarker(visuals, "TalkPos");
            Control bounds = visuals.GetNode<Control>("Bounds");
            (bounds.OffsetLeft, bounds.OffsetRight) =
                (-bounds.OffsetRight, -bounds.OffsetLeft);
        }

        Marker2D intentMarker = visuals.GetNode<Marker2D>("IntentPos");
        intentMarker.Position += Vector2.Right * HandIntentRightOffset;

        bool isUnited = hand.Creature.CombatState?.Enemies
            .Select(static creature => creature.Monster)
            .OfType<Rnfmabj>()
            .Any(static boss => boss.IsUnited) == true;
        visuals.SetAvailability(isUnited, hand.IsFakeDead);
        return visuals;
    }

    private static void MirrorMarker(Node root, string path)
    {
        Marker2D marker = root.GetNode<Marker2D>(path);
        marker.Position = new Vector2(-marker.Position.X, marker.Position.Y);
    }
}

[HarmonyPatch(
    typeof(RnfmabjHandBase),
    nameof(RnfmabjHandBase.RefreshCombatAvailability))]
internal static class RnfmabjHandAvailabilityVisualPatch
{
    [HarmonyPostfix]
    private static void Postfix(RnfmabjHandBase __instance)
    {
        RefreshVisual(__instance);
    }

    internal static void RefreshVisual(RnfmabjHandBase hand)
    {
        NCreature? node = NCombatRoom.Instance?
            .GetCreatureNode(hand.Creature);
        if (node?.Visuals is not RnfmabjHandCreatureVisuals visuals)
        {
            return;
        }

        bool isUnited = hand.Creature.CombatState?.Enemies
            .Select(static creature => creature.Monster)
            .OfType<Rnfmabj>()
            .Any(static boss => boss.IsUnited) == true;
        visuals.SetAvailability(isUnited, hand.IsFakeDead);
    }
}

[HarmonyPatch(
    typeof(RnfmabjHandBase),
    nameof(RnfmabjHandBase.ApplySavedCombatAvailability))]
internal static class RnfmabjSavedHandAvailabilityVisualPatch
{
    [HarmonyPostfix]
    private static void Postfix(RnfmabjHandBase __instance) =>
        RnfmabjHandAvailabilityVisualPatch.RefreshVisual(__instance);
}

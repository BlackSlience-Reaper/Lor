using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.specialguests.Xiao;

internal abstract partial class XiaoSceneCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName switch
        {
            "Damaged" => "Hit",
            "Block" => "Guard",
            "Dodge" => "Evade",
            _ => triggerName,
        };
}

internal sealed partial class XiaoStageOneCreatureVisuals :
    XiaoSceneCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/xiao_stage_one.tscn";

    protected override string ResolveCurrentAnimationLibrary() =>
        XiaoAnimationContract.StageOneLibrary;
}

internal sealed partial class MirisCreatureVisuals :
    SpriteAttackCreatureVisuals
{
    internal static readonly SpriteVisualProfile Profile =
        XiaoGuestVisualProfile.Build(
            "res://images/special_guests/xiao/monsters/miris/",
            "Miris",
            hasMove: false,
            hasEvade: true,
            extraAttacks: [],
            idleAnchorX: 95.33f,
            flipH: false,
            frameAnchors: new Dictionary<string, float>
            {
                ["Damaged"] = 162.08f,
                ["Guard"] = 209.54f,
                ["Evade"] = 116.1f,
                ["Hit"] = 162.08f,
                ["Penetrate"] = 233.96f,
                ["Slash"] = 109.47f,
                ["Strike"] = 129.85f,
                ["S1"] = 640.4f,
                ["S2"] = 104f,
            });

    internal override SpriteVisualProfile SpriteProfile => Profile;
}

internal sealed partial class XiaoEgoCreatureVisuals :
    XiaoSceneCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/xiao_ego.tscn";

    protected override string ResolveCurrentAnimationLibrary() =>
        XiaoAnimationContract.StageTwoLibrary;
}

internal static class XiaoAnimationContract
{
    internal const string StageOneLibrary = "stage_1";
    internal const string StageTwoLibrary = "stage_2";

    internal const float AttackDurationSeconds = 0.8f;
    internal const float AttackSettlementDelaySeconds = 0.9f;
    internal const float HitDurationSeconds = 0.58f;
    internal const float WoundedDurationSeconds = 0.68f;
    internal const float GuardDurationSeconds = 0.62f;
    internal const float EvadeDurationSeconds = 0.62f;
    internal const float MoveDurationSeconds = 0.7f;

    internal static IReadOnlyList<string> StageOneAnimations { get; } =
    [
        "Idle", "Hit", "Wounded", "Guard", "Evade", "Move",
        "Penetrate", "Slash", "Strike", "S1", "S2",
    ];

    internal static IReadOnlyList<string> StageTwoAnimations { get; } =
    [
        "Idle", "Hit", "Wounded", "Guard", "Evade", "Move",
        "Penetrate", "Slash", "Strike", "S1", "S2", "S3", "S4",
        "S5", "Special",
    ];

    internal static float DurationForTrigger(string triggerName) =>
        triggerName switch
        {
            "Hit" => HitDurationSeconds,
            "Wounded" => WoundedDurationSeconds,
            "Guard" => GuardDurationSeconds,
            "Evade" => EvadeDurationSeconds,
            "Move" => MoveDurationSeconds,
            _ => AttackDurationSeconds,
        };
}

internal static class XiaoGuestVisualProfile
{
    internal static SpriteVisualProfile Build(
        string root,
        string prefix,
        bool hasMove,
        bool hasEvade,
        IReadOnlyList<string> extraAttacks,
        float idleAnchorX,
        bool flipH,
        IReadOnlyDictionary<string, float> frameAnchors,
        IReadOnlyDictionary<string, float>? frameOffsetsY = null)
    {
        var profile = new SpriteVisualProfile();
        SpriteVisualVariantDefinition variant = profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            Path(root, prefix, "Default"))
            .AnchorX(idleAnchorX);
        if (flipH)
        {
            variant.Flip();
        }

        AddSwap(profile, root, prefix, frameAnchors, "Hit", 0.58f, "Hit", "Damaged");
        AddSwap(profile, root, prefix, frameAnchors, "Damaged", 0.68f, "Wounded");
        AddSwap(profile, root, prefix, frameAnchors, "Guard", 0.62f, "Guard", "Block");
        if (hasEvade)
        {
            AddSwap(profile, root, prefix, frameAnchors, "Evade", 0.62f, "Evade", "Dodge");
        }
        if (hasMove)
        {
            AddSwap(profile, root, prefix, frameAnchors, "Move", 0.7f, "Move");
        }

        AddLunge(profile, root, prefix, frameAnchors, frameOffsetsY, "Slash");
        AddLunge(profile, root, prefix, frameAnchors, frameOffsetsY, "Penetrate");
        AddLunge(profile, root, prefix, frameAnchors, frameOffsetsY, "Strike");
        AddLunge(profile, root, prefix, frameAnchors, frameOffsetsY, "S1");
        AddLunge(profile, root, prefix, frameAnchors, frameOffsetsY, "S2");
        foreach (string attack in extraAttacks)
        {
            AddLunge(profile, root, prefix, frameAnchors, frameOffsetsY, attack);
        }

        profile.Validate();
        return profile;
    }

    private static void AddSwap(
        SpriteVisualProfile profile,
        string root,
        string prefix,
        IReadOnlyDictionary<string, float> frameAnchors,
        string suffix,
        float duration,
        params string[] triggers)
    {
        string key = suffix.ToLowerInvariant();
        SpriteFrameDefinition frame =
            profile.Frame(key, Path(root, prefix, suffix));
        if (frameAnchors.TryGetValue(suffix, out float anchorX))
        {
            frame.AnchorX(anchorX);
        }
        profile.Swap(key, duration, triggers);
    }

    private static void AddLunge(
        SpriteVisualProfile profile,
        string root,
        string prefix,
        IReadOnlyDictionary<string, float> frameAnchors,
        IReadOnlyDictionary<string, float>? frameOffsetsY,
        string suffix)
    {
        string key = "attack_" + suffix.ToLowerInvariant();
        SpriteFrameDefinition frame =
            profile.Frame(key, Path(root, prefix, suffix));
        if (frameAnchors.TryGetValue(suffix, out float anchorX))
        {
            frame.AnchorX(anchorX);
        }
        if (frameOffsetsY is { } offsetsY
            && offsetsY.TryGetValue(suffix, out float offsetY))
        {
            frame.OffsetY(offsetY);
        }
        profile.Lunge(key, 0.1f, 0.12f, 0.12f, suffix);
    }

    private static string Path(string root, string prefix, string suffix) =>
        root + prefix + "_" + suffix + ".png";
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.CreateVisuals))]
[HarmonyPriority(Priority.Low)]
[LibraryPatch(Reason = "原版 CreateVisuals 非虚，晓与米莉丝的外观由代码或场景拼装；只作用于晓特殊来宾的三只怪物。")]
internal static class XiaoSpecialGuestCreateVisualsPatch
{
    private static readonly CreatureVisualLayout MirisLayout = new(
        SpritePos: new Vector2(-2.5f, -70f),
        SpriteScale: new Vector2(0.48f, 0.48f),
        BoundsLeft: -128f,
        BoundsTop: -275f,
        BoundsRight: 128f,
        BoundsBottom: 15f,
        CenterPos: new Vector2(0f, -165f),
        IntentPos: new Vector2(40f, -335f))
    {
        TalkPos = new Vector2(0f, -245f),
        StateDisplayLiftY = 40f,
    };

    [HarmonyPrefix]
    private static bool Prefix(MonsterModel __instance, ref NCreatureVisuals __result)
    {
        switch (__instance)
        {
            case XiaoStageOne:
                __result = XiaoGuestVisualFactory
                    .CreateScene<XiaoStageOneCreatureVisuals>(
                    __instance.Id.Entry,
                    XiaoStageOneCreatureVisuals.ScenePath);
                return false;
            case Miris:
                __result = XiaoGuestVisualFactory.Create<MirisCreatureVisuals>(
                    __instance.Id.Entry,
                    MirisLayout,
                    MirisCreatureVisuals.Profile);
                return false;
            case XiaoEgo:
                __result = XiaoGuestVisualFactory
                    .CreateScene<XiaoEgoCreatureVisuals>(
                    __instance.Id.Entry,
                    XiaoEgoCreatureVisuals.ScenePath);
                return false;
            default:
                return true;
        }
    }
}

internal static class XiaoGuestVisualFactory
{
    private const float XiaoStateDisplayLiftY = 40f;

    internal static NCreatureVisuals CreateScene<TVisuals>(
        string id,
        string scenePath)
        where TVisuals : XiaoSceneCreatureVisuals, new()
    {
        NCreatureVisuals visuals = WrappedMonsterVisualFactory
            .CreateSceneBackedVisuals<TVisuals>(id, scenePath);
        visuals.AddChild(
            new CreatureStateDisplayOffset
            {
                Name = "StateDisplayOffset",
                LiftY = XiaoStateDisplayLiftY,
            });
        return visuals;
    }

    internal static NCreatureVisuals Create<TVisuals>(
        string id,
        CreatureVisualLayout layout,
        SpriteVisualProfile profile)
        where TVisuals : SpriteAttackCreatureVisuals, new()
    {
        var visuals = new TVisuals { Name = id };
        if (!ReferenceEquals(visuals.SpriteProfile, profile))
        {
            throw new InvalidOperationException(
                $"Xiao visual profile mismatch for {id}.");
        }

        var motionRoot = new Node2D
        {
            Name = "MotionRoot",
            UniqueNameInOwner = true,
        };
        var idle = CreateSprite("Visuals", layout, visible: true);
        var attack = CreateSprite("AttackVisuals", layout, visible: false);
        GodotTextureSafety.TrySetTexture(
            idle,
            ResourceLoader.Load<Texture2D>(
                profile.DefaultIdleTexturePath));
        motionRoot.AddChild(idle);
        motionRoot.AddChild(attack);
        visuals.AddChild(motionRoot);
        visuals.AddChild(CreateBounds(layout));
        visuals.AddChild(
            new Marker2D
            {
                Name = "CenterPos",
                UniqueNameInOwner = true,
                Position = layout.CenterPos,
            });
        visuals.AddChild(
            new Marker2D
            {
                Name = "IntentPos",
                UniqueNameInOwner = true,
                Position = layout.IntentPos,
            });
        if (layout.TalkPos.HasValue)
        {
            visuals.AddChild(
                new Marker2D
                {
                    Name = "TalkPos",
                    UniqueNameInOwner = true,
                    Position = layout.TalkPos.Value,
                });
        }
        if (!Mathf.IsZeroApprox(layout.StateDisplayLiftY))
        {
            visuals.AddChild(
                new CreatureStateDisplayOffset
                {
                    Name = "StateDisplayOffset",
                    LiftY = layout.StateDisplayLiftY,
                });
        }

        foreach (Node child in visuals.GetChildren())
        {
            AssignOwner(child, visuals);
        }
        return visuals;
    }

    private static Sprite2D CreateSprite(
        string name,
        CreatureVisualLayout layout,
        bool visible) =>
        new()
        {
            Name = name,
            UniqueNameInOwner = true,
            Position = layout.SpritePos,
            Scale = layout.SpriteScale,
            Visible = visible,
        };

    private static Control CreateBounds(CreatureVisualLayout layout) =>
        new()
        {
            Name = "Bounds",
            UniqueNameInOwner = true,
            LayoutMode = 3,
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = layout.BoundsLeft,
            OffsetTop = layout.BoundsTop,
            OffsetRight = layout.BoundsRight,
            OffsetBottom = layout.BoundsBottom,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

    private static void AssignOwner(Node node, Node owner)
    {
        node.Owner = owner;
        foreach (Node child in node.GetChildren())
        {
            AssignOwner(child, owner);
        }
    }
}

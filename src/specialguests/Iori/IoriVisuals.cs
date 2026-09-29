using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.patches;
using LibraryOfRuina.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.specialguests.Iori;

internal static class IoriPresentationAssets
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/iori.tscn";
    internal const string SlashAnimationLibrary =
        "res://scenes/creature_visuals/iori_slash_animations.tres";
    internal const string PierceAnimationLibrary =
        "res://scenes/creature_visuals/iori_pierce_animations.tres";
    internal const string BluntAnimationLibrary =
        "res://scenes/creature_visuals/iori_blunt_animations.tres";
    internal const string DefenseAnimationLibrary =
        "res://scenes/creature_visuals/iori_defense_animations.tres";

    internal const string FrameRoot =
        "res://images/special_guests/iori/combat/frames/";
    internal const string VfxRoot =
        "res://images/special_guests/iori/combat/vfx/";
    internal const string AudioRoot =
        "res://audio/special_guests/iori/combat/";

    internal static IReadOnlyList<string> All { get; } =
    [
        ScenePath,
        SlashAnimationLibrary,
        PierceAnimationLibrary,
        BluntAnimationLibrary,
        DefenseAnimationLibrary,
        FrameRoot + "iori_hit.png",
        FrameRoot + "iori_blunt.png",
        FrameRoot + "iori_blunt_stance_blunt.png",
        FrameRoot + "iori_idle_blunt.png",
        FrameRoot + "iori_guard.png",
        FrameRoot + "iori_slash.png",
        FrameRoot + "iori_slash_stance_s1.png",
        FrameRoot + "iori_guard_slash.png",
        FrameRoot + "iori_slash_stance_slash.png",
        FrameRoot + "iori_idle_slash.png",
        FrameRoot + "iori_pierce.png",
        FrameRoot + "iori_pierce_stance_s1.png",
        FrameRoot + "iori_pierce_stance_s2.png",
        FrameRoot + "iori_guard_pierce.png",
        FrameRoot + "iori_idle_pierce.png",
        FrameRoot + "iori_evade.png",
        FrameRoot + "iori_idle_defense.png",
        FrameRoot + "iori_idle_neutral.png",
        VfxRoot + "FX_Tex_Mon_PurpleTear_Trail1.png",
        VfxRoot + "FX_Tex_Mon_PurpleTear_Trail1_Mask.png",
        VfxRoot + "PurpleTear_MirageTex.png",
        AudioRoot + "Purple_Guard.ogg",
        AudioRoot + "Purple_Hit_Hori.ogg",
        AudioRoot + "Purple_Hit_Vert.ogg",
        AudioRoot + "Purple_Slash_Hori.ogg",
        AudioRoot + "Purple_Slash_VertDown.ogg",
        AudioRoot + "Purple_Slash_VertUp.ogg",
        AudioRoot + "Purple_Stab_Stab1.ogg",
        AudioRoot + "Purple_Stab_Stab2.ogg",
        AudioRoot + "Purple_Warp.ogg",
    ];
}

internal sealed partial class IoriCreatureVisuals
    : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath = IoriPresentationAssets.ScenePath;
    internal const float StateDisplayLiftY = 40f;

    internal static readonly Vector2 SceneMotionRootPosition =
        new(0f, -20f);
    internal static readonly Vector2 SceneMotionRootScale =
        new(0.55f, 0.55f);
    internal static readonly Vector2 SceneCenterPosition =
        new(0f, -175f);
    internal static readonly Vector2 SceneIntentPosition =
        new(0f, -385f);
    internal static readonly Vector2 SceneTalkPosition =
        new(0f, -285f);
    internal static readonly Vector2I StandardFrameCanvas =
        new(1536, 1152);
    internal static readonly Vector2 StandardFrameFaceAnchor =
        new(768f, 500f);
    internal static readonly Vector2 StandardFrameCharacterOrigin =
        new(768f, 900f);

    protected override string ResolveCurrentAnimationLibrary() =>
        IoriAnimationContract.LibraryForStance(ResolveStance());

    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName switch
        {
            "Default" or "Standing" => "Idle",
            "Damaged" or "Hurt" or "Wounded" => "Hit",
            "Block" or "Defend" or "Buff" or "Debuff" or "Cast" or
                "Heal" => "Guard",
            "Dodge" => "Evade",
            "AttackSlash" => "Slash",
            "Penetrate" or "AttackPierce" or "AttackThrust" => "Pierce",
            "Strike" or "AttackBlunt" => "Blunt",
            "Phase" or "Stance" => "StanceChange",
            "Attack" => IoriAnimationContract.AttackTriggerForStance(
                ResolveStance()),
            _ => triggerName,
        };

    internal static NCreatureVisuals Create(string id)
    {
        NCreatureVisuals visuals = WrappedMonsterVisualFactory
            .CreateSceneBackedVisuals<IoriCreatureVisuals>(id, ScenePath);
        visuals.AddChild(
            new CreatureStateDisplayOffset
            {
                Name = "StateDisplayOffset",
                LiftY = StateDisplayLiftY,
            });
        return visuals;
    }

    internal static void RefreshStance(Creature creature)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals
            is IoriCreatureVisuals visuals)
        {
            visuals.TryPlayTrigger("Idle");
        }
    }

    internal static void BeginAttackChain(Creature creature)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals
            is IContinuousAttackVisuals visuals)
        {
            visuals.BeginContinuousAttackChain();
        }
    }

    internal static void EndAttackChain(Creature creature)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals
            is IContinuousAttackVisuals visuals)
        {
            visuals.EndContinuousAttackChain();
        }
    }

    private IoriStance ResolveStance() =>
        (GetParent() as NCreature)?.Entity?.Monster
            is IoriMonsterBase iori
                ? iori.CurrentStance
                : IoriStance.Slash;
}

internal static class IoriAnimationContract
{
    internal const string SlashLibrary = "slash";
    internal const string PierceLibrary = "pierce";
    internal const string BluntLibrary = "blunt";
    internal const string DefenseLibrary = "defense";

    internal const float ActionDurationSeconds = 1f;
    internal const float PhantomDanceDurationSeconds =
        ActionDurationSeconds * 4f;

    internal static IReadOnlyList<string> Libraries { get; } =
        [SlashLibrary, PierceLibrary, BluntLibrary, DefenseLibrary];

    internal static IReadOnlyList<string> Animations { get; } =
    [
        "Idle",
        "StanceChange",
        "Slash",
        "SlashS1",
        "Pierce",
        "PierceS1",
        "PierceS2",
        "Blunt",
        "BluntStance",
        "Guard",
        "Hit",
        "Evade",
        "PhantomDanceSlashA",
        "PhantomDanceBlunt",
        "PhantomDancePierce",
        "PhantomDanceSlashB",
    ];

    internal static string LibraryForStance(IoriStance stance) =>
        stance switch
        {
            IoriStance.Pierce => PierceLibrary,
            IoriStance.Blunt => BluntLibrary,
            IoriStance.Defense => DefenseLibrary,
            _ => SlashLibrary,
        };

    internal static string AttackTriggerForStance(IoriStance stance) =>
        stance switch
        {
            IoriStance.Pierce => "Pierce",
            IoriStance.Blunt => "Blunt",
            _ => "Slash",
        };

    internal static string QualifiedAnimationName(
        IoriStance stance,
        string triggerName) =>
        $"{LibraryForStance(stance)}/{triggerName}";

    internal static float DurationForTrigger(string triggerName) =>
        string.Equals(triggerName, "Idle", StringComparison.Ordinal)
            ? throw new ArgumentOutOfRangeException(
                nameof(triggerName),
                triggerName,
                "Idle loops and has no settlement duration.")
            : ActionDurationSeconds;
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.CreateVisuals))]
[HarmonyPriority(Priority.Low)]
[LibraryPatch(Reason = "原版 CreateVisuals 非虚，伊织外观由代码拼装；只作用于 IoriMonsterBase。")]
internal static class IoriSpecialGuestCreateVisualsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        MonsterModel __instance,
        ref NCreatureVisuals __result)
    {
        if (__instance is not IoriMonsterBase)
        {
            return true;
        }

        __result = IoriCreatureVisuals.Create(__instance.Id.Entry);
        return false;
    }
}

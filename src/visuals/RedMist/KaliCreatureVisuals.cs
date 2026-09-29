using System;
using Godot;
using LibraryOfRuina.patches;
using LibraryOfRuina.specialguests.Kali;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.visuals.RedMist;

[MonsterVisual(typeof(Kali), ScenePath = KaliCreatureVisuals.ScenePath)]
internal sealed partial class KaliCreatureVisuals
    : SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/kali.tscn";
    internal static readonly Vector2 SceneMotionRootPosition = new(0f, -30f);
    internal static readonly Vector2 SceneCenterPosition = new(-80f, -145f);
    internal static readonly Vector2 SceneIntentPosition = new(-80f, -380f);
    internal static readonly Vector2 SceneTalkPosition = new(-80f, -280f);
    internal const float StateDisplayLiftY = 40f;

    internal const float BloodMistAnimationSeconds =
        KaliAnimationContract.BloodMistDurationSeconds;
    internal const float FieldOfCorpsesAnimationSeconds =
        KaliAnimationContract.FieldOfCorpsesDurationSeconds;

    protected override string ResolveCurrentAnimationLibrary() =>
        KaliAnimationContract.LibraryForEgo(IsEgoActive());

    [MonsterVisualFactory]
    internal static NCreatureVisuals CreateForMonster(MonsterModel monster) =>
        Create(monster.Id.Entry);

    internal static NCreatureVisuals Create(string id)
    {
        NCreatureVisuals visuals = WrappedMonsterVisualFactory
            .CreateSceneBackedVisuals<KaliCreatureVisuals>(id, ScenePath);
        visuals.AddChild(
            new CreatureStateDisplayOffset
            {
                Name = "StateDisplayOffset",
                LiftY = StateDisplayLiftY,
            });
        return visuals;
    }

    protected override string NormalizeTriggerName(string triggerName) =>
        triggerName switch
        {
            "Penetrate" => "AttackPierce",
            "Strike" => "AttackBlunt",
            "Slash" => "AttackSlash",
            "Block" => "Guard",
            "Damaged" or "Wounded" => "Hit",
            "Dodge" => "Evade",
            _ => triggerName,
        };

    internal static void SetEgoState(Creature creature, bool active)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals
            is KaliCreatureVisuals visuals)
        {
            // The model owns the form. The scene only switches to that
            // form's editable animation library and refreshes its Idle.
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

    internal static string ResolveAnimationName(
        bool egoActive,
        string triggerName) =>
        KaliAnimationContract.QualifiedAnimationName(
            egoActive,
            triggerName);

    private bool IsEgoActive()
    {
        Creature? creature = (GetParent() as NCreature)?.Entity;
        return creature?.Monster is Kali { IsEgoActive: true };
    }
}

internal static class KaliAnimationContract
{
    internal const string NormalLibrary = "normal";
    internal const string EgoLibrary = "ego";

    internal const float AttackDurationSeconds = 0.46f;
    internal const float BloodMistDurationSeconds = 3.6f;
    internal const float FieldOfCorpsesDurationSeconds = 1.4f;
    internal const float GuardDurationSeconds = 0.62f;
    internal const float HitDurationSeconds = 0.58f;
    internal const float MoveDurationSeconds = 0.7f;
    internal const float EvadeDurationSeconds = 0.62f;

    private static readonly string[] AnimationNames =
    [
        "Idle",
        "AttackPierce",
        "AttackBlunt",
        "AttackSlash",
        "Attack",
        "BloodMist",
        "FieldOfCorpses",
        "Guard",
        "Hit",
        "Move",
        "Evade",
    ];

    internal static IReadOnlyList<string> Libraries { get; } =
        [NormalLibrary, EgoLibrary];

    internal static IReadOnlyList<string> Animations => AnimationNames;

    internal static string LibraryForEgo(bool egoActive) =>
        egoActive ? EgoLibrary : NormalLibrary;

    internal static string QualifiedAnimationName(
        bool egoActive,
        string triggerName) =>
        $"{LibraryForEgo(egoActive)}/{triggerName}";

    internal static float DurationForTrigger(string triggerName) =>
        triggerName switch
        {
            "AttackPierce" or "AttackBlunt" or "AttackSlash" or "Attack" =>
                AttackDurationSeconds,
            "BloodMist" => BloodMistDurationSeconds,
            "FieldOfCorpses" => FieldOfCorpsesDurationSeconds,
            "Guard" => GuardDurationSeconds,
            "Hit" => HitDurationSeconds,
            "Move" => MoveDurationSeconds,
            "Evade" => EvadeDurationSeconds,
            _ => throw new ArgumentOutOfRangeException(
                nameof(triggerName),
                triggerName,
                "Only non-idle Kali actions have a duration."),
        };
}

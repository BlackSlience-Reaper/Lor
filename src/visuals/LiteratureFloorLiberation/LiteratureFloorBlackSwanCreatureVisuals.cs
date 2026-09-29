using System;
using Godot;
using LibraryOfRuina.monsters.LiteratureFloorLiberation;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.LiteratureFloorLiberation;

[MonsterVisual(typeof(LiteratureFloorBlackSwanBoss), ScenePath = LiteratureFloorBlackSwanCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorBlackSwanCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/literature_floor_black_swan_boss.tscn";

    protected override string ResolveCurrentAnimationLibrary() =>
        LiteratureFloorBlackSwanAnimationContract.Library;
}

internal static class LiteratureFloorBlackSwanAnimationContract
{
    internal const string Library = "black_swan";
    internal const float SlashDurationSeconds = 0.52f;
    internal const float PierceDurationSeconds = 0.58f;
    internal const float GuardDurationSeconds = 0.72f;
    internal const float SpecialDurationSeconds = 1.10f;
    internal const float HitDurationSeconds = 0.18f;

    internal static IReadOnlyList<string> Animations { get; } =
    [
        "Idle",
        "SlashOne",
        "SlashTwo",
        "Pierce",
        "Guard",
        "Special",
        "Hit"
    ];

    internal static float DurationForTrigger(string triggerName) =>
        triggerName switch
        {
            "SlashOne" or "SlashTwo" => SlashDurationSeconds,
            "Pierce" => PierceDurationSeconds,
            "Guard" => GuardDurationSeconds,
            "Special" => SpecialDurationSeconds,
            "Hit" => HitDurationSeconds,
            _ => throw new ArgumentOutOfRangeException(
                nameof(triggerName),
                triggerName,
                "Only non-idle Black Swan actions have a duration.")
        };
}

[MonsterVisual(typeof(LiteratureFloorBlackSwanFirstBrother), ScenePath = LiteratureFloorBlackSwanBrotherCreatureVisuals.ScenePath)]
[MonsterVisual(typeof(LiteratureFloorBlackSwanSecondBrother), ScenePath = LiteratureFloorBlackSwanBrotherCreatureVisuals.ScenePath)]
[MonsterVisual(typeof(LiteratureFloorBlackSwanThirdBrother), ScenePath = LiteratureFloorBlackSwanBrotherCreatureVisuals.ScenePath)]
[MonsterVisual(typeof(LiteratureFloorBlackSwanFourthBrother), ScenePath = LiteratureFloorBlackSwanBrotherCreatureVisuals.ScenePath)]
[MonsterVisual(typeof(LiteratureFloorBlackSwanFifthBrother), ScenePath = LiteratureFloorBlackSwanBrotherCreatureVisuals.ScenePath)]
[MonsterVisual(typeof(LiteratureFloorBlackSwanSixthBrother), ScenePath = LiteratureFloorBlackSwanBrotherCreatureVisuals.ScenePath)]
internal sealed partial class
    LiteratureFloorBlackSwanBrotherCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    internal const string ScenePath =
        "res://scenes/creature_visuals/literature_floor_black_swan_brother.tscn";

    protected override string ResolveCurrentAnimationLibrary()
    {
        if (GetParent()
                is NCreature node
            && node.Entity.Monster
                is LiteratureFloorBlackSwanBrotherBase brother)
        {
            return LibraryForBrother(brother.BrotherNumber);
        }

        return LibraryForBrother(1);
    }

    internal static string LibraryForBrother(int brotherNumber) =>
        $"brother_{Math.Clamp(brotherNumber, 1, 6)}";

    internal void ShowInactiveOrDeadIdle(
        LiteratureFloorBlackSwanBrotherBase brother)
    {
        AnimationPlayer.Stop();

        Sprite2D visuals = GetNode<Sprite2D>("%Visuals");
        Sprite2D attackVisuals = GetNode<Sprite2D>("%AttackVisuals");
        visuals.Texture = ResourceLoader.Load<Texture2D>(
            brother.IdleTexturePath);
        visuals.Position = new Vector2(0f, -126f);
        float scale = brother.BrotherNumber == 1 ? 0.546f : 0.504f;
        visuals.Scale = Vector2.One * scale;
        visuals.Visible = true;
        attackVisuals.Visible = false;
    }
}

internal static class LiteratureFloorBlackSwanBrotherAnimationContract
{
    internal const float AttackDurationSeconds = 0.50f;
    internal const float HitDurationSeconds = 0.18f;

    internal static IReadOnlyList<string> Animations { get; } =
        ["Idle", "Attack", "AttackAlt", "Hit"];
}

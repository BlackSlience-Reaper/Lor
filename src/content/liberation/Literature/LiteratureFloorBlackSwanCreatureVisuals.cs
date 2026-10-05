using System;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Literature;

[MonsterVisual(typeof(LiteratureFloorBlackSwanBoss), ScenePath = LiteratureFloorBlackSwanCreatureVisuals.ScenePath)]
internal sealed partial class LiteratureFloorBlackSwanCreatureVisuals :
    SceneAnimatedCreatureVisuals
{
    // Spine 身体的触发与动画契约同名；多段招式的换姿势时刻照场景动画，见 tools/spine_from_layers/build_boss_configs.py
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "literature_floor_liberation",
        "black_swan",
        "slash_one",
        new Dictionary<string, string>
        {
            ["SlashOne"] = "slash_one",
            ["SlashTwo"] = "slash_two",
            ["Pierce"] = "pierce",
            ["Guard"] = "guard",
            ["Special"] = "special",
        });

    internal override RuntimeSpineBody.Spec? SpineSpec => Spine;

    internal const string ScenePath =
        LiteratureFloorAssets.BlackSwanBossScene;

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
        LiteratureFloorAssets.BlackSwanBrotherScene;

    // 一到五哥战斗中共用同一套图，六哥带笑另一套；原版只有待机分层，攻击、受击是整图
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "literature_floor_liberation",
        "swan_bro",
        "attack",
        new Dictionary<string, string>
        {
            ["AttackAlt"] = "attack_alt",
        });

    internal static readonly RuntimeSpineBody.Spec SixthSpine = LayeredBossSpine.Create(
        "literature_floor_liberation",
        "swan_bro_smile",
        "attack",
        new Dictionary<string, string>
        {
            ["AttackAlt"] = "attack_alt",
        });

    internal override RuntimeSpineBody.Spec? SpineSpecFor(string library) =>
        library == LibraryForBrother(6) ? SixthSpine : Spine;

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
        // 各自的倒下待机图（带黏液）只有整图，骨架藏掉
        DetachSpineBody();
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

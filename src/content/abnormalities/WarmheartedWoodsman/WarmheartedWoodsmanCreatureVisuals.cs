using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;

/// <summary>
/// 热心的樵夫的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，各姿势按标注点对齐、动作不转身体），
/// 加载失败时退回下面的逐帧换图。无心、有心两张待机各一副骨架。原来动作图只有待机的 0.725 倍，骨架里放大回同大。
/// </summary>
public sealed partial class WarmheartedWoodsmanCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    private static Dictionary<string, string> SpineTriggers() => new()
    {
        ["AttackBlunt"] = "blunt",
        ["AttackSlash"] = "slash",
        ["LoggingFinal"] = "logging",
        ["Guard"] = "guard",
        ["Cast"] = "guard",
    };

    internal static readonly RuntimeSpineBody.Spec EmptySpine = LayeredBossSpine.Create(
        "warmhearted_woodsman", "woodsman_empty", "blunt", SpineTriggers());

    internal static readonly RuntimeSpineBody.Spec WarmSpine = LayeredBossSpine.Create(
        "warmhearted_woodsman", "woodsman_warm", "blunt", SpineTriggers());

    internal override RuntimeSpineBody.Spec SpineSpec => EmptySpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [EmptySpine, WarmSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) =>
        variantKey == WarmVariant ? WarmSpine : EmptySpine;

    [MonsterVisual(typeof(WarmheartedWoodsman))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -8f), new(0.87f, 0.87f), -190f, -430f, 190f, 16f, new(0f, -190f), new(0f, -455f))
    {
        TalkPos = new Vector2(0f, -360f),
        StateDisplayLiftY = 40f,
    };

    // 按樵夫当前是否持有温暖之心选初始立绘与 _Ready 时的变体。
    [MonsterVisualFactory]
    internal static NCreatureVisuals CreateForMonster(MonsterModel monster)
    {
        string id = monster.Id.Entry;
        return WrappedMonsterVisualFactory.CreateScriptedSpriteVisuals<WarmheartedWoodsmanCreatureVisuals>(
            id,
            monster is WarmheartedWoodsman { HasWarmHeart: true }
                ? WarmheartedWoodsman.WarmIdleTexturePath
                : WarmheartedWoodsman.EmptyIdleTexturePath,
            visuals => visuals.StartsWarm = monster is WarmheartedWoodsman { HasWarmHeart: true });
    }

    private const string EmptyVariant = "empty";
    private const string WarmVariant = "warm";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    public bool StartsWarm { get; set; }

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        SetSpriteVisualVariant(
            StartsWarm ? WarmVariant : EmptyVariant);
    }

    protected override void BeforeResolveSpriteTrigger(string triggerName)
    {
        switch (triggerName)
        {
            case "EmptyIdle":
                SetSpriteVisualVariant(EmptyVariant);
                break;
            case "WarmIdle":
                SetSpriteVisualVariant(WarmVariant);
                break;
        }
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                EmptyVariant,
                WarmheartedWoodsman
                    .EmptyIdleTexturePath)
            .Scale(1.2f)
            .IdleOnly();
        profile.Variant(
                WarmVariant,
                WarmheartedWoodsman
                    .WarmIdleTexturePath)
            .Scale(1.2f)
            .IdleOnly();
        profile.InitialVariant(EmptyVariant);
        profile.Frame(
                "logging_final",
                WarmheartedWoodsman
                    .LoggingFinalTexturePath)
            .Nudge(-190f, 145f);
        profile.Frame(
                "blunt",
                WarmheartedWoodsman
                    .AttackBluntTexturePath)
            .Nudge(90f, 35f);
        profile.Frame(
                "slash",
                WarmheartedWoodsman
                    .AttackSlashTexturePath)
            .Nudge(-10f, 35f);
        profile.Frame(
            "hit",
            WarmheartedWoodsman
                .HitTexturePath);
        profile.Frame(
            "guard",
            WarmheartedWoodsman
                .GuardTexturePath);
        profile.Swap("blunt", 0.38f, "AttackBlunt");
        profile.Swap("slash", 0.38f, "AttackSlash");
        profile.Swap("logging_final", 0.48f, "LoggingFinal");
        profile.Swap("hit", 0.34f, "Hit");
        profile.Swap("guard", 0.38f, "Guard", "Cast");
        return profile;
    }
}

/// <summary>樵夫的树：Spine 身体见 <see cref="LayeredBossSpine"/>（整块，绕根部慢慢摇），加载失败时退回贴图。</summary>
public sealed partial class WoodsmanTreeCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "warmhearted_woodsman",
        "woodsman_tree",
        "guard",
        new Dictionary<string, string>
        {
            ["Guard"] = "guard",
            ["Cast"] = "guard",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(WoodsmanTree))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -10f), new(0.36f, 0.36f), -190f, -419f, 185f, 13f, new(0f, -200f), new(0f, -455f))
    {
        StateDisplayLiftY = 18f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            WoodsmanTree.IdleTexturePath);
        string idle =
            $"@idle:{SpriteVisualProfile.DefaultVariantKey}";
        profile.Swap(idle, 0.24f, "Hit");
        profile.Swap(idle, 0.28f, "Guard", "Cast");
        return profile;
    }
}

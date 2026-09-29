using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;

public sealed partial class WarmheartedWoodsmanCreatureVisuals
    : SpriteAttackCreatureVisuals
{
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

public sealed partial class WoodsmanTreeCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(WoodsmanTree))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -10f), new(0.36f, 0.36f), -190f, -420f, 190f, 12f, new(0f, -200f), new(0f, -455f))
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

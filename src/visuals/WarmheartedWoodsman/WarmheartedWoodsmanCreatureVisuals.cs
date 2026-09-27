using LibraryOfRuina.monsters.WarmheartedWoodsman;

namespace LibraryOfRuina.visuals.WarmheartedWoodsman;

public sealed partial class WarmheartedWoodsmanCreatureVisuals
    : SpriteAttackCreatureVisuals
{
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
                monsters.WarmheartedWoodsman.WarmheartedWoodsman
                    .EmptyIdleTexturePath)
            .Scale(1.2f)
            .IdleOnly();
        profile.Variant(
                WarmVariant,
                monsters.WarmheartedWoodsman.WarmheartedWoodsman
                    .WarmIdleTexturePath)
            .Scale(1.2f)
            .IdleOnly();
        profile.InitialVariant(EmptyVariant);
        profile.Frame(
                "logging_final",
                monsters.WarmheartedWoodsman.WarmheartedWoodsman
                    .LoggingFinalTexturePath)
            .Nudge(-190f, 145f);
        profile.Frame(
                "blunt",
                monsters.WarmheartedWoodsman.WarmheartedWoodsman
                    .AttackBluntTexturePath)
            .Nudge(90f, 35f);
        profile.Frame(
                "slash",
                monsters.WarmheartedWoodsman.WarmheartedWoodsman
                    .AttackSlashTexturePath)
            .Nudge(-10f, 35f);
        profile.Frame(
            "hit",
            monsters.WarmheartedWoodsman.WarmheartedWoodsman
                .HitTexturePath);
        profile.Frame(
            "guard",
            monsters.WarmheartedWoodsman.WarmheartedWoodsman
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

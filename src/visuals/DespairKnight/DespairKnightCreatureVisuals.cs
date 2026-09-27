using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.visuals.DespairKnight;

public sealed partial class DespairKnightCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    private const string NormalVariant = "normal";
    private const string StabbedOneVariant = "stabbed_one";
    private const string StabbedTwoVariant = "stabbed_two";
    private const string StabbedThreeVariant = "stabbed_three";
    private const string DespairVariant = "despair";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        RefreshVariant();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        RefreshVariant();
    }

    protected override void BeforeResolveSpriteTrigger(string triggerName)
    {
        RefreshVariant();
    }

    private void RefreshVariant()
    {
        string variant = ResolveVariant();
        if (CurrentSpriteVariantKey != variant)
        {
            SetSpriteVisualVariant(variant);
        }
    }

    private string ResolveVariant()
    {
        if (GetParent() is NCreature
            {
                Entity.Monster: monsters.DespairKnight.DespairKnight knight
            })
        {
            return knight.ResolveIdleTexturePath() switch
            {
                monsters.DespairKnight.DespairKnight.StabbedOneTexturePath =>
                    StabbedOneVariant,
                monsters.DespairKnight.DespairKnight.StabbedTwoTexturePath =>
                    StabbedTwoVariant,
                monsters.DespairKnight.DespairKnight.StabbedThreeTexturePath =>
                    StabbedThreeVariant,
                monsters.DespairKnight.DespairKnight.DespairTexturePath =>
                    DespairVariant,
                _ => NormalVariant
            };
        }

        return NormalVariant;
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            NormalVariant,
            monsters.DespairKnight.DespairKnight.IdleTexturePath);
        profile.Variant(
            StabbedOneVariant,
            monsters.DespairKnight.DespairKnight.StabbedOneTexturePath);
        profile.Variant(
            StabbedTwoVariant,
            monsters.DespairKnight.DespairKnight.StabbedTwoTexturePath);
        profile.Variant(
            StabbedThreeVariant,
            monsters.DespairKnight.DespairKnight.StabbedThreeTexturePath);
        profile.Variant(
            DespairVariant,
            monsters.DespairKnight.DespairKnight.DespairTexturePath);
        profile.InitialVariant(NormalVariant);

        AddCurrentIdleAnimation(profile, NormalVariant);
        AddCurrentIdleAnimation(profile, StabbedOneVariant);
        AddCurrentIdleAnimation(profile, StabbedTwoVariant);
        AddCurrentIdleAnimation(profile, StabbedThreeVariant);
        AddCurrentIdleAnimation(profile, DespairVariant);
        profile.Swap("@idle:despair", 0.5f, "Despair");
        return profile;
    }

    private static void AddCurrentIdleAnimation(
        SpriteVisualProfile profile,
        string variant)
    {
        profile.Swap($"@idle:{variant}", 0.5f, "Cast", "Hit")
            .ForVariant(variant);
    }
}

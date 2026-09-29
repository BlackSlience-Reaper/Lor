using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.DespairKnight;

public sealed partial class DespairKnightCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(DespairKnight))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 6f), new(0.77f, 0.77f), -181.5f, -336.7f, 181.5f, 12f, new(0f, -160.1f), new(0f, -369.7f))
    {
        TalkPos = new Vector2(0f, -286.6f),
    };

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
                Entity.Monster: DespairKnight knight
            })
        {
            return knight.ResolveIdleTexturePath() switch
            {
                DespairKnight.StabbedOneTexturePath =>
                    StabbedOneVariant,
                DespairKnight.StabbedTwoTexturePath =>
                    StabbedTwoVariant,
                DespairKnight.StabbedThreeTexturePath =>
                    StabbedThreeVariant,
                DespairKnight.DespairTexturePath =>
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
            DespairKnight.IdleTexturePath);
        profile.Variant(
            StabbedOneVariant,
            DespairKnight.StabbedOneTexturePath);
        profile.Variant(
            StabbedTwoVariant,
            DespairKnight.StabbedTwoTexturePath);
        profile.Variant(
            StabbedThreeVariant,
            DespairKnight.StabbedThreeTexturePath);
        profile.Variant(
            DespairVariant,
            DespairKnight.DespairTexturePath);
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

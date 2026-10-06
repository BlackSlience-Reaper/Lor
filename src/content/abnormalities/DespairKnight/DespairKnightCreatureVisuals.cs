using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.abnormalities.DespairKnight;

/// <summary>
/// 绝望骑士：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，整图只平移转动），加载失败时退回逐帧换图。
/// 平常、插了一到三把剑、绝望五个形态各一副骨架，每副只有待机、受击、死亡；施法与绝望触发原来就停在待机图，不映射。
/// 骑士本身不出手，"Attack" 万一收到也只播受击。
/// </summary>
public sealed partial class DespairKnightCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec NormalSpine = LayeredBossSpine.Create(
        "despair_knight", "despair_knight_normal", "hurt", new Dictionary<string, string>());

    internal static readonly RuntimeSpineBody.Spec StabbedOneSpine = LayeredBossSpine.Create(
        "despair_knight", "despair_knight_stabbedone", "hurt", new Dictionary<string, string>());

    internal static readonly RuntimeSpineBody.Spec StabbedTwoSpine = LayeredBossSpine.Create(
        "despair_knight", "despair_knight_stabbedtwo", "hurt", new Dictionary<string, string>());

    internal static readonly RuntimeSpineBody.Spec StabbedThreeSpine = LayeredBossSpine.Create(
        "despair_knight", "despair_knight_stabbedthree", "hurt", new Dictionary<string, string>());

    internal static readonly RuntimeSpineBody.Spec DespairSpine = LayeredBossSpine.Create(
        "despair_knight", "despair_knight_despair", "hurt", new Dictionary<string, string>());

    internal override RuntimeSpineBody.Spec SpineSpec => NormalSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        [NormalSpine, StabbedOneSpine, StabbedTwoSpine, StabbedThreeSpine, DespairSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) => variantKey switch
    {
        StabbedOneVariant => StabbedOneSpine,
        StabbedTwoVariant => StabbedTwoSpine,
        StabbedThreeVariant => StabbedThreeSpine,
        DespairVariant => DespairSpine,
        _ => NormalSpine,
    };

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

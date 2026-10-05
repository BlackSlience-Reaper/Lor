using Godot;
using LibraryOfRuina.content.abnormalities.DespairKnight;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.History;

/// <summary>
/// 遗忘骑士之剑的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，只平移、转动），加载失败时退回下面的逐帧换图。
/// 普通、泪滴、绝望三个形态各一副骨架；待机整把剑悬空轻晃。打击、斩击、突刺原图里的剑只画了一半大，骨架里放大到与待机同大。
/// </summary>
public sealed partial class ForgottenKnightSwordCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec NormalSpine = LayeredBossSpine.Create(
        "forgotten_knight_sword",
        "forgotten_sword_normal",
        "blunt",
        new Dictionary<string, string>
        {
            ["NormalBlunt"] = "blunt",
            ["NormalPierce"] = "pierce",
            ["NormalSlash"] = "slash",
            ["NormalParry"] = "parry",
            ["Cast"] = "parry",
        });

    internal static readonly RuntimeSpineBody.Spec TeardropSpine = LayeredBossSpine.Create(
        "forgotten_knight_sword",
        "forgotten_sword_teardrop",
        "blunt",
        new Dictionary<string, string>
        {
            ["TeardropBlunt"] = "blunt",
            ["TeardropPierce"] = "pierce",
            ["TeardropSlash"] = "slash",
            ["TeardropParry"] = "parry",
            ["Cast"] = "parry",
        });

    internal static readonly RuntimeSpineBody.Spec DespairSpine = LayeredBossSpine.Create(
        "forgotten_knight_sword",
        "forgotten_sword_despair",
        "attack",
        new Dictionary<string, string>
        {
            ["DespairAttack"] = "attack",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => NormalSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [NormalSpine, TeardropSpine, DespairSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) => variantKey switch
    {
        TeardropVariant => TeardropSpine,
        DespairVariant => DespairSpine,
        _ => NormalSpine,
    };

    [MonsterVisual(typeof(ForgottenKnightSword))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 6f), new(0.54f, 0.54f), -74f, -242f, 78f, 14f, new(0f, -120f), new(0f, -292f))
    {
        TalkPos = new Vector2(0f, -230f),
    };

    private const string NormalVariant = "normal";
    private const string TeardropVariant = "teardrop";
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
                Entity.Monster: ForgottenKnightSword sword
            })
        {
            if (sword.IsKnightInDespair)
            {
                return DespairVariant;
            }

            if (sword.HasTeardrop)
            {
                return TeardropVariant;
            }
        }

        return NormalVariant;
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            NormalVariant,
            ForgottenKnightSword.NormalIdleTexturePath);
        profile.Variant(
            TeardropVariant,
            ForgottenKnightSword.TeardropIdleTexturePath);
        profile.Variant(
            DespairVariant,
            ForgottenKnightSword.DespairIdleTexturePath);
        profile.InitialVariant(NormalVariant);

        AddAttackFrame(
            profile,
            "normal_blunt",
            ForgottenKnightSword.NormalBluntTexturePath);
        AddAttackFrame(
            profile,
            "normal_pierce",
            ForgottenKnightSword.NormalPierceTexturePath);
        AddAttackFrame(
            profile,
            "normal_slash",
            ForgottenKnightSword.NormalSlashTexturePath);
        profile.Frame(
            "normal_parry",
            ForgottenKnightSword.NormalParryTexturePath);
        profile.Frame(
            "normal_hit",
            ForgottenKnightSword.NormalHitTexturePath)
            .ForVariant(NormalVariant);

        AddAttackFrame(
            profile,
            "teardrop_blunt",
            ForgottenKnightSword.TeardropBluntTexturePath);
        AddAttackFrame(
            profile,
            "teardrop_pierce",
            ForgottenKnightSword.TeardropPierceTexturePath);
        AddAttackFrame(
            profile,
            "teardrop_slash",
            ForgottenKnightSword.TeardropSlashTexturePath);
        profile.Frame(
            "teardrop_parry",
            ForgottenKnightSword.TeardropParryTexturePath);
        profile.Frame(
            "teardrop_hit",
            ForgottenKnightSword.TeardropHitTexturePath)
            .ForVariant(TeardropVariant);

        AddAttackFrame(
            profile,
            "despair_attack",
            ForgottenKnightSword.DespairAttackTexturePath);
        profile.Frame(
            "despair_hit",
            ForgottenKnightSword.DespairHitTexturePath)
            .ForVariant(DespairVariant);

        profile.Swap("normal_blunt", 0.62f, "NormalBlunt");
        profile.Swap("normal_pierce", 0.62f, "NormalPierce");
        profile.Swap("normal_slash", 0.62f, "NormalSlash");
        profile.Swap("normal_parry", 0.62f, "NormalParry", "Cast");
        profile.Swap("teardrop_blunt", 0.62f, "TeardropBlunt");
        profile.Swap("teardrop_pierce", 0.62f, "TeardropPierce");
        profile.Swap("teardrop_slash", 0.62f, "TeardropSlash");
        profile.Swap("teardrop_parry", 0.62f, "TeardropParry");
        profile.Swap("despair_attack", 0.62f, "DespairAttack");
        profile.Swap("normal_hit", 0.36f, "Hit")
            .ForVariant(NormalVariant);
        profile.Swap("teardrop_hit", 0.36f, "Hit")
            .ForVariant(TeardropVariant);
        profile.Swap("despair_hit", 0.36f, "Hit")
            .ForVariant(DespairVariant);
        return profile;
    }

    private static void AddAttackFrame(
        SpriteVisualProfile profile,
        string key,
        string path)
    {
        profile.Frame(key, path).Nudge(-46f, -10f);
    }
}

using Godot;
using LibraryOfRuina.content.abnormalities.DespairKnight;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.History;

public sealed partial class ForgottenKnightSwordCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(ForgottenKnightSword))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 6f), new(0.54f, 0.54f), -120f, -265f, 120f, 14f, new(0f, -120f), new(0f, -292f))
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

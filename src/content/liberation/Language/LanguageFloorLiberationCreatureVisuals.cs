using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.liberation.Language;

/// <summary>猩红创痕的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class LanguageFloorScarletScarCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "language_floor_liberation",
        "scarlet_scar",
        "slash",
        new Dictionary<string, string>
        {
            ["Fire"] = "shot_1",
            ["ShootS1"] = "shot_1",
            ["ShootS2"] = "shot_2",
            ["ShootS3"] = "shot_3",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(LanguageFloorScarletScar))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 42f), new(-0.50f, 0.50f), -158f, -294f, 158f, 12f, new(0f, -76f), new(0f, -330f))
    {
        TalkPos = new Vector2(0f, -248f),
        StateDisplayLiftY = 18f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public void FacePlayers()
    {
        SetSpriteFlipH(true);
    }

    public void FacePartner()
    {
        SetSpriteFlipH(false);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        const string root =
            LanguageFloorAssets.LanguageFloorLiberationMonsterRoot;
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                root + "scarlet_scar.png")
            .AnchorX(280f);

        profile.Frame("slash", root + "scarlet_scar_slash.png")
            .AnchorX(650f);
        profile.Frame("hit", root + "scarlet_scar_hit.png")
            .AnchorX(340f);
        profile.Frame("shot_1", root + "scarlet_scar_s1.png")
            .AnchorX(220f);
        profile.Frame("shot_2", root + "scarlet_scar_s2.png")
            .AnchorX(1110f);
        profile.Frame("shot_3", root + "scarlet_scar_s3.png")
            .AnchorX(1120f);

        profile.Swap("slash", 0.42f, "Attack");
        profile.Swap("shot_1", 0.34f, "Fire", "ShootS1");
        profile.Swap("shot_2", 0.34f, "ShootS2");
        profile.Swap("shot_3", 0.34f, "ShootS3");
        profile.Swap("hit", 0.26f, "Hit");
        return profile;
    }
}

/// <summary>失去一切的狼的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class LanguageFloorLostEverythingWolfCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "language_floor_liberation",
        "cobalt_shadow",
        "attack",
        new Dictionary<string, string>
        {
            ["WolfSlash"] = "attack",
            ["Special"] = "howl",
            ["Howl"] = "howl",
            ["WolfS2"] = "howl",
            ["WolfHowl"] = "howl",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(LanguageFloorLostEverythingWolf))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 20f), new(0.62f, 0.62f), -235f, -317f, 235f, 12f, new(-20f, -86f), new(-70f, -300f))
    {
        TalkPos = new Vector2(-155f, -193f),
        StateDisplayLiftY = 12f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        const string root =
            LanguageFloorAssets.LanguageFloorLiberationMonsterRoot;
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                root + "lost_everything_wolf.png")
            .AnchorX(330f);

        profile.Frame(
                "attack",
                root + "lost_everything_wolf_attack.png")
            .AnchorX(700f);
        profile.Frame(
                "special",
                root + "lost_everything_wolf_howl.png")
            .AnchorX(560f);
        profile.Frame(
                "hit",
                root + "lost_everything_wolf_hit.png")
            .AnchorX(400f);

        profile.Swap("attack", 0.42f, "Attack", "WolfSlash");
        profile.Swap(
            "special",
            0.48f,
            "Special",
            "Howl",
            "WolfS2",
            "WolfHowl");
        profile.Swap("hit", 0.26f, "Hit");
        return profile;
    }
}

/// <summary>郁蓝创痕的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。各形态各一副骨架。</summary>
public sealed partial class LanguageFloorCobaltScarCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "language_floor_liberation",
        "cobalt_scar",
        "strike",
        new Dictionary<string, string>
        {
            ["CobaltStrike"] = "strike",
            ["CobaltSlash"] = "slash",
        });

    internal static readonly RuntimeSpineBody.Spec BigWolfSpine = LayeredBossSpine.Create(
        "language_floor_liberation",
        "cobalt_big_wolf",
        "slash",
        new Dictionary<string, string>
        {
            ["BigWolfStrike"] = "strike",
            ["BigWolfSlash"] = "slash",
            ["BigWolfGuard"] = "guard",
            ["BigWolfS1"] = "s1",
            ["BigWolfS2"] = "s2",
        });

    internal static readonly RuntimeSpineBody.Spec ShadowSpine = LayeredBossSpine.Create(
        "language_floor_liberation",
        "cobalt_shadow",
        "attack",
        new Dictionary<string, string>
        {
            ["ShadowAssault"] = "attack",
            ["BigWolfStrike"] = "attack",
            ["BigWolfSlash"] = "attack",
            ["BigWolfS1"] = "attack",
            ["BigWolfS2"] = "howl",
            ["ShadowHowl"] = "howl",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs =>
        [Spine, BigWolfSpine, ShadowSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) => variantKey switch
        {
            BigWolfVariant => BigWolfSpine,
            ShadowVariant => ShadowSpine,
            _ => Spine,
        };

    [MonsterVisual(typeof(LanguageFloorCobaltScar))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 18f), new(0.62f, 0.62f), -235f, -400f, 235f, 12f, new(-20f, -86f), new(-70f, -300f))
    {
        TalkPos = new Vector2(-155f, -193f),
        StateDisplayLiftY = 12f,
    };

    private const string CobaltVariant = "cobalt";
    private const string BigWolfVariant = "big_wolf";
    private const string ShadowVariant = "shadow";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private LanguageFloorCobaltScarForm _form;
    private bool _shadow;
    private bool _ready;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        _ready = true;
        RefreshVariant();
    }

    public void SetForm(LanguageFloorCobaltScarForm form, bool shadow)
    {
        _form = form;
        _shadow = shadow;
        if (_ready)
        {
            RefreshVariant();
        }
    }

    private void RefreshVariant()
    {
        SetSpriteVisualVariant(
            _shadow
                ? ShadowVariant
                : _form == LanguageFloorCobaltScarForm.BigBadWolf
                    ? BigWolfVariant
                    : CobaltVariant);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        const string root =
            LanguageFloorAssets.LanguageFloorLiberationMonsterRoot;
        var profile = new SpriteVisualProfile();
        profile.Variant(CobaltVariant, root + "cobalt_scar.png")
            .AnchorX(247f);
        profile.Variant(
                BigWolfVariant,
                root + "cobalt_scar_big_wolf.png")
            .AnchorX(275f);
        profile.Variant(
                ShadowVariant,
                root + "lost_everything_wolf.png")
            .AnchorX(330f);
        profile.InitialVariant(CobaltVariant);

        profile.Frame(
            "cobalt_strike",
            root + "cobalt_scar_strike.png")
            .ForVariant(CobaltVariant);
        profile.Frame(
            "cobalt_slash",
            root + "cobalt_scar_slash.png")
            .ForVariant(CobaltVariant);
        profile.Frame(
            "cobalt_hit",
            root + "cobalt_scar_hit.png")
            .ForVariant(CobaltVariant);
        profile.Frame(
            "big_wolf_strike",
            root + "cobalt_scar_big_wolf_strike.png")
            .ForVariant(BigWolfVariant);
        profile.Frame(
            "big_wolf_slash",
            root + "cobalt_scar_big_wolf_slash.png")
            .ForVariant(BigWolfVariant);
        profile.Frame(
            "big_wolf_guard",
            root + "cobalt_scar_big_wolf_guard.png")
            .ForVariant(BigWolfVariant);
        profile.Frame(
            "big_wolf_hit",
            root + "cobalt_scar_big_wolf_hit.png")
            .ForVariant(BigWolfVariant);
        profile.Frame(
            "big_wolf_s1",
            root + "cobalt_scar_big_wolf_s1.png")
            .ForVariant(BigWolfVariant);
        profile.Frame(
            "big_wolf_s2",
            root + "cobalt_scar_big_wolf_s2.png")
            .ForVariant(BigWolfVariant);
        profile.Frame(
            "shadow_attack",
            root + "lost_everything_wolf_attack.png")
            .ForVariant(ShadowVariant);
        profile.Frame(
            "shadow_hit",
            root + "lost_everything_wolf_hit.png")
            .ForVariant(ShadowVariant);
        profile.Frame(
            "shadow_howl",
            root + "lost_everything_wolf_howl.png")
            .ForVariant(ShadowVariant);

        profile.Swap(
                "cobalt_strike",
                0.42f,
                "Attack",
                "CobaltStrike")
            .ForVariant(CobaltVariant);
        profile.Swap("cobalt_slash", 0.42f, "CobaltSlash")
            .ForVariant(CobaltVariant);
        profile.Swap("cobalt_hit", 0.26f, "Hit")
            .ForVariant(CobaltVariant);

        profile.Swap("big_wolf_strike", 0.42f, "BigWolfStrike")
            .ForVariant(BigWolfVariant);
        profile.Swap(
                "big_wolf_slash",
                0.42f,
                "Attack",
                "BigWolfSlash")
            .ForVariant(BigWolfVariant);
        profile.Swap("big_wolf_guard", 0.42f, "BigWolfGuard")
            .ForVariant(BigWolfVariant);
        profile.Swap("big_wolf_s1", 0.52f, "BigWolfS1")
            .ForVariant(BigWolfVariant);
        profile.Swap("big_wolf_s2", 0.52f, "BigWolfS2")
            .ForVariant(BigWolfVariant);
        profile.Swap("big_wolf_hit", 0.26f, "Hit")
            .ForVariant(BigWolfVariant);

        profile.Swap(
                "shadow_attack",
                0.42f,
                "Attack",
                "ShadowAssault",
                "BigWolfStrike",
                "BigWolfSlash")
            .ForVariant(ShadowVariant);
        profile.Swap("shadow_attack", 0.52f, "BigWolfS1")
            .ForVariant(ShadowVariant);
        profile.Swap(
                "shadow_howl",
                0.52f,
                "BigWolfS2",
                "ShadowHowl")
            .ForVariant(ShadowVariant);
        profile.Swap("shadow_hit", 0.26f, "Hit")
            .ForVariant(ShadowVariant);
        return profile;
    }
}

/// <summary>笑靥的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。</summary>
public sealed partial class LanguageFloorSmilingFaceCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "language_floor_liberation",
        "smiling_face",
        "thrust",
        new Dictionary<string, string>
        {
            ["AttackThrust"] = "thrust",
            ["AttackSlash"] = "slash",
            ["Scream"] = "scream",
            ["Vomit"] = "vomit",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(LanguageFloorSmilingFace))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 48f), new(0.48f, 0.48f), -245f, -390f, 245f, 12f, new(0f, -110f), new(0f, -320f))
    {
        TalkPos = new Vector2(35f, -300f),
        StateDisplayLiftY = 12f,
    };

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                SpriteVisualProfile.DefaultVariantKey,
                LanguageFloorSmilingFace.IdleTexturePath)
            .AnchorX(590f);

        profile.Frame(
                "thrust",
                LanguageFloorSmilingFace.AttackThrustTexturePath)
            .AnchorX(1150f);
        profile.Frame(
                "slash",
                LanguageFloorSmilingFace.AttackSlashTexturePath)
            .AnchorX(970f);
        profile.Frame("hit", LanguageFloorSmilingFace.HitTexturePath)
            .AnchorX(750f);
        profile.Frame(
                "scream",
                LanguageFloorSmilingFace.ScreamTexturePath)
            .AnchorX(600f);
        profile.Frame(
                "vomit",
                LanguageFloorSmilingFace.VomitTexturePath)
            .AnchorX(1450f);

        profile.Swap("thrust", 0.45f, "AttackThrust");
        profile.Swap("slash", 0.45f, "AttackSlash");
        profile.Swap("hit", 0.28f, "Hit");
        profile.Swap("scream", 0.75f, "Scream");
        profile.Swap("vomit", 0.75f, "Vomit");
        profile.Swap(
            $"@idle:{SpriteVisualProfile.DefaultVariantKey}",
            0.75f,
            "Phase");
        return profile;
    }
}

/// <summary>
/// 溶解的死尸（语言层）的外观：Spine 身体见 <see cref="LayeredBossSpine"/>（模组贴图做成整块，整图只平移转动，
/// 各姿势按标注点对齐），加载失败时退回下面的逐帧换图。
/// </summary>
public sealed partial class LanguageFloorMeltingCorpseCreatureVisuals
    : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec Spine = LayeredBossSpine.Create(
        "language_floor_liberation",
        "lang_melting_corpse",
        "attack",
        new Dictionary<string, string>
        {
            ["Moan"] = "attack",
        });

    internal override RuntimeSpineBody.Spec SpineSpec => Spine;

    [MonsterVisual(typeof(LanguageFloorMeltingCorpse))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, 5f), new(0.28f, 0.28f), -105f, -150f, 105f, 10f, new(0f, -60f), new(0f, -190f));

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    internal override SpriteVisualProfile SpriteProfile => Profile;

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
            SpriteVisualProfile.DefaultVariantKey,
            LanguageFloorMeltingCorpse.IdleTexturePath);
        profile.Frame(
            "attack",
            LanguageFloorMeltingCorpse.AttackTexturePath);
        profile.Swap("attack", 0.4f, "Moan");
        return profile;
    }
}

using System.Collections.Generic;
using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;

namespace LibraryOfRuina.content.events.WarpTrain;

/// <summary>
/// 汤吗丽的外观：Spine 身体见 <see cref="LayeredBossSpine"/>，加载失败时退回下面的逐帧换图。两个阶段各一副骨架，
/// 三种攻击按顺序轮换（换图版随机抽一张）。
/// </summary>
public partial class TomerryCreatureVisuals : SpineSpriteAttackCreatureVisuals
{
    internal static readonly RuntimeSpineBody.Spec PhaseOneSpine = LayeredBossSpine.Create(
        "warp_train",
        "tomerry_p1",
        "strike",
        new Dictionary<string, string>
        {
            // 一阶段没有三角铁姿势；招式只在二阶段出现，保险起见用打击
            ["TriangleSoundsBetter"] = "strike",
        }) with { AttackCycle = ["strike", "thrust", "slash"] };

    internal static readonly RuntimeSpineBody.Spec PhaseTwoSpine = LayeredBossSpine.Create(
        "warp_train",
        "tomerry_p2",
        "strike",
        new Dictionary<string, string>
        {
            ["TriangleSoundsBetter"] = "triangle",
        }) with { AttackCycle = ["strike", "thrust", "slash"] };

    internal override RuntimeSpineBody.Spec SpineSpec => PhaseOneSpine;

    internal override IEnumerable<RuntimeSpineBody.Spec> AllSpineSpecs => [PhaseOneSpine, PhaseTwoSpine];

    internal override RuntimeSpineBody.Spec SpineSpecFor(string? variantKey) =>
        variantKey == PhaseTwoVariant ? PhaseTwoSpine : PhaseOneSpine;

    [MonsterVisual(typeof(Tomerry))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -118f), new(0.46f, 0.46f), -140f, -280f, 140f, 8f, new(0f, -120f), new(0f, -315f))
    {
        TalkPos = new Vector2(-6f, -250f),
    };

    private const string PhaseOneVariant = "phase_one";
    private const string PhaseTwoVariant = "phase_two";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private bool _isPhaseTwo;
    private bool _ready;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        _isPhaseTwo = false;
        _ready = true;
        RefreshVariant();
    }

    public void SetPhaseTwo()
    {
        _isPhaseTwo = true;
        if (_ready)
        {
            RefreshVariant();
        }
    }

    private void RefreshVariant()
    {
        SetSpriteVisualVariant(
            _isPhaseTwo ? PhaseTwoVariant : PhaseOneVariant);
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(PhaseOneVariant, WarpTrainAssets.TomerryMonsterPrefix + ".webp");
        profile.Variant(PhaseTwoVariant, WarpTrainAssets.TomerryMonsterPrefix + "_phase2.webp");
        profile.InitialVariant(PhaseOneVariant);

        AddPhaseFrames(profile, PhaseOneVariant, WarpTrainAssets.TomerryMonsterPrefix + "_phase1");
        AddPhaseFrames(profile, PhaseTwoVariant, WarpTrainAssets.TomerryMonsterPrefix + "_phase2");
        profile.Frame(
                "triangle_sounds_better",
                WarpTrainAssets.TomerryMonsterPrefix + "_triangle_sounds_better.webp")
            .Nudge(24f, -118f)
            .Scale(0.5f);
        profile.Lunge(
            "triangle_sounds_better",
            0.16f,
            0.06f,
            0.2f,
            "TriangleSoundsBetter");
        return profile;
    }

    private static void AddPhaseFrames(
        SpriteVisualProfile profile,
        string variant,
        string texturePrefix)
    {
        string strike = variant + "_strike";
        string thrust = variant + "_thrust";
        string slash = variant + "_slash";
        string hit = variant + "_hit";
        foreach (string frame in new[] { strike, thrust, slash })
        {
            string suffix = frame[(variant.Length + 1)..];
            profile.Frame(
                    frame,
                    $"{texturePrefix}_attack_{suffix}.webp")
                .ForVariant(variant)
                .Nudge(24f, -118f)
                .Scale(0.5f);
        }

        profile.Frame(hit, texturePrefix + "_hit.webp")
            .ForVariant(variant);
        profile.Lunge(
                [strike, thrust, slash],
                0.16f,
                0.06f,
                0.2f,
                "Attack")
            .ForVariant(variant)
            .Random();
        profile.Swap(hit, 0.1f, "Hit")
            .ForVariant(variant);
    }
}

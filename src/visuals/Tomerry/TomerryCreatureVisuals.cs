using Godot;
using LibraryOfRuina.patches;

namespace LibraryOfRuina.visuals.Tomerry;

public partial class TomerryCreatureVisuals : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(monsters.Tomerry.Tomerry))]
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
        const string root = "res://images/monsters/tomerry";
        var profile = new SpriteVisualProfile().Centered();
        profile.Variant(PhaseOneVariant, root + ".webp");
        profile.Variant(PhaseTwoVariant, root + "_phase2.webp");
        profile.InitialVariant(PhaseOneVariant);

        AddPhaseFrames(profile, PhaseOneVariant, root + "_phase1");
        AddPhaseFrames(profile, PhaseTwoVariant, root + "_phase2");
        profile.Frame(
                "triangle_sounds_better",
                root + "_triangle_sounds_better.webp")
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

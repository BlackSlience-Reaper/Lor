using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.monsters.LanguageFloorLiberation;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace LibraryOfRuina.visuals.LanguageFloorLiberation;

public sealed partial class LanguageFloorMimicryCreatureVisuals
    : SpriteAttackCreatureVisuals
{
    [MonsterVisual(typeof(LanguageFloorMimicry))]
    internal static readonly CreatureVisualLayout Layout = new(
        new(0f, -80f), new(0.78f, 0.78f), -180f, -390f, 180f, 12f, new(0f, -130f), new(0f, -410f))
    {
        TalkPos = new Vector2(0f, -325f),
        StateDisplayLiftY = 18f,
    };

    private const string FirstVariant = "first";
    private const string SecondVariant = "second";
    private const string ThirdVariant = "third";

    internal static readonly SpriteVisualProfile Profile = BuildProfile();

    private LanguageFloorMimicryForm _form;
    private bool _ready;

    internal override SpriteVisualProfile SpriteProfile => Profile;

    public override void _Ready()
    {
        base._Ready();
        _ready = true;
        RefreshVariant();
    }

    public void SetForm(LanguageFloorMimicryForm form)
    {
        _form = form;
        if (_ready)
        {
            RefreshVariant();
        }
    }

    private void RefreshVariant()
    {
        SetSpriteVisualVariant(_form switch
        {
            LanguageFloorMimicryForm.First => FirstVariant,
            LanguageFloorMimicryForm.Second => SecondVariant,
            _ => ThirdVariant
        });
    }

    private static SpriteVisualProfile BuildProfile()
    {
        var profile = new SpriteVisualProfile();
        profile.Variant(
                FirstVariant,
                LanguageFloorMimicry.FormOneIdleTexturePath)
            .Scale(0.75f);
        profile.Variant(
                SecondVariant,
                LanguageFloorMimicry.FormTwoIdleTexturePath)
            .Scale(0.75f);
        profile.Variant(
                ThirdVariant,
                LanguageFloorMimicry.FormThreeIdleTexturePath)
            .Scale(0.60f)
            .At(0f, 120f);
        profile.InitialVariant(FirstVariant);

        profile.Frame(
                "first_thrust",
                LanguageFloorMimicry.FormOneThrustTexturePath)
            .ForVariant(FirstVariant)
            .Scale(0.75f)
            .Nudge(-6f, 53f);
        profile.Frame(
                "first_hit",
                LanguageFloorMimicry.FormOneHitTexturePath)
            .ForVariant(FirstVariant)
            .Scale(0.75f);
        profile.Frame(
                "third_strike",
                LanguageFloorMimicry.FormThreeStrikeTexturePath)
            .ForVariant(ThirdVariant)
            .Scale(0.60f)
            .Nudge(0f, -60f);
        profile.Frame(
                "third_thrust",
                LanguageFloorMimicry.FormThreeThrustTexturePath)
            .ForVariant(ThirdVariant)
            .Scale(0.60f);
        profile.Frame(
                "third_slash",
                LanguageFloorMimicry.FormThreeSlashTexturePath)
            .ForVariant(ThirdVariant)
            .Scale(0.60f);
        profile.Frame(
                "third_hit",
                LanguageFloorMimicry.FormThreeHitTexturePath)
            .ForVariant(ThirdVariant)
            .Scale(0.60f);
        profile.Frame(
                "third_parry",
                LanguageFloorMimicry.FormThreeParryTexturePath)
            .ForVariant(ThirdVariant)
            .Scale(0.60f);
        profile.Frame(
                "third_hello",
                LanguageFloorMimicry.FormThreeHelloTexturePath)
            .ForVariant(ThirdVariant)
            .Scale(0.60f);
        profile.Frame(
                "third_goodbye",
                LanguageFloorMimicry.FormThreeGoodbyeTexturePath)
            .ForVariant(ThirdVariant)
            .Scale(0.60f);

        profile.Swap("first_thrust", 0.55f, "AttackThrust")
            .ForVariant(FirstVariant);
        profile.Swap("first_hit", 0.32f, "Hit")
            .ForVariant(FirstVariant);
        profile.Swap("@idle:first", 0.55f, "Parry")
            .ForVariant(FirstVariant);
        profile.Swap("@idle:second", 0.32f, "Hit")
            .ForVariant(SecondVariant);
        profile.Swap("@idle:second", 0.55f, "Parry")
            .ForVariant(SecondVariant);
        profile.Swap("third_strike", 0.55f, "AttackStrike")
            .ForVariant(ThirdVariant);
        profile.Swap("third_thrust", 0.55f, "AttackThrust")
            .ForVariant(ThirdVariant);
        profile.Swap("third_slash", 0.55f, "AttackSlash")
            .ForVariant(ThirdVariant);
        profile.Swap("third_hit", 0.32f, "Hit")
            .ForVariant(ThirdVariant);
        profile.Swap("third_parry", 0.55f, "Parry")
            .ForVariant(ThirdVariant);
        profile.Swap("third_hello", 0.55f, "Hello")
            .ForVariant(ThirdVariant);
        profile.Swap("third_goodbye", 1.4f, "Goodbye")
            .ForVariant(ThirdVariant);
        return profile;
    }
}

internal static class LanguageFloorMimicrySpecialEffects
{
    private const int OverlayLayer = 235;

    public static async Task PlayTransformationAsync(
        Creature creature)
    {
        LocalOggOneShotPlayer.Play(
            LanguageFloorMimicry.ChangeSfxPath,
            -2f);
        if (TestMode.IsOn)
        {
            return;
        }

        Node? host = NRun.Instance?.GlobalUi ?? (Node?)NGame.Instance;
        if (host == null)
        {
            return;
        }

        var layer = new CanvasLayer
        {
            Name = "LanguageFloorMimicryTransformation",
            Layer = OverlayLayer
        };
        Color[] colors =
        [
            new Color(0.10f, 0.00f, 0.00f),
            new Color(0.35f, 0.00f, 0.00f, 0.88f),
            new Color(0.02f, 0.02f, 0.02f, 0.82f),
            new Color(0.70f, 0.00f, 0.00f, 0.48f)
        ];
        var filters = new List<ColorRect>(colors.Length);
        foreach (Color color in colors)
        {
            var filter = CreateFullRect(color);
            filter.Modulate = new Color(1f, 1f, 1f, 0f);
            layer.AddChild(filter);
            filters.Add(filter);
        }

        try
        {
            host.AddChildSafely(layer);
            foreach (ColorRect filter in filters)
            {
                if (!GodotObject.IsInstanceValid(filter) || !filter.IsInsideTree())
                {
                    return;
                }

                Tween reveal = filter.CreateTween();
                reveal.TweenProperty(filter, "modulate:a", 1f, 0.25);
                await Wait(layer, 0.25);
            }

            await Wait(layer, 0.5);
            foreach (ColorRect filter in filters)
            {
                if (!GodotObject.IsInstanceValid(filter) || !filter.IsInsideTree())
                {
                    return;
                }

                Tween fade = filter.CreateTween();
                fade.TweenProperty(filter, "modulate:a", 0f, 1.0);
            }

            await Wait(layer, 1.0);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(layer))
            {
                layer.QueueFree();
            }
        }
    }

    public static async Task PlayGoodbyeAsync(
        Creature attacker,
        IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(
            LanguageFloorMimicry.GoodbyeSfxPath,
            -2f);
        if (TestMode.IsOn)
        {
            return;
        }

        Node? host = NRun.Instance?.GlobalUi ?? (Node?)NGame.Instance;
        if (host == null)
        {
            return;
        }

        var layer = new CanvasLayer
        {
            Name = "LanguageFloorMimicryGoodbye",
            Layer = OverlayLayer + 1
        };
        ColorRect red = CreateFullRect(
            new Color(0.72f, 0f, 0f));
        layer.AddChild(red);

        Texture2D? attackerTexture =
            ResourceLoader.Load<Texture2D>(
                LanguageFloorMimicry.FormThreeIdleTexturePath);
        if (attackerTexture != null)
        {
            layer.AddChild(
                CreateSilhouette(
                    attackerTexture,
                    left: 0.52f,
                    right: 1f,
                    flip: attacker.Side == CombatSide.Player));
        }

        Texture2D? targetTexture = targets
            .Select(FindCreatureTexture)
            .FirstOrDefault(static texture => texture != null);
        if (targetTexture != null)
        {
            layer.AddChild(
                CreateSilhouette(
                    targetTexture,
                    left: 0f,
                    right: 0.48f,
                    flip: attacker.Side == CombatSide.Enemy));
        }

        bool cleanupScheduled = false;
        try
        {
            host.AddChildSafely(layer);
            await Wait(layer, 1.0);
            if (!GodotObject.IsInstanceValid(layer) || !layer.IsInsideTree())
            {
                return;
            }

            LocalOggOneShotPlayer.Play(LanguageFloorMimicry.GoodbyeAttackSfxPath, -2f);
            await CreatureCmd.TriggerAnim(attacker, "Goodbye", 0f);
            await Wait(layer, 1.0);
            if (!GodotObject.IsInstanceValid(layer) || !layer.IsInsideTree())
            {
                return;
            }

            LocalOggOneShotPlayer.Play(LanguageFloorMimicry.GoodbyeBloodSfxPath, -2f);
            AddDirectionalBloodStrike(layer, attacker);

            Tween fade = red.CreateTween();
            fade.TweenProperty(red, "modulate:a", 0f, 0.35);
            SceneTreeTimer cleanup = layer.GetTree().CreateTimer(0.36);
            cleanup.Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(layer))
                {
                    layer.QueueFree();
                }
            };
            cleanupScheduled = true;
        }
        finally
        {
            if (!cleanupScheduled && GodotObject.IsInstanceValid(layer))
            {
                layer.QueueFree();
            }
        }
    }

    private static ColorRect CreateFullRect(Color color) =>
        new()
        {
            Color = color,
            LayoutMode = 1,
            AnchorsPreset =
                (int)Control.LayoutPreset.FullRect,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

    private static TextureRect CreateSilhouette(
        Texture2D texture,
        float left,
        float right,
        bool flip)
    {
        var silhouette = new TextureRect
        {
            Texture = texture,
            LayoutMode = 1,
            AnchorLeft = left,
            AnchorRight = right,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode =
                TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode =
                TextureRect.StretchModeEnum.KeepAspectCentered,
            SelfModulate = Colors.Black
        };
        if (flip)
        {
            silhouette.Scale = new Vector2(-1f, 1f);
            silhouette.PivotOffset = new Vector2(
                texture.GetWidth() * 0.5f,
                0f);
        }

        return silhouette;
    }

    private static Texture2D? FindCreatureTexture(
        Creature creature)
    {
        Node? visuals =
            NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals;
        if (visuals == null)
        {
            return null;
        }

        foreach (Node node in visuals.FindChildren(
                     "*",
                     "Sprite2D",
                     recursive: true,
                     owned: false))
        {
            if (node is Sprite2D { Texture: { } texture })
            {
                return texture;
            }
        }

        foreach (Node node in visuals.FindChildren(
                     "*",
                     "TextureRect",
                     recursive: true,
                     owned: false))
        {
            if (node is TextureRect { Texture: { } texture })
            {
                return texture;
            }
        }

        return null;
    }

    private static void AddDirectionalBloodStrike(
        CanvasLayer layer,
        Creature attacker)
    {
        float direction =
            attacker.Side == CombatSide.Enemy ? -1f : 1f;
        var strike = new ColorRect
        {
            Color = new Color(0.95f, 0f, 0f, 0.92f),
            LayoutMode = 1,
            AnchorLeft = 0.08f,
            AnchorTop = 0.46f,
            AnchorRight = 0.92f,
            AnchorBottom = 0.55f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Rotation = direction * 0.12f,
            Scale = new Vector2(direction, 1f)
        };
        layer.AddChild(strike);
        Tween fade = strike.CreateTween();
        fade.TweenProperty(strike, "modulate:a", 0f, 0.4);
    }

    private static Task Wait(Node node, double seconds)
    {
        if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
        {
            return Task.CompletedTask;
        }

        SceneTreeTimer? timer =
            node.GetTree()?.CreateTimer(seconds);
        if (timer == null)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource();
        timer.Timeout += () => completion.TrySetResult();
        return completion.Task;
    }
}

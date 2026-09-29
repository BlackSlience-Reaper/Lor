using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.liberation.Natural;

internal static class NaturalFloorNihilTransition
{
    private const string ScenePath = "res://scenes/vfx/natural_floor_nihil_transition.tscn";
    private const string SoundPath = "res://audio/sfx/natural_floor_nihil/Nihil_Filter.ogg";
    internal static readonly string[] AssetPaths = [ScenePath, SoundPath];
    private static Control? _root;
    private static TaskCompletionSource? _completion;

    internal static async Task PlayAsync(NaturalFloorNihilForm form)
    {
        Cleanup();
        Control? host = NRun.Instance?.GlobalUi?.AboveTopBarVfxContainer ?? NCombatRoom.Instance?.CombatVfxContainer;
        if (host?.GetTree() == null)
        {
            return;
        }

        Control root = ResourceLoader.Load<PackedScene>(ScenePath).Instantiate<Control>();
        TextureRect icon = root.GetNode<TextureRect>("Center/PhaseIcon");
        icon.Visible = form != NaturalFloorNihilForm.Nihil;
        if (icon.Visible)
        {
            icon.Texture = ResourceLoader.Load<Texture2D>("res://images/vfx/natural_floor_nihil/" + form.ToString().ToLowerInvariant() + ".png");
        }

        host.AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root = root;
        var completion = new TaskCompletionSource();
        _completion = completion;
        root.TreeExiting += () => completion.TrySetResult();
        AnimationPlayer animation = root.GetNode<AnimationPlayer>("AnimationPlayer");
        animation.AnimationFinished += _ => completion.TrySetResult();
        LocalOggOneShotPlayer.Play(SoundPath);
        animation.Play("ChangeFilter");
        try
        {
            await completion.Task;
        }
        finally
        {
            if (ReferenceEquals(_root, root))
            {
                Cleanup();
            }
        }
    }

    internal static void Cleanup()
    {
        _completion?.TrySetResult();
        _completion = null;
        if (GodotObject.IsInstanceValid(_root))
        {
            _root!.QueueFree();
        }

        _root = null;
    }
}

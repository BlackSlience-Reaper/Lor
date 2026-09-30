using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.framework.assets;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.reverberation.CryingChildren;

internal static class CryingChildrenAssets
{
    internal const string PhilipScene = "res://scenes/creature_visuals/reverberation_philip.tscn";
    internal const string ChildScene = "res://scenes/creature_visuals/unspeaking_child.tscn";
    internal const string AudioRoot = "res://audio/sfx/reverberation/crying_children/";

    internal static readonly string[] Paths =
    [
        PhilipScene, ChildScene,
        AudioRoot + "philip_slash.ogg", AudioRoot + "philip_pierce.ogg",
        AudioRoot + "philip_strike.ogg", AudioRoot + "philip_ranged.ogg",
        AudioRoot + "child_slash.ogg", AudioRoot + "child_pierce.ogg",
        SharedAssets.LibraryPassiveOrangeIcon,
        SharedAssets.LibraryPassivePurpleIcon
    ];

    internal static string AttackSound(CryingChildMonsterBase monster, string animation)
    {
        if (monster is UnspeakingChild)
        {
            return AudioRoot + (animation == "Pierce" ? "child_pierce.ogg" : "child_slash.ogg");
        }
        return animation switch
        {
            "Pierce" => AudioRoot + "philip_pierce.ogg",
            "Strike" => AudioRoot + "philip_strike.ogg",
            "Ranged" => AudioRoot + "philip_ranged.ogg",
            _ => AudioRoot + "philip_slash.ogg"
        };
    }

    internal static void Speak(ReverberationPhilip monster, string moment) =>
        DawnOfficeDialogueHelper.Speak(monster, "REVERBERATION_PHILIP.banter." + moment);
}

[MonsterVisual(typeof(ReverberationPhilip), ScenePath = CryingChildrenAssets.PhilipScene)]
internal sealed partial class ReverberationPhilipVisuals : SceneAnimatedCreatureVisuals
{
    protected override string ResolveCurrentAnimationLibrary() =>
        (GetParent() as NCreature)?.Entity?.Monster is ReverberationPhilip { Phase: 3 }
            ? "burning" : "normal";
}

[MonsterVisual(typeof(UnspeakingChild), ScenePath = CryingChildrenAssets.ChildScene)]
internal sealed partial class UnspeakingChildVisuals : SceneAnimatedCreatureVisuals
{
    protected override string ResolveCurrentAnimationLibrary() => "normal";
}

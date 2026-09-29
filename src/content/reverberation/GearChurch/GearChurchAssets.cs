using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace LibraryOfRuina.content.reverberation.GearChurch;

internal static class GearChurchAssets
{
    internal const string EileenScene = "res://scenes/creature_visuals/reverberation_eileen.tscn";
    internal const string FollowerScene = "res://scenes/creature_visuals/gear_church_follower.tscn";
    internal const string AudioRoot = "res://audio/sfx/reverberation/gear_church/";

    internal static readonly string[] Paths =
    [
        EileenScene, FollowerScene,
        AudioRoot + "slash.ogg", AudioRoot + "pierce.ogg",
        AudioRoot + "strike.ogg", AudioRoot + "steam.ogg",
        AudioRoot + "eileen_slash.ogg", AudioRoot + "eileen_pierce.ogg",
        AudioRoot + "eileen_strike.ogg", AudioRoot + "eileen_ranged.ogg",
        "res://images/powers/gear_church_smoke_power.png",
        "res://images/powers/library_passive_orange.png",
        "res://images/powers/library_passive_purple.png"
    ];

    internal static string AttackSound(GearChurchMonsterBase monster, string animation)
    {
        if (monster is ReverberationEileen)
        {
            return AudioRoot + (animation switch
            {
                "Pierce" => "eileen_pierce.ogg",
                "Strike" => "eileen_strike.ogg",
                "SpecialTwo" or "Ranged" => "eileen_ranged.ogg",
                _ => "eileen_slash.ogg"
            });
        }
        return AudioRoot + (animation switch
        {
            "Pierce" => "pierce.ogg",
            "Strike" => "strike.ogg",
            "SpecialTwo" or "Ranged" => "steam.ogg",
            _ => "slash.ogg"
        });
    }

    internal static void Speak(ReverberationEileen monster, string moment)
    {
        string key = monster.Id.Entry + ".banter." + moment;
        if (moment == "death")
        {
            // 死亡回调中本体 TalkCmd 拒绝已死亡的说话者，直接使用仍存在的战斗节点挂载对白。
            const double deathDialogueSeconds = 3d; // 死亡对白：气泡显示时长。
            string text = MonsterModel.L10NMonsterLookup(key).GetFormattedText();
            NSpeechBubbleVfx? bubble = NSpeechBubbleVfx.Create(text, monster.Creature, deathDialogueSeconds);
            if (bubble != null)
            {
                monster.Creature.GetVfxContainer()?.AddChildSafely(bubble);
            }
            return;
        }
        DawnOfficeDialogueHelper.Speak(monster, key);
    }
}

[MonsterVisual(typeof(ReverberationEileen), ScenePath = GearChurchAssets.EileenScene)]
internal sealed partial class ReverberationEileenVisuals : SceneAnimatedCreatureVisuals
{
    protected override string ResolveCurrentAnimationLibrary() => "normal";
}

[MonsterVisual(typeof(GearChurchFollower), ScenePath = GearChurchAssets.FollowerScene)]
internal sealed partial class GearChurchFollowerVisuals : SceneAnimatedCreatureVisuals
{
    protected override string ResolveCurrentAnimationLibrary() => "normal";
}

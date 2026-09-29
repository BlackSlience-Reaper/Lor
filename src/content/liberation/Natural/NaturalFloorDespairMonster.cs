using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

public abstract class NaturalFloorDespairMonster : NaturalFloorPhaseMonster
{
    internal const string SfxRoot = "res://audio/sfx/natural_floor_liberation/despair/";
    internal const float AttackTime = 0.96f; // 自然层绝望阶段：攻击结算等待秒数。

    protected virtual string[] SoundFiles =>
    [
        "normal_slash", "normal_blunt", "normal_pierce",
        "tear_slash", "tear_blunt", "tear_pierce",
        "hit", "guard", "grant", "despair", "stab", "broken", "crying"
    ];

    public override IEnumerable<string> AssetPaths => base.AssetPaths
        .Concat(SoundFiles.Select(file => SfxRoot + file + ".ogg"))
        .Concat(["res://images/powers/library_passive_green.png", "res://images/powers/forgotten_knight_sword_teardrop_power.png"])
        .Distinct();

    public override Task AfterDamageReceivedLate(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Creature && result.TotalDamage > 0)
        {
            Sound(result.WasFullyBlocked ? "guard" : "hit");
            if (Creature.IsAlive)
            {
                return CreatureCmd.TriggerAnim(Creature, result.WasFullyBlocked ? "Guard" : "Hit", 0f);
            }
        }
        return Task.CompletedTask;
    }

    internal static void Sound(string file) => LocalOggOneShotPlayer.Play(SfxRoot + file + ".ogg", -2f);

}

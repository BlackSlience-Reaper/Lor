using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.ScorchedGirl;

public sealed class ScorchedGirlMonster : CounterIntentMonsterModel
{
    private static readonly string[] BattleStartLines =
    {
        "SCORCHED_GIRL_MONSTER.dialogue.battleStart.0",
        "SCORCHED_GIRL_MONSTER.dialogue.battleStart.1",
        "SCORCHED_GIRL_MONSTER.dialogue.battleStart.2",
        "SCORCHED_GIRL_MONSTER.dialogue.battleStart.3",
        "SCORCHED_GIRL_MONSTER.dialogue.battleStart.4"
    };

    private static readonly string[] LostHopeLines =
    {
        "SCORCHED_GIRL_MONSTER.dialogue.lostHope.0",
        "SCORCHED_GIRL_MONSTER.dialogue.lostHope.1"
    };

    private static readonly string[] AttackLines =
    {
        "SCORCHED_GIRL_MONSTER.dialogue.attack.0",
        "SCORCHED_GIRL_MONSTER.dialogue.attack.1",
        "SCORCHED_GIRL_MONSTER.dialogue.attack.2"
    };

    private static readonly string[] BackgroundTextLineKeys =
    {
        "SCORCHED_GIRL_MONSTER.backgroundText.0",
        "SCORCHED_GIRL_MONSTER.backgroundText.1",
        "SCORCHED_GIRL_MONSTER.backgroundText.2",
        "SCORCHED_GIRL_MONSTER.backgroundText.3",
        "SCORCHED_GIRL_MONSTER.backgroundText.4"
    };

    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private int FourthMatchFlameDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 15, 14);

    private const int FourthMatchFlameSelfDamage = 15;
    private const float AttackAnimDelaySeconds = AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds;

    private const string GirlAttackSfxPath = "res://audio/sfx/scorched_girl/scorched_girl_explosion.ogg";
    private static readonly string MatchMarkRelicTitleLocKey = $"{ModelDb.GetId<MatchMarkRelic>().Entry}.title";

    private bool _matchFlamesAllExtinguished;
    private bool _nextExtinguishedTurnIsAttack;
    private bool _hasPlayedBattleStartLine;
    private bool _hasPlayedLostHopeLine;
    private bool _backgroundMoonTextLoopStarted;
    private int _turnsTaken;

    private MoveState? FourthMatchFlameState { get; set; }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 500, 400);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 500, 400);

    public override IEnumerable<string> AssetPaths =>
        ScorchedGirlMonsterCreatureVisuals.Profile.AssetPaths
            .Append(GirlAttackSfxPath)
            .Concat(base.AssetPaths.Skip(1))
            .Distinct();

    public override int DefaultChaoResistance => 100;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Vulnerable,
        Pierce = LibraryResistanceLevel.Vulnerable,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        _matchFlamesAllExtinguished = false;
        _nextExtinguishedTurnIsAttack = false;
        _hasPlayedBattleStartLine = false;
        _hasPlayedLostHopeLine = false;
        _backgroundMoonTextLoopStarted = false;
        _turnsTaken = 0;

        //await PowerCmdCompat.Apply<LibraryOfRuinaScorchedGirlAshesPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<LibraryOfRuinaScorchedGirlExtinguishedSparkPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override Task BeforeCombatStart()
    {
        if (Creature.IsDead)
        {
            return Task.CompletedTask;
        }

        if (!_backgroundMoonTextLoopStarted)
        {
            _backgroundMoonTextLoopStarted = true;
            StartBackgroundMoonTextLoop();
        }

        if (!_hasPlayedBattleStartLine)
        {
            _hasPlayedBattleStartLine = true;
            ScorchedGirlDialogueHelper.SpeakRandom(this, BattleStartLines);
        }

        return Task.CompletedTask;
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented)
        {
            return;
        }

        if (creature == Creature)
        {
            AddMatchMarkRewardsFromDeathHook(creature);
            return;
        }

        if (Creature.IsDead)
        {
            return;
        }

        if (creature.Side != Creature.Side || creature.Monster is not TheFourthMatchFlame)
        {
            return;
        }

        if (HasLivingMatchFlame())
        {
            return;
        }

        await EnterExtinguishedPhase();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var fourthMatchFlame = new MoveState(
            "FOURTH_MATCH_FLAME",
            FourthMatchFlameMove,
            new SingleAttackIntent(FourthMatchFlameDamage),
            new DebuffIntent(true));

        var smolderingStillness = new MoveState(
            "SMOLDERING_STILLNESS",
            SmolderingStillnessMove,
            new StunIntent());

        FourthMatchFlameState = fourthMatchFlame;

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(fourthMatchFlame, IsFourthMatchFlameTurn);
        chooser.AddState(smolderingStillness, () => true);

        fourthMatchFlame.FollowUpState = chooser;
        smolderingStillness.FollowUpState = chooser;

        states.Add(fourthMatchFlame);
        states.Add(smolderingStillness);
        states.Add(chooser);

        return new MonsterMoveStateMachine(states, chooser);
    }

    private bool IsFourthMatchFlameTurn()
    {
        if (_matchFlamesAllExtinguished)
        {
            return _nextExtinguishedTurnIsAttack;
        }

        return (_turnsTaken + 1) % 3 == 0;
    }

    private async Task FourthMatchFlameMove(IReadOnlyList<Creature> targets)
    {
        if (_matchFlamesAllExtinguished)
        {
            ScorchedGirlDialogueHelper.SpeakRandom(this, AttackLines);
        }

        LocalOggOneShotPlayer.Play(GirlAttackSfxPath, -2f);

        await AbnormalityAnimHelper.ExecuteAttackSegment(this, FourthMatchFlameDamage, delaySeconds: AttackAnimDelaySeconds);
        await PowerCmdCompat.Apply<LibraryBurnPower>(targets, 9, Creature, null);

        
        await ApplySelfHpLoss(FourthMatchFlameSelfDamage);

        AdvanceTurnCadence();
    }

    private Task SmolderingStillnessMove(IReadOnlyList<Creature> targets)
    {
        AdvanceTurnCadence();
        return Task.CompletedTask;
    }

    private void AdvanceTurnCadence()
    {
        _turnsTaken++;
        if (_matchFlamesAllExtinguished)
        {
            _nextExtinguishedTurnIsAttack = !_nextExtinguishedTurnIsAttack;
        }
    }

    private async Task EnterExtinguishedPhase()
    {
        if (_matchFlamesAllExtinguished)
        {
            return;
        }

        _matchFlamesAllExtinguished = true;
        _nextExtinguishedTurnIsAttack = true;

        if (!_hasPlayedLostHopeLine)
        {
            _hasPlayedLostHopeLine = true;
            ScorchedGirlDialogueHelper.SpeakRandom(this, LostHopeLines);
        }

        if (FourthMatchFlameState != null)
        {
            SetMoveImmediate(FourthMatchFlameState, forceTransition: true);
        }

        var creatureNode = CombatQueries.CreatureNodeOf(this);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

    private bool HasLivingMatchFlame()
    {
        return CombatState?.Enemies.Any(enemy => !enemy.IsDead && enemy.Monster is TheFourthMatchFlame) == true;
    }

    private static bool IsScorchedGirlEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is ScorchedGirlMonster);
    }

    private void AddMatchMarkRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room || !IsScorchedGirlEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<MatchMarkRelic>(room, MatchMarkRelicTitleLocKey);
    }

    private static bool HasMatchMarkReward(CombatRoom room, Player player)
    {
        if (!room.ExtraRewards.TryGetValue(player, out List<Reward>? rewards) || rewards == null)
        {
            return false;
        }

        return rewards
            .OfType<RelicReward>()
            .Any(reward =>
                reward.IsPopulated
                && reward.Description.LocTable == "relics"
                && reward.Description.LocEntryKey == MatchMarkRelicTitleLocKey);
    }

    private static void StartBackgroundMoonTextLoop()
    {
        MonsterMoonTextLoop.Start(BackgroundTextLineKeys, BackgroundTextIntervalSeconds, BackgroundTextSpawnArea);
    }

    private async Task ApplySelfHpLoss(int amount)
    {
        CombatStateLike? combatState = Creature.CombatState;
        if (amount <= 0 || combatState == null || Creature.IsDead)
        {
            return;
        }

        decimal targetHp = Creature.CurrentHp - amount;
        if (targetHp < 0m)
        {
            targetHp = 0m;
        }

        await CreatureCmd.SetCurrentHp(Creature, targetHp);
    }
}

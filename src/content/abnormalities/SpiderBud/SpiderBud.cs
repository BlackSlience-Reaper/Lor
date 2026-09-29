using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.powers;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.SpiderBud;

public sealed class SpiderBud : CounterIntentMonsterModel
{
    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "SPIDER_BUD.backgroundText.normal.0",
        "SPIDER_BUD.backgroundText.normal.1",
        "SPIDER_BUD.backgroundText.normal.2",
        "SPIDER_BUD.backgroundText.normal.3",
        "SPIDER_BUD.backgroundText.normal.4",
    ];

    private static readonly string[] HuntBackgroundTextLineKeys =
    [
        "SPIDER_BUD.backgroundText.hunt.0",
        "SPIDER_BUD.backgroundText.hunt.1",
        "SPIDER_BUD.backgroundText.hunt.2",
        "SPIDER_BUD.backgroundText.hunt.3",
        "SPIDER_BUD.backgroundText.hunt.4",
    ];

    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);
    private static readonly Random BackgroundTextRng = new();

    private const string HuntMoveId = "HUNT";
    private const string GuardBabiesMoveId = "GUARD_BABIES";
    
    private const string UnknownMoveId1 = "UNKNOWN_1";
    private const string UnknownMoveId2 = "UNKNOWN_2";

    private const string Root = "res://images/monsters/spider_bud/";
    public const string IdleTexturePath = Root + "spider_bud_idle.png";
    public const string AttackTexturePath = Root + "spider_bud_attack.png";
    public const string GuardTexturePath = Root + "spider_bud_guard.png";

    public const string SfxRoot = "res://audio/sfx/spider_bud/";
    public const string AttackSfxPath = SfxRoot + "spider_bud_attack.ogg";
    public const string GuardSfxPath = SfxRoot + "spider_bud_guard.ogg";
    public const string PassiveSfxPath = SfxRoot + "spider_bud_passive.ogg";

    private static readonly string[] SfxPaths = [AttackSfxPath, GuardSfxPath, PassiveSfxPath];

    private MoveState _huntState = null!;
    private MoveState _guardBabiesState = null!;
    private MoveState _unknownState1 = null!;
    private MoveState _unknownState2 = null!;

    private bool _smallSpiderKilledTextActive;
    private bool _huntTextActive;

    private static readonly string SpiderBudPageRelicTitleLocKey =
        $"{ModelDb.GetId<SpiderBudPageRelic>().Entry}.title";

    public bool HuntPending { get; private set; }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 69, 47);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 73, 50);

    public override int DefaultChaoResistance => 50;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    private int HuntDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 13, 10);

    private const int GuardBabiesBlock = 11;
    private const int GuardBabiesStrength = 2;

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                SpiderBudCreatureVisuals.Profile.AssetPaths.Count
                + SfxPaths.Length
                + 4);
            paths.AddRange(SpiderBudCreatureVisuals.Profile.AssetPaths);
            paths.AddRange(SfxPaths);
            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _smallSpiderKilledTextActive = false;
        _huntTextActive = false;
        EncounterBgmController.RegisterMonster(Creature);
        
        await PowerCmdCompat.Apply<SpiderBudStartHuntingPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Ensure<UntargetablePower>(Creature);
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead)
        {
            StartBackgroundMoonTextLoopForCurrentTurn();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Player && _smallSpiderKilledTextActive)
        {
            _smallSpiderKilledTextActive = false;
            StartBackgroundMoonTextLoopForCurrentTurn();
        }

        return base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        // 原版此时已生成本回合意图，玩家接下来仍有完整的应对回合。
        if (side == CombatSide.Player && HuntPending)
        {
            StartHuntFromSmallSpiderDeath();
        }
    }

    public void QueueHuntFromSmallSpiderDeath()
    {
        if (Creature.IsDead || MoveStateMachine == null)
        {
            return;
        }

        HuntPending = true;
        if (SpiderBudHuntTiming.HasPlayerResponseWindow(Creature))
        {
            StartHuntFromSmallSpiderDeath();
        }
    }

    private void StartHuntFromSmallSpiderDeath()
    {
        if (Creature.IsDead || MoveStateMachine == null)
        {
            return;
        }

        HuntPending = false;
        QueueSmallSpiderKilledMoonText();
        LocalOggOneShotPlayer.Play(PassiveSfxPath, -1.5f);

        if (Creature.HasPower<UntargetablePower>() && AreAllSmallSpidersDead())
        {
            _huntTextActive = true;
            Creature.GetPower<UntargetablePower>()?.RemoveInternal();

            _huntState.FollowUpState = _huntState;
        }

        SetMoveImmediate(_huntState, forceTransition: true);
    }

    public void QueueSmallSpiderKilledMoonText()
    {
        if (Creature.IsDead
            || !CombatManager.Instance.IsInProgress
            || Creature.CombatState?.CurrentSide != CombatSide.Player)
        {
            return;
        }

        _smallSpiderKilledTextActive = true;
        StartBackgroundMoonTextLoop(HuntBackgroundTextLineKeys);
        ShowImmediateBackgroundMoonText(HuntBackgroundTextLineKeys);
    }

    private bool AreAllSmallSpidersDead()
    {
        return Creature.CombatState?.Enemies
            .Where(e => e.IsAlive)
            .Select(e => e.Monster)
            .OfType<SpiderBudSmallSpider>()
            .Any() != true;
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return Task.CompletedTask;
        }

        AddSpiderBudPageRewards();
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _huntState = new MoveState(
            HuntMoveId,
            HuntMove,
            new SingleAttackIntent(HuntDamage));

        _guardBabiesState = new MoveState(
            GuardBabiesMoveId,
            GuardBabiesMove,
            new DefendIntent(),
            new DetailedBuffIntent<StrengthPower>(
                GuardBabiesStrength,
                DetailedBuffTargetScope.OtherEnemies,
                descriptionKey: "SPIDER_BUD.guard_babies.strength.description"));

        _unknownState1 = new MoveState(
            UnknownMoveId1,
            UnknownMove,
            new UnknownIntent());

        _unknownState2 = new MoveState(
            UnknownMoveId2,
            UnknownMove,
            new UnknownIntent());

        _unknownState1.FollowUpState = _unknownState2;
        _unknownState2.FollowUpState = _guardBabiesState;
        _guardBabiesState.FollowUpState = _unknownState1;
        _huntState.FollowUpState = _unknownState1;

        List<MonsterState> states = [_unknownState1, _unknownState2, _guardBabiesState, _huntState];
        return new MonsterMoveStateMachine(states, _unknownState1);
    }

    private async Task HuntMove(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentTurn();
        LocalOggOneShotPlayer.Play(AttackSfxPath, -1.5f);
        await DamageCmd.Attack(HuntDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 0.9f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task UnknownMove(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentTurn();
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.9f);
    }

    private async Task GuardBabiesMove(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentTurn();
        LocalOggOneShotPlayer.Play(GuardSfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.9f);
        await CreatureCmd.GainBlock(Creature, GuardBabiesBlock, ValueProp.Move, null);

        IReadOnlyList<Creature>? otherEnemies = Creature.CombatState?.Enemies
            ?.Where(e => e.IsAlive && e != Creature)
            ?.ToArray();

        if (otherEnemies != null && otherEnemies.Count > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(otherEnemies, GuardBabiesStrength, Creature, null);
        }
    }

    private void StartBackgroundMoonTextLoopForCurrentTurn()
    {
        IReadOnlyList<string> lineKeys = _smallSpiderKilledTextActive || _huntTextActive
            ? HuntBackgroundTextLineKeys
            : NormalBackgroundTextLineKeys;
        StartBackgroundMoonTextLoop(lineKeys);
    }

    private static void StartBackgroundMoonTextLoop(IReadOnlyList<string> lineKeys)
    {
        MoonTextService.StartRandomLoop(
            lineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    private static void ShowImmediateBackgroundMoonText(IReadOnlyList<string> lineKeys)
    {
        if (lineKeys.Count == 0)
        {
            return;
        }

        string key;
        lock (BackgroundTextRng)
        {
            key = lineKeys[BackgroundTextRng.Next(lineKeys.Count)];
        }

        MoonTextService.StartSequence(
            new[] { new MoonTextSequenceEntry(L10NMonsterLookup(key), 0f) });
    }

    private void AddSpiderBudPageRewards()
    {
        if (Creature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is SpiderBud))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<SpiderBudPageRelic>(room, SpiderBudPageRelicTitleLocKey);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new SingleAttackIntent(HuntDamage);
        yield return new DefendIntent();
        yield return new DetailedBuffIntent<StrengthPower>(
            GuardBabiesStrength,
            DetailedBuffTargetScope.OtherEnemies,
            descriptionKey: "SPIDER_BUD.guard_babies.strength.description");
        yield return new UnknownIntent();
    }
}

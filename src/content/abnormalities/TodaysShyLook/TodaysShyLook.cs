using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.infra.helpers;
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
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.TodaysShyLook;

public sealed class TodaysShyLook : CounterIntentMonsterModel
{
    private const string TodaysExpression1MoveId = "TODAYS_EXPRESSION_1";
    private const string TodaysExpression2MoveId = "TODAYS_EXPRESSION_2";
    private const string TodaysExpression3MoveId = "TODAYS_EXPRESSION_3";
    private const string Shyness1MoveId = "SHYNESS_1";
    private const string Shyness2MoveId = "SHYNESS_2";

    private const int IntentSequenceLength = 256;
    private const int ExpressionPowerAmount = 1;
    private const int TodaysExpression3Hits = 2;
    private const int VulnerableAmount = 1;
    private const int WeakAmount = 1;
    private const int Shyness1Block = 4;
    private const int Shyness1NextTurnStrength = 2;
    private const int Shyness2Block = 8;
    private const float BackgroundTextIntervalSeconds = 5f;

    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private static readonly string[][] BackgroundTextLineKeys =
    [
        [
            "TODAYS_SHY_LOOK.backgroundText.expression1.0",
            "TODAYS_SHY_LOOK.backgroundText.expression1.1"
        ],
        [
            "TODAYS_SHY_LOOK.backgroundText.expression2.0",
            "TODAYS_SHY_LOOK.backgroundText.expression2.1"
        ],
        [
            "TODAYS_SHY_LOOK.backgroundText.expression3.0",
            "TODAYS_SHY_LOOK.backgroundText.expression3.1"
        ],
        [
            "TODAYS_SHY_LOOK.backgroundText.expression4.0",
            "TODAYS_SHY_LOOK.backgroundText.expression4.1"
        ],
        [
            "TODAYS_SHY_LOOK.backgroundText.expression5.0",
            "TODAYS_SHY_LOOK.backgroundText.expression5.1"
        ]
    ];

    private static readonly string TodaysShyLookPageRelicTitleLocKey =
        $"{ModelDb.GetId<TodaysShyLookPageRelic>().Entry}.title";

    private List<int> _intentSequence = [];
    private int _intentSequenceCursor;
    private int _currentExpression = 5;
    private Dictionary<int, MoveState> _moveStatesByExpression = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _intentSequence = [.. _intentSequence];
        _moveStatesByExpression = [];
    }

    private int TodaysExpression1Damage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 19, 17);

    private int TodaysExpression2Damage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 14, 13);

    private int TodaysExpression3Damage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    public int CurrentExpression => _currentExpression;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 120, 98);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 123, 101);

    public override int DefaultChaoResistance => 80;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                TodaysShyLookCreatureVisuals.Profile.AssetPaths.Count + 12);
            paths.AddRange(
                TodaysShyLookCreatureVisuals.Profile.AssetPaths);

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

        GenerateFixedIntentSequence();
        SwitchToNextExpression(refreshMoonText: false);
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<LibraryOfRuinaTodaysShyLookExpressionPower>(
            Creature,
            ExpressionPowerAmount,
            Creature,
            null,
            silent: true);
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead)
        {
            StartBackgroundMoonTextLoopForCurrentExpression();
        }

        return Task.CompletedTask;
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

        AddTodaysShyLookPageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    public async Task SwitchExpressionFromPower()
    {
        if (Creature.IsDead || Creature.IsStunned)
        {
            return;
        }

        SwitchToNextExpression(refreshMoonText: true);

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState expression1 = CreateMoveState(
            TodaysExpression1MoveId,
            TodaysExpression1Move,
            new SingleAttackIntent(TodaysExpression1Damage));

        MoveState expression2 = CreateMoveState(
            TodaysExpression2MoveId,
            TodaysExpression2Move,
            new SingleAttackIntent(TodaysExpression2Damage),
            new DebuffIntent());

        MoveState expression3 = CreateMoveState(
            TodaysExpression3MoveId,
            TodaysExpression3Move,
            new MultiAttackIntent(TodaysExpression3Damage, TodaysExpression3Hits),
            new DebuffIntent());

        MoveState shyness1 = CreateMoveState(
            Shyness1MoveId,
            Shyness1Move,
            new DefendIntent(),
            new BuffIntent());

        MoveState shyness2 = CreateMoveState(
            Shyness2MoveId,
            Shyness2Move,
            new DefendIntent());

        _moveStatesByExpression = new Dictionary<int, MoveState>
        {
            [1] = expression1,
            [2] = expression2,
            [3] = expression3,
            [4] = shyness1,
            [5] = shyness2
        };

        foreach (MoveState state in _moveStatesByExpression.Values)
        {
            state.FollowUpState = state;
        }

        return new MonsterMoveStateMachine(_moveStatesByExpression.Values, shyness2);
    }

    private static MoveState CreateMoveState(
        string id,
        Func<IReadOnlyList<Creature>, Task> onPerform,
        params AbstractIntent[] intents)
    {
        return new MoveState(
            id,
            onPerform,
            intents);
    }

    private async Task TodaysExpression1Move(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentExpression();

        await AbnormalityAnimHelper.ExecuteAttackSegment(this, TodaysExpression1Damage);

        AdvanceAfterPerformingMove();
    }

    private async Task TodaysExpression2Move(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentExpression();

        await AbnormalityAnimHelper.ExecuteAttackSegment(this, TodaysExpression2Damage);
        await PowerCmdCompat.Apply<VulnerablePower>(targets, VulnerableAmount, Creature, null);

        AdvanceAfterPerformingMove();
    }

    private async Task TodaysExpression3Move(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentExpression();

        for (int i = 0; i < TodaysExpression3Hits; i++)
        {
            await AbnormalityAnimHelper.ExecuteAttackSegment(this, TodaysExpression3Damage);
        }
        await PowerCmdCompat.Apply<WeakPower>(targets, WeakAmount, Creature, null);

        AdvanceAfterPerformingMove();
    }

    private async Task Shyness1Move(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentExpression();

        await AbnormalityAnimHelper.TriggerCast(Creature);
        await CreatureCmd.GainBlock(Creature, Shyness1Block, ValueProp.Move, null);
        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(
            Creature,
            Shyness1NextTurnStrength,
            Creature,
            null);

        AdvanceAfterPerformingMove();
    }

    private async Task Shyness2Move(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentExpression();

        await AbnormalityAnimHelper.TriggerCast(Creature);
        await CreatureCmd.GainBlock(Creature, Shyness2Block, ValueProp.Move, null);

        AdvanceAfterPerformingMove();
    }

    private void GenerateFixedIntentSequence()
    {
        _intentSequence.Clear();
        _intentSequenceCursor = 0;

        Rng? monsterAi = TryResolveMonsterAiRng();
        int previousExpression = 0;
        for (int i = 0; i < IntentSequenceLength; i++)
        {
            int expression = monsterAi?.NextInt(1, 6) ?? i % 5 + 1;
            if (expression == previousExpression)
            {
                expression = expression % 5 + 1;
            }

            _intentSequence.Add(expression);
            previousExpression = expression;
        }

        if (_intentSequence.Count > 1 && _intentSequence[^1] == _intentSequence[0])
        {
            _intentSequence[^1] = _intentSequence[^1] % 5 + 1;
        }
    }

    private Rng? TryResolveMonsterAiRng()
    {
        try
        {
            Rng? combatRng = Creature.CombatState?.RunState.Rng.MonsterAi;
            if (combatRng != null)
            {
                return combatRng;
            }
        }
        catch
        {
        }

        try
        {
            return RunRng.MonsterAi;
        }
        catch
        {
            return null;
        }
    }

    private void AdvanceAfterPerformingMove()
    {
        if (!Creature.IsDead)
        {
            SwitchToNextExpression(refreshMoonText: true);
        }
    }

    private void SwitchToNextExpression(bool refreshMoonText)
    {
        if (_moveStatesByExpression.Count == 0)
        {
            return;
        }

        int expression = ConsumeNextExpression();
        _currentExpression = expression;

        SetMoveImmediate(_moveStatesByExpression[expression], forceTransition: true);
        ApplyVisualExpression();

        if (refreshMoonText && CombatManager.Instance.IsInProgress)
        {
            StartBackgroundMoonTextLoopForCurrentExpression();
        }
    }

    private int ConsumeNextExpression()
    {
        if (_intentSequence.Count == 0)
        {
            GenerateFixedIntentSequence();
        }

        int expression = _intentSequence[_intentSequenceCursor];
        _intentSequenceCursor = (_intentSequenceCursor + 1) % _intentSequence.Count;
        return expression;
    }

    private void ApplyVisualExpression()
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode?.Visuals is TodaysShyLookCreatureVisuals visuals)
        {
            visuals.SetExpression(_currentExpression);
        }
    }

    private void StartBackgroundMoonTextLoopForCurrentExpression()
    {
        if (_currentExpression < 1 || _currentExpression > BackgroundTextLineKeys.Length)
        {
            return;
        }

        MoonTextService.StartRandomLoop(
            BackgroundTextLineKeys[_currentExpression - 1].Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    private static bool IsTodaysShyLookEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is TodaysShyLook);
    }

    private void AddTodaysShyLookPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room || !IsTodaysShyLookEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<TodaysShyLookPageRelic>(room, TodaysShyLookPageRelicTitleLocKey);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new SingleAttackIntent(TodaysExpression1Damage);
        yield return new SingleAttackIntent(TodaysExpression2Damage);
        yield return new DebuffIntent();
        yield return new MultiAttackIntent(TodaysExpression3Damage, TodaysExpression3Hits);
        yield return new DefendIntent();
        yield return new BuffIntent();
    }
}

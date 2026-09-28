using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.AllAroundHelper;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.AllAroundHelper;
using LibraryOfRuina.visuals.AllAroundHelper;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.AllAroundHelper;

internal enum AllAroundHelperInitialMove
{
    Charge,
    Clean,
    Rest
}

public sealed class AllAroundHelper : CounterIntentMonsterModel
{
    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "ALL_AROUND_HELPER.backgroundText.normal.0",
        "ALL_AROUND_HELPER.backgroundText.normal.1",
        "ALL_AROUND_HELPER.backgroundText.normal.2",
        "ALL_AROUND_HELPER.backgroundText.normal.3",
        "ALL_AROUND_HELPER.backgroundText.normal.4"
    ];

    private static readonly string[] RecognizedBackgroundTextLineKeys =
    [
        "ALL_AROUND_HELPER.backgroundText.recognized.0",
        "ALL_AROUND_HELPER.backgroundText.recognized.1",
        "ALL_AROUND_HELPER.backgroundText.recognized.2",
        "ALL_AROUND_HELPER.backgroundText.recognized.3",
        "ALL_AROUND_HELPER.backgroundText.recognized.4"
    ];

    private const string ChargeMoveId = "CHARGE";
    private const string CleanMoveId = "CLEAN";
    private const string RestMoveId = "REST";

    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private const int ChargeBlock = 4;
    private const int ChargeStrength = 1;
    private const int ChargeGuardAmount = 1;
    private const int ChargeGuardTurns = 1;
    private const int CleanHits = 2;
    private const int RestDazedCount = 2;

    private const string AttackSfxPath = "res://audio/sfx/all_around_helper/all_around_helper_attack.ogg";

    private static readonly string AllAroundHelperPageRelicTitleLocKey =
        $"{ModelDb.GetId<AllAroundHelperPageRelic>().Entry}.title";

    private AllAroundHelperInitialMove _initialMove = AllAroundHelperInitialMove.Charge;
    private bool _recognizedTextQueuedForNextTurn;

    private int CleanDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 51, 49);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 54, 50);

    public override int DefaultChaoResistance => 30;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Vulnerable,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                AllAroundHelperCreatureVisuals.Profile.AssetPaths.Count + 8);
            paths.AddRange(
                AllAroundHelperCreatureVisuals.Profile.AssetPaths);

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

        _recognizedTextQueuedForNextTurn = false;
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<LibraryOfRuinaAllAroundHelperRecognitionModePower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead)
        {
            StartBackgroundMoonTextLoopForCurrentTurn();
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

        AddAllAroundHelperPageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    public void QueueRecognizedMoonText()
    {
        _recognizedTextQueuedForNextTurn = true;
    }

    internal void ConfigureInitialMove(AllAroundHelperInitialMove initialMove)
    {
        AssertMutable();
        _initialMove = initialMove;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var charge = new MoveState(
            ChargeMoveId,
            ChargeMove,
            new DefendIntent(),
            new BuffIntent());

        var clean = new MoveState(
            CleanMoveId,
            CleanMove,
            new MultiAttackIntent(CleanDamage, CleanHits));

        var rest = new MoveState(
            RestMoveId,
            RestMove,
            new DetailedStatusCardIntent<Dazed>(
                RestDazedCount,
                PileType.Discard,
                showSingleTargetMarker: false));

        charge.FollowUpState = clean;
        clean.FollowUpState = rest;
        rest.FollowUpState = charge;

        List<MonsterState> states =
        [
            charge,
            clean,
            rest
        ];

        MonsterState initialState = _initialMove switch
        {
            AllAroundHelperInitialMove.Clean => clean,
            AllAroundHelperInitialMove.Rest => rest,
            _ => charge
        };

        return new MonsterMoveStateMachine(states, initialState);
    }

    private async Task ChargeMove(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentTurn();

        await AbnormalityAnimHelper.TriggerCast(Creature);
        await CreatureCmd.GainBlock(Creature, ChargeBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Creature, ChargeStrength, Creature, null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            Creature,
            ChargeGuardAmount,
            ChargeGuardTurns,
            Creature,
            null);
    }

    private async Task CleanMove(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentTurn();

        for (int i = 0; i < CleanHits; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            await AbnormalityAnimHelper.ExecuteAttackSegment(this, CleanDamage);
        }
    }

    private async Task RestMove(IReadOnlyList<Creature> targets)
    {
        StartBackgroundMoonTextLoopForCurrentTurn();

        await AbnormalityAnimHelper.TriggerCast(Creature);

        var playerCreatures = targets.Where(c => c is { IsDead: false, IsPlayer: true }).ToList();

        if (playerCreatures.Count > 0)
        {
            await CardPileCmdCompat.AddToCombatAndPreview<Dazed>(
                playerCreatures,
                PileType.Discard,
                RestDazedCount,
                addedByPlayer: false);
        }
    }

    private void StartBackgroundMoonTextLoopForCurrentTurn()
    {
        IReadOnlyList<string> lineKeys = _recognizedTextQueuedForNextTurn
            ? RecognizedBackgroundTextLineKeys
            : NormalBackgroundTextLineKeys;

        _recognizedTextQueuedForNextTurn = false;

        MoonTextService.StartRandomLoop(
            lineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    private static bool IsAllAroundHelperEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is AllAroundHelper);
    }

    private void AddAllAroundHelperPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room || !IsAllAroundHelperEncounter(room))
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<AllAroundHelperPageRelic>(
                room,
                player,
                AllAroundHelperPageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<AllAroundHelperPageRelic>().ToMutable(), player));
        }
    }

    private static bool HasAllAroundHelperPageReward(CombatRoom room, Player player)
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
                && reward.Description.LocEntryKey == AllAroundHelperPageRelicTitleLocKey);
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (!state.IsMove || state is not MoveState moveState)
            {
                continue;
            }

            foreach (AbstractIntent intent in moveState.Intents)
            {
                yield return intent;
            }
        }
    }
}

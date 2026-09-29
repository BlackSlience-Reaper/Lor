using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.ForsakenMurderer;

public sealed class ForsakenMurderer : CounterIntentMonsterModel
{
    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "FORSAKEN_MURDERER.backgroundText.normal.0",
        "FORSAKEN_MURDERER.backgroundText.normal.1",
        "FORSAKEN_MURDERER.backgroundText.normal.2",
        "FORSAKEN_MURDERER.backgroundText.normal.3"
    ];

    private static readonly string[] StrengthDownBackgroundTextLineKeys =
    [
        "FORSAKEN_MURDERER.backgroundText.strengthDown.0",
        "FORSAKEN_MURDERER.backgroundText.strengthDown.1",
        "FORSAKEN_MURDERER.backgroundText.strengthDown.2"
    ];

    private const string ChainedWrathMoveId = "CHAINED_WRATH";
    private const string MetallicRingingMoveId = "METALLIC_RINGING";

    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private int MetallicRingingDamage =>
     AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 6, 4);

    private const int MetallicRingingHits = 2;

    private int ChainedWrathBlock =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 12, 11);

    private const int ChainedWrathStrength = 1;

    private static readonly string ForsakenMurdererPageRelicTitleLocKey =
        $"{ModelDb.GetId<ForsakenMurdererPageRelic>().Entry}.title";

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 51, 49);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 54, 52);

    public override int DefaultChaoResistance => 35;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Fatal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                ForsakenMurdererCreatureVisuals.Profile.AssetPaths.Count
                + 8);
            paths.AddRange(
                ForsakenMurdererCreatureVisuals.Profile.AssetPaths);

            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    public override Task AfterAddedToRoom()
    {
        // BGM 登记在入场流程返回任务之后（原先是补在本方法上的后缀）：恐惧能力的施加已经开始，登记抛异常也不会拦住它。
        Task added = AddedToRoomAsync();
        EncounterBgmController.RegisterMonster(Creature);
        return added;
    }

    private async Task AddedToRoomAsync()
    {
        await base.AfterAddedToRoom();

        ForsakenMurdererFearBackgroundOverlay.SetOverlayVisible(false);

        await PowerCmdCompat.Apply<LibraryOfRuinaForsakenMurdererFearPower>(
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
            RefreshBackgroundMoonTextLoop();
        }

        return Task.CompletedTask;
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
        ForsakenMurdererFearBackgroundOverlay.SetOverlayVisible(false);
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

        AddForsakenMurdererPageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var metallicRinging = new MoveState(
            MetallicRingingMoveId,
            MetallicRingingMove,
            new MultiAttackIntent(MetallicRingingDamage, MetallicRingingHits));

        var chainedWrath = new MoveState(
            ChainedWrathMoveId,
            ChainedWrathMove,
            new DefendIntent(),
            new BuffIntent());

        metallicRinging.FollowUpState = chainedWrath;
        chainedWrath.FollowUpState = metallicRinging;

        List<MonsterState> states =
        [
            metallicRinging,
            chainedWrath
        ];

        return new MonsterMoveStateMachine(states, chainedWrath);
    }

    private async Task MetallicRingingMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < MetallicRingingHits; i++)
        {
            if (Creature.IsDead) return;
            await AbnormalityAnimHelper.ExecuteAttackSegment(this, MetallicRingingDamage);
        }
    }

    private async Task ChainedWrathMove(IReadOnlyList<Creature> targets)
    {
        await AbnormalityAnimHelper.TriggerCast(Creature);
        await CreatureCmd.GainBlock(Creature, ChainedWrathBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, ChainedWrathStrength, Creature, null);
    }

    private static bool IsForsakenMurdererEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is ForsakenMurderer);
    }

    private void AddForsakenMurdererPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room || !IsForsakenMurdererEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<ForsakenMurdererPageRelic>(room, ForsakenMurdererPageRelicTitleLocKey);
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

    public void RefreshBackgroundMoonTextLoop()
    {
        IReadOnlyList<string> lineKeys = Creature.GetPower<LibraryWeakPower>() != null
            ? StrengthDownBackgroundTextLineKeys
            : NormalBackgroundTextLineKeys;

        MoonTextService.StartRandomLoop(
            lineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }
}

using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.backgrounds.ForsakenMurderer;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.ForsakenMurderer;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.ForsakenMurderer;
using LibraryOfRuina.visuals.ForsakenMurderer;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.ForsakenMurderer;

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

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();

        ForsakenMurdererFearBackgroundOverlay.SetOverlayVisible(false);
        EncounterBgmController.RegisterMonster(Creature);

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
        ForsakenMurdererFearBackgroundOverlay.SetOverlayVisible(false);
        base.BeforeRemovedFromRoom();
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

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<ForsakenMurdererPageRelic>(
                room,
                player,
                ForsakenMurdererPageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<ForsakenMurdererPageRelic>().ToMutable(), player));
        }
    }

    private static bool HasForsakenMurdererPageReward(CombatRoom room, Player player)
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
                && reward.Description.LocEntryKey == ForsakenMurdererPageRelicTitleLocKey);
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

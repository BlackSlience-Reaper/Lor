using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.HappyTeddy;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.HappyTeddy;
using LibraryOfRuina.visuals.HappyTeddy;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.HappyTeddy;

public sealed class HappyTeddyMonster : CounterIntentMonsterModel
{
    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "HAPPY_TEDDY_MONSTER.backgroundText.normal.0",
        "HAPPY_TEDDY_MONSTER.backgroundText.normal.1",
        "HAPPY_TEDDY_MONSTER.backgroundText.normal.2",
        "HAPPY_TEDDY_MONSTER.backgroundText.normal.3",
        "HAPPY_TEDDY_MONSTER.backgroundText.normal.4",
        "HAPPY_TEDDY_MONSTER.backgroundText.normal.5"
    ];

    private static readonly string[] AffectionBackgroundTextLineKeys =
    [
        "HAPPY_TEDDY_MONSTER.backgroundText.affection.0",
        "HAPPY_TEDDY_MONSTER.backgroundText.affection.1",
        "HAPPY_TEDDY_MONSTER.backgroundText.affection.2"
    ];

    private static readonly string[] EmbraceBackgroundTextLineKeys =
    [
        "HAPPY_TEDDY_MONSTER.backgroundText.embrace.0",
        "HAPPY_TEDDY_MONSTER.backgroundText.embrace.1",
        "HAPPY_TEDDY_MONSTER.backgroundText.embrace.2"
    ];

    private const string DisplayOfAffectionMoveId = "DISPLAY_OF_AFFECTION";
    private const string TimidEndearmentMoveId = "TIMID_ENDEARMENT";
    private const string NostalgicEmbraceMoveId = "NOSTALGIC_EMBRACE_OF_THE_OLD_DAYS";

    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private const int DisplayOfAffectionHits = 3;
    private const int TimidEndearmentBlock = 6;
    private const int TimidEndearmentStrength = 1;
    private const int NostalgicEmbraceDamage = 19;
    private const int NostalgicEmbraceTriggerAffection = 3;
    private const int ConfusionTurns = 1;

    private const string DisplayOfAffectionSfxPath = "res://audio/sfx/happy_teddy/happy_teddy_normal_attack.ogg";
    private const string NostalgicEmbraceSfxPath = "res://audio/sfx/happy_teddy/happy_teddy_embrace.ogg";

    private static readonly string HappyTeddyPageRelicTitleLocKey = $"{ModelDb.GetId<HappyTeddyPageRelic>().Entry}.title";

    private int DisplayOfAffectionDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private int DodgeAmount => AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    private int _baseCadenceIndex;
    private bool _specialAttackQueued;
    private HappyTeddyBackgroundTextPool _currentBackgroundTextPool;

    private MoveState? NostalgicEmbraceState { get; set; }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 67, 55);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 69, 58);

    public override int DefaultChaoResistance => 50;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Vulnerable,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Vulnerable,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                HappyTeddyCreatureVisuals.Profile.AssetPaths.Count + 8);
            paths.AddRange(HappyTeddyCreatureVisuals.Profile.AssetPaths);

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

        _baseCadenceIndex = 0;
        _specialAttackQueued = false;
        _currentBackgroundTextPool = HappyTeddyBackgroundTextPool.None;

        EncounterBgmController.RegisterMonster(Creature);

        LibraryOfRuinaHappyTeddyAffectionPower? affectionPower = await PowerCmdCompat.Apply<LibraryOfRuinaHappyTeddyAffectionPower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
        affectionPower?.SetAmount(0, silent: true);
    }

    public override Task BeforeCombatStart()
    {
        if (!Creature.IsDead)
        {
            RefreshBackgroundMoonTextLoop();
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

        AddHappyTeddyPageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    public bool CanGainAffectionFromPlayedAttack()
    {
        if (_specialAttackQueued || Creature.IsDead)
        {
            return false;
        }

        return NextMove.Id == DisplayOfAffectionMoveId;
    }

    public async Task QueueNostalgicEmbraceFromAffection()
    {
        if (_specialAttackQueued || Creature.IsDead || NostalgicEmbraceState == null)
        {
            return;
        }

        _specialAttackQueued = true;
        SetMoveImmediate(NostalgicEmbraceState, forceTransition: true);

        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (creatureNode != null)
        {
            await creatureNode.RefreshIntents();
        }

        RefreshBackgroundMoonTextLoop();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();

        var displayOfAffection = new MoveState(
            DisplayOfAffectionMoveId,
            DisplayOfAffectionMove,
            new MultiAttackIntent(DisplayOfAffectionDamage, DisplayOfAffectionHits)
            //new DodgeIntent(DodgeAmount)
            );

        var timidEndearment = new MoveState(
            TimidEndearmentMoveId,
            TimidEndearmentMove,
            new DefendIntent(),
            new BuffIntent());

        var nostalgicEmbrace = new MoveState(
            NostalgicEmbraceMoveId,
            NostalgicEmbraceMove,
            new SingleAttackIntent(NostalgicEmbraceDamage),
            new DebuffIntent(strong: true));

        NostalgicEmbraceState = nostalgicEmbrace;

        var chooser = new ConditionalBranchState("CHOOSER");
        chooser.AddState(nostalgicEmbrace, () => _specialAttackQueued);
        chooser.AddState(displayOfAffection, () => _baseCadenceIndex == 0);
        chooser.AddState(timidEndearment, () => true);

        displayOfAffection.FollowUpState = chooser;
        timidEndearment.FollowUpState = chooser;
        nostalgicEmbrace.FollowUpState = chooser;

        states.Add(displayOfAffection);
        states.Add(timidEndearment);
        states.Add(nostalgicEmbrace);
        states.Add(chooser);

        return new MonsterMoveStateMachine(states, chooser);
    }

    private async Task DisplayOfAffectionMove(IReadOnlyList<Creature> targets)
    {
        for (int i = 0; i < DisplayOfAffectionHits; i++)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(DisplayOfAffectionSfxPath, -2.5f);
            await AbnormalityAnimHelper.ExecuteAttackSegment(this, DisplayOfAffectionDamage);
        }
        //await LibraryOfRuinaDodgeDicePower.ApplyDodge(Creature, DodgeAmount, Creature, null);
        AdvanceBaseCadence();
    }

    private async Task TimidEndearmentMove(IReadOnlyList<Creature> targets)
    {
        await AbnormalityAnimHelper.TriggerCast(Creature);
        await CreatureCmd.GainBlock(Creature, TimidEndearmentBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<StrengthPower>(Creature, TimidEndearmentStrength, Creature, null);

        AdvanceBaseCadence();
    }

    private async Task NostalgicEmbraceMove(IReadOnlyList<Creature> targets)
    {
        RefreshBackgroundMoonTextLoop();
        LocalOggOneShotPlayer.Play(NostalgicEmbraceSfxPath, -2f);

        await DamageCmd.Attack(NostalgicEmbraceDamage)
            .FromMonster(this)
            .WithAttackerAnim("NostalgicEmbrace", 0.375f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await PowerCmdCompat.Apply<LibraryOfRuinaConfusionPower>(targets, ConfusionTurns, Creature, null);

        _specialAttackQueued = false;
        await LibraryOfRuinaHappyTeddyAffectionPower.ResetAffectionStacks(Creature);
        RefreshBackgroundMoonTextLoop();
    }

    private void AdvanceBaseCadence()
    {
        _baseCadenceIndex = (_baseCadenceIndex + 1) % 2;
    }

    private static bool IsHappyTeddyEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is HappyTeddyMonster);
    }

    private void AddHappyTeddyPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room || !IsHappyTeddyEncounter(room))
        {
            return;
        }

        AbnormalityPageRewardHelper.AddPageRewardForEachPlayer<HappyTeddyPageRelic>(room, HappyTeddyPageRelicTitleLocKey);
    }

    private static bool HasHappyTeddyPageReward(CombatRoom room, Player player)
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
                && reward.Description.LocEntryKey == HappyTeddyPageRelicTitleLocKey);
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
        HappyTeddyBackgroundTextPool nextPool = ResolveBackgroundTextPool();
        if (_currentBackgroundTextPool == nextPool)
        {
            return;
        }

        _currentBackgroundTextPool = nextPool;
        IReadOnlyList<string> lineKeys = nextPool switch
        {
            HappyTeddyBackgroundTextPool.Embrace => EmbraceBackgroundTextLineKeys,
            HappyTeddyBackgroundTextPool.Affection => AffectionBackgroundTextLineKeys,
            _ => NormalBackgroundTextLineKeys
        };

        MoonTextService.StartRandomLoop(
            lineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    private HappyTeddyBackgroundTextPool ResolveBackgroundTextPool()
    {
        if (_specialAttackQueued)
        {
            return HappyTeddyBackgroundTextPool.Embrace;
        }

        LibraryOfRuinaHappyTeddyAffectionPower? affection = Creature.GetPower<LibraryOfRuinaHappyTeddyAffectionPower>();
        return affection != null && affection.Amount > 0 && affection.Amount < NostalgicEmbraceTriggerAffection
            ? HappyTeddyBackgroundTextPool.Affection
            : HappyTeddyBackgroundTextPool.Normal;
    }

    private enum HappyTeddyBackgroundTextPool
    {
        None,
        Normal,
        Affection,
        Embrace
    }
}

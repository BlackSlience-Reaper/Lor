using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.FairyFestival;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.FairyFestival;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.FairyFestival;
using LibraryOfRuina.visuals.FairyFestival;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.FairyFestival;

public sealed class FairyQueen : CounterIntentMonsterModel
{
    private const string QueensDecreeMoveId = "QUEENS_DECREE";
    private const string PredationMoveId = "PREDATION";
    private const string StarvedFlutteringMoveId = "STARVED_FLUTTERING";

    private const float SegmentDelaySeconds = 1.35f;
    private const int QueensDecreeBlock = 9;
    private const int PredationHeal = 6;
    private const int PredationBlock = 10;
    private const int StarvedFlutteringBleed = 1;
    // 饥饿狂乱吞噬全部畸块时，女王剩余生命占最大生命的百分比阈值。
    public const int StarvedFrenzyHpThresholdPercent = 25;

    public const string Root = "res://images/monsters/fairy_festival/";
    public const string IdleTexturePath = Root + "fairy_queen.png";
    public const string IdleAltTexturePath = Root + "fairy_queen_idle_alt.png";
    public const string AttackTexturePath = Root + "fairy_queen_attack.png";
    public const string CastTexturePath = Root + "fairy_queen_cast.png";
    public const string HitTexturePath = Root + "fairy_queen_hit.png";

    public const string SfxRoot = "res://audio/sfx/fairy_festival/";
    public const string AttackSfxPath = SfxRoot + "queen_attack.ogg";
    public const string PredationSfxPath = SfxRoot + "queen_predation.ogg";
    public const string BreathSfxPath = SfxRoot + "queen_breath.ogg";
    public const string ChangeSfxPath = SfxRoot + "queen_change.ogg";
    public const string SpecialSfxPath = SfxRoot + "special.ogg";

    private static readonly string FairyFestivalPageRelicTitleLocKey =
        $"{ModelDb.GetId<FairyFestivalPageRelic>().Entry}.title";

    private static readonly string[] NormalBackgroundTextLineKeys =
    [
        "FAIRY_QUEEN.backgroundText.normal.0",
        "FAIRY_QUEEN.backgroundText.normal.1",
        "FAIRY_QUEEN.backgroundText.normal.2",
        "FAIRY_QUEEN.backgroundText.normal.3"
    ];

    private static readonly string[] CareBackgroundTextLineKeys =
    [
        "FAIRY_QUEEN.backgroundText.care.0",
        "FAIRY_QUEEN.backgroundText.care.1",
        "FAIRY_QUEEN.backgroundText.care.2"
    ];

    private static readonly string[] MomentarySatietyBackgroundTextLineKeys =
    [
        "FAIRY_QUEEN.backgroundText.momentarySatiety.0",
        "FAIRY_QUEEN.backgroundText.momentarySatiety.1",
        "FAIRY_QUEEN.backgroundText.momentarySatiety.2"
    ];

    private static readonly string[] StarvedFrenzyBackgroundTextLineKeys =
    [
        "FAIRY_QUEEN.backgroundText.starvedFrenzy.0",
        "FAIRY_QUEEN.backgroundText.starvedFrenzy.1",
        "FAIRY_QUEEN.backgroundText.starvedFrenzy.2"
    ];

    private const float BackgroundTextIntervalSeconds = 5f;
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 200f, 900f, 450f);

    private bool _starvedFrenzyTriggered;
    private bool _starvedAlonePending;
    private bool _starvedAloneRewardApplied;
    private bool _momentarySatietyTextQueuedForNextRefresh;
    private FairyQueenBackgroundTextPool _currentBackgroundTextPool;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 110, 88);

    public override int MaxInitialHp => MinInitialHp;

    public override int DefaultChaoResistance => 90;

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

    private static int PredationDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private static int StarvedFlutteringFirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    private static int StarvedFlutteringSecondDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 4, 3);

    private static int StarvedFlutteringThirdDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 3, 2);

    private static int[] StarvedFlutteringDamages =>
        [StarvedFlutteringFirstDamage, StarvedFlutteringSecondDamage, StarvedFlutteringThirdDamage];

    public override IEnumerable<string> AssetPaths =>
        FairyQueenCreatureVisuals.Profile.AssetPaths
        .Concat(new[]
        {
            AttackSfxPath,
            PredationSfxPath,
            BreathSfxPath,
            ChangeSfxPath,
            SpecialSfxPath,
            "res://images/vfx/fairy_festival_predation_overlay.png"
        })
        .Concat(EnumerateIntentAssets().SelectMany(intent => intent.AssetPaths))
        .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _starvedFrenzyTriggered = false;
        _starvedAlonePending = false;
        _starvedAloneRewardApplied = false;
        _momentarySatietyTextQueuedForNextRefresh = false;
        _currentBackgroundTextPool = FairyQueenBackgroundTextPool.None;
        EncounterBgmController.RegisterMonster(Creature);
        FairyFestivalBackgroundController.SetStarvedBackground(false);

        await PowerCmdCompat.Apply<FairyQueenMomentarySatietyPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<FairyQueenStarvedFrenzyPower>(Creature, 1m, Creature, null, silent: true);
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

        AddFairyFestivalPageRewards();
        return Task.CompletedTask;
    }

    internal bool IsStarvedFrenzyReady =>
        !_starvedFrenzyTriggered
        && Creature.IsAlive
        && Creature.MaxHp > 0
        && Creature.CurrentHp * 100m <= Creature.MaxHp * StarvedFrenzyHpThresholdPercent;

    public bool ShouldTriggerStarvedFrenzy()
    {
        if (!IsStarvedFrenzyReady)
        {
            return false;
        }

        _starvedFrenzyTriggered = true;
        RefreshBackgroundMoonTextLoop(force: true);
        return true;
    }

    public void MarkStarvedAlonePending()
    {
        if (_starvedAloneRewardApplied)
        {
            return;
        }

        _starvedAlonePending = true;
    }

    public bool TryConsumeStarvedAloneReward()
    {
        if (_starvedAloneRewardApplied || !_starvedAlonePending)
        {
            return false;
        }

        _starvedAlonePending = false;
        _starvedAloneRewardApplied = true;
        return true;
    }

    public async Task Devour(Creature food, bool forceKill = true)
    {
        if (Creature.IsDead || food.IsDead)
        {
            return;
        }

        _momentarySatietyTextQueuedForNextRefresh = true;
        FairyFestivalBackgroundController.PlayPredationOverlay();
        LocalOggOneShotPlayer.Play(PredationSfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(Creature, "Cast", SegmentDelaySeconds);
        await CreatureCmd.Kill(food, force: forceKill);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var queensDecree = new MoveState(
            QueensDecreeMoveId,
            QueensDecreeMove,
            new DefendIntent(),
            new DetailedBuffIntent<FairyFestivalReservedFoodPower>(
                1,
                DetailedBuffTargetScope.RandomEnemy));

        var predation = new MoveState(
            PredationMoveId,
            PredationMove,
            new SingleAttackIntent(PredationDamage),
            new HealIntent(),
            new DefendIntent());

        var starvedFluttering = new MoveState(
            StarvedFlutteringMoveId,
            StarvedFlutteringMove,
            new BadgedAttackIntent(
                StarvedFlutteringFirstDamage,
                "FAIRY_FESTIVAL_UNBLOCKED_BLEED_ATTACK.description",
                IntentBadge.Bleed(StarvedFlutteringBleed)),
            new BadgedAttackIntent(
                StarvedFlutteringSecondDamage,
                "FAIRY_FESTIVAL_UNBLOCKED_BLEED_ATTACK.description",
                IntentBadge.Bleed(StarvedFlutteringBleed)),
            new BadgedAttackIntent(
                StarvedFlutteringThirdDamage,
                "FAIRY_FESTIVAL_UNBLOCKED_BLEED_ATTACK.description",
                IntentBadge.Bleed(StarvedFlutteringBleed)));

        queensDecree.FollowUpState = predation;
        predation.FollowUpState = starvedFluttering;
        starvedFluttering.FollowUpState = queensDecree;

        return new MonsterMoveStateMachine(
            new MonsterState[] { queensDecree, predation, starvedFluttering },
            queensDecree);
    }

    private async Task QueensDecreeMove(IReadOnlyList<Creature> targets)
    {
        if (ShouldStopCurrentMove())
        {
            return;
        }

        RefreshBackgroundMoonTextLoop();
        LocalOggOneShotPlayer.Play(BreathSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Cast", SegmentDelaySeconds);
        if (ShouldStopCurrentMove())
        {
            return;
        }

        await CreatureCmd.GainBlock(Creature, QueensDecreeBlock, ValueProp.Move, null);
        Creature? food = RunRng.MonsterAi.NextItem(FairyFestivalCombatHelper.GetLivingFairyMasses(Creature).ToList());
        if (food != null)
        {
            await PowerCmdCompat.Apply<FairyFestivalReservedFoodPower>(food, 1m, Creature, null);
        }
    }

    private bool ShouldStopCurrentMove() => Creature.IsDead || Creature.CombatState == null;

    private async Task PredationMove(IReadOnlyList<Creature> targets)
    {
        RefreshBackgroundMoonTextLoop();
        LocalOggOneShotPlayer.Play(AttackSfxPath, -1.5f);
        await ExecuteSegmentAttack(PredationDamage);
        await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, PredationHeal));
        await CreatureCmd.GainBlock(Creature, PredationBlock, ValueProp.Move, null);
    }

    private async Task StarvedFlutteringMove(IReadOnlyList<Creature> targets)
    {
        RefreshBackgroundMoonTextLoop();
        foreach (int damage in StarvedFlutteringDamages)
        {
            if (Creature.IsDead) return;
            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            AttackCommand attack = await ExecuteSegmentAttack(damage);
            IReadOnlyList<Creature> bleedTargets = AttackCommandCompat.Results(attack)
                .Where(result => result.Receiver.IsPlayer && result.UnblockedDamage > 0)
                .Select(result => result.Receiver)
                .Distinct()
                .ToList();

            if (bleedTargets.Count > 0)
            {
                await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, StarvedFlutteringBleed, Creature, null);
            }
        }
    }

    private Task<AttackCommand> ExecuteSegmentAttack(int damage)
    {
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private void AddFairyFestivalPageRewards()
    {
        if (Creature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is FairyQueen))
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<FairyFestivalPageRelic>(
                room,
                player,
                FairyFestivalPageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<FairyFestivalPageRelic>().ToMutable(), player));
        }
    }

    private void RefreshBackgroundMoonTextLoop(bool force = false)
    {
        FairyQueenBackgroundTextPool nextPool = ResolveBackgroundTextPool();
        if (!force && _currentBackgroundTextPool == nextPool && !_momentarySatietyTextQueuedForNextRefresh)
        {
            return;
        }

        _currentBackgroundTextPool = nextPool;

        if (_momentarySatietyTextQueuedForNextRefresh && nextPool != FairyQueenBackgroundTextPool.StarvedFrenzy)
        {
            _momentarySatietyTextQueuedForNextRefresh = false;
            MoonTextService.StartRandomLoop(
                MomentarySatietyBackgroundTextLineKeys.Select(L10NMonsterLookup).ToArray(),
                BackgroundTextIntervalSeconds,
                BackgroundTextSpawnArea);
            return;
        }

        IReadOnlyList<string> lineKeys = nextPool switch
        {
            FairyQueenBackgroundTextPool.StarvedFrenzy => StarvedFrenzyBackgroundTextLineKeys,
            FairyQueenBackgroundTextPool.Care => CareBackgroundTextLineKeys,
            _ => NormalBackgroundTextLineKeys
        };

        MoonTextService.StartRandomLoop(
            lineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    private FairyQueenBackgroundTextPool ResolveBackgroundTextPool()
    {
        if (_starvedFrenzyTriggered)
        {
            return FairyQueenBackgroundTextPool.StarvedFrenzy;
        }

        bool hasReservedFood = FairyFestivalCombatHelper
            .GetLivingFairyMasses(Creature)
            .Any(creature => creature.GetPower<FairyFestivalReservedFoodPower>() != null);

        return hasReservedFood ? FairyQueenBackgroundTextPool.Care : FairyQueenBackgroundTextPool.Normal;
    }

    private enum FairyQueenBackgroundTextPool
    {
        None,
        Normal,
        Care,
        StarvedFrenzy
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new DefendIntent();
        yield return new DetailedBuffIntent<FairyFestivalReservedFoodPower>(
            1,
            DetailedBuffTargetScope.RandomEnemy);
        yield return new SingleAttackIntent(PredationDamage);
        yield return new HealIntent();
        yield return new DefendIntent();
        yield return new BadgedAttackIntent(
            StarvedFlutteringFirstDamage,
            "FAIRY_FESTIVAL_UNBLOCKED_BLEED_ATTACK.description",
            IntentBadge.Bleed(StarvedFlutteringBleed));
        yield return new BadgedAttackIntent(
            StarvedFlutteringSecondDamage,
            "FAIRY_FESTIVAL_UNBLOCKED_BLEED_ATTACK.description",
            IntentBadge.Bleed(StarvedFlutteringBleed));
        yield return new BadgedAttackIntent(
            StarvedFlutteringThirdDamage,
            "FAIRY_FESTIVAL_UNBLOCKED_BLEED_ATTACK.description",
            IntentBadge.Bleed(StarvedFlutteringBleed));
    }
}

using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.SpinyBus;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.helpers;
using LibraryOfRuina.powers.SpinyBus;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.SpinyBus;
using LibraryOfRuina.visuals.SpinyBus;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
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

namespace LibraryOfRuina.monsters.SpinyBus;

public sealed class SpinyBus : LorMonsterModel
{
    private const float SegmentDelaySeconds = AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds;

    private const string TrustGameMoveId = "TRUST_GAME";
    private const string GrinningMoveId = "GRINNING";
    private const string LittleHeadExplodingMoveId = "LITTLE_HEAD_EXPLODING";

    private const int TrustGameBlock = 14;
    private const int TrustGameThorns = 3;
    private const int GrinningThorns = 2;
    private const int WeakStacks = 1;
    private const int LittleHeadHits = 3;
    private const string BackgroundTextScope = "spiny_bus";
    private const float BackgroundTextIntervalSeconds = 5f;

    private static readonly string SpinyBusPageRelicTitleLocKey = $"{ModelDb.GetId<SpinyBusPageRelic>().Entry}.title";
    private static readonly Rect2 BackgroundTextSpawnArea = new(150f, 190f, 980f, 470f);

    private static readonly string[] BackgroundTextLineKeys =
    [
        "SPINY_BUS.backgroundText.normal.0",
        "SPINY_BUS.backgroundText.normal.1",
        "SPINY_BUS.backgroundText.normal.2",
        "SPINY_BUS.backgroundText.normal.3",
        "SPINY_BUS.backgroundText.normal.4"
    ];

    private const string Root = "res://images/monsters/spiny_bus/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string ParryTexturePath = Root + "parry.png";
    public const string AttackTexturePath = Root + "attack.png";
    public const string Attack2TexturePath = Root + "attack2.png";
    public const string HitTexturePath = Root + "hit.png";

    private const string SfxRoot = "res://audio/sfx/spiny_bus/";
    public const string ParrySfxPath = SfxRoot + "special.ogg";
    public const string HitSfxPath = SfxRoot + "hit.ogg";
    public const string AttackSfxPath = SfxRoot + "thrust.ogg";

    private static readonly string[] SfxPaths =
    [
        ParrySfxPath,
        HitSfxPath,
        AttackSfxPath
    ];

    private bool _backgroundMoonTextLoopStarted;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 313, 270);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 316, 274);

    public override int DefaultChaoResistance => 150;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Blunt = LibraryResistanceLevel.Endure
    };

    private int GrinningDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 9);

    private int LittleHeadDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 5);

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                SpinyBusCreatureVisuals.Profile.AssetPaths);
            paths.AddRange(SfxPaths);
            paths.Add(SpinyBusAttackOverlayController.OverlayTexturePath);

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
        _backgroundMoonTextLoopStarted = false;
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<SpinyBusSoftBodyPower>(Creature, 1m, Creature, null, silent: true);
        await PowerCmdCompat.Apply<SpinyBusUnbearablePleasurePower>(Creature, 1m, Creature, null, silent: true);
    }

    public override Task BeforeCombatStart()
    {
        StartBackgroundMoonTextLoop();
        return Task.CompletedTask;
    }

    public override void BeforeRemovedFromRoom()
    {
        StopBackgroundMoonTextLoop();
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

        AddSpinyBusPageRewards();
        StopBackgroundMoonTextLoop();
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var trustGame = new MoveState(
            TrustGameMoveId,
            TrustGameMove,
            new DefendIntent(),
            new BuffIntent());

        var grinning = new MoveState(
            GrinningMoveId,
            GrinningMove,
            new SingleAttackIntent(GrinningDamage),
            new BuffIntent());

        var littleHeadExploding = new MoveState(
            LittleHeadExplodingMoveId,
            LittleHeadExplodingMove,
            new MultiAttackIntent(LittleHeadDamage, LittleHeadHits),
            new DebuffIntent());

        trustGame.FollowUpState = grinning;
        grinning.FollowUpState = littleHeadExploding;
        littleHeadExploding.FollowUpState = trustGame;

        return new MonsterMoveStateMachine(
            [trustGame, grinning, littleHeadExploding],
            trustGame);
    }

    private async Task TrustGameMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(ParrySfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Parry", 0.075f);
        await CreatureCmd.GainBlock(Creature, TrustGameBlock, ValueProp.Move, null);
        await PowerCmdCompat.Apply<ThornsPower>(Creature, TrustGameThorns, Creature, null);
    }

    private async Task GrinningMove(IReadOnlyList<Creature> targets)
    {
        SpinyBusAttackOverlayController.PlayOverlay();
        LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
        await ExecuteAttackSegment(GrinningDamage, "Attack");
        await PowerCmdCompat.Apply<ThornsPower>(Creature, GrinningThorns, Creature, null);
    }

    private async Task LittleHeadExplodingMove(IReadOnlyList<Creature> targets)
    {
        SpinyBusAttackOverlayController.PlayOverlay();

        var hitPlayers = new HashSet<Creature>();
        for (int hit = 0; hit < LittleHeadHits; hit++)
        {
            string animId = hit % 2 == 0 ? "Attack" : "Attack2";
            LocalOggOneShotPlayer.Play(AttackSfxPath, -2f);
            AttackCommand attack = await ExecuteAttackSegment(LittleHeadDamage, animId);
            foreach (Creature receiver in AttackCommandCompat.Results(attack)
                .Where(static result => result.Receiver.IsPlayer)
                .Select(static result => result.Receiver))
            {
                hitPlayers.Add(receiver);
            }

            await Cmd.CustomScaledWait(0.04f, 0.08f);
        }

        if (hitPlayers.Count > 0)
        {
            await PowerCmdCompat.ApplyDebuff<WeakPower>(hitPlayers, WeakStacks, Creature, null);
        }
    }

    private Task<AttackCommand> ExecuteAttackSegment(int damage, string animId)
    {
        return DamageCmd.Attack(damage)
            .FromMonster(this)
            .WithAttackerAnim(animId, SegmentDelaySeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private void StartBackgroundMoonTextLoop()
    {
        if (_backgroundMoonTextLoopStarted || Creature.IsDead)
        {
            return;
        }

        _backgroundMoonTextLoopStarted = true;
        MoonTextService.StartRandomLoop(
            Creature,
            BackgroundTextScope,
            BackgroundTextLineKeys.Select(L10NMonsterLookup).ToArray(),
            BackgroundTextIntervalSeconds,
            BackgroundTextSpawnArea);
    }

    private void StopBackgroundMoonTextLoop()
    {
        MoonTextService.StopRandomLoop(Creature, BackgroundTextScope);
        _backgroundMoonTextLoopStarted = false;
    }

    private void AddSpinyBusPageRewards()
    {
        if (Creature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is SpinyBus))
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<SpinyBusPageRelic>(
                room,
                player,
                SpinyBusPageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<SpinyBusPageRelic>().ToMutable(), player));
        }
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new DefendIntent();
        yield return new BuffIntent();
        yield return new SingleAttackIntent(GrinningDamage);
        yield return new MultiAttackIntent(LittleHeadDamage, LittleHeadHits);
        yield return new DebuffIntent();
    }
}

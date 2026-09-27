using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.intents;
using LibraryOfRuina.visuals.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.LiteratureFloorLiberation;

public sealed class LiteratureFloorEnhancedSmallSpider :
    CounterIntentMonsterModel
{
    public const string SharpFangsMoveId = "SHARP_FANGS";
    public const string SlenderWebMoveId = "SLENDER_WEB";
    public const int SharpFangsHits = 2;
    public const int SlenderWebBlock = 6;
    public const int DebuffTurns = 1;

    private const string Root =
        "res://images/monsters/literature_floor_liberation/enhanced_small_spider/";
    public const string IdleTexturePath = Root + "small_spider_idle.png";
    public const string MoveTexturePath = Root + "small_spider_move.png";
    public const string AttackTexturePath = Root + "small_spider_attack.png";
    public const string GuardTexturePath = Root + "small_spider_guard.png";
    public const string HitTexturePath = Root + "small_spider_hit.png";

    private const string SfxRoot =
        "res://audio/sfx/literature_floor_liberation/red_eyes/";
    public const string AttackSfxPath =
        SfxRoot + "enhanced_small_spider_fangs.ogg";
    public const string WebSfxPath =
        SfxRoot + "enhanced_small_spider_web.ogg";

    private MoveState? _sharpFangsState;
    private MoveState? _slenderWebState;
    private bool _startsWithSharpFangs = true;
    private bool _deathReported;

    public bool StartsWithSharpFangs => _startsWithSharpFangs;

    private int SharpFangsDamage =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            2);

    private int SharpFangsFlaw =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            3,
            2);

    private int SlenderWebBinding =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            9,
            6);

    internal static (int Min, int Max) DebugHpRange(bool toughEnemies) =>
        toughEnemies ? (57, 60) : (50, 54);

    internal static int DebugSharpFangsDamage(bool deadlyEnemies) =>
        deadlyEnemies ? 3 : 2;

    internal static int DebugSharpFangsFlaw(bool deadlyEnemies) =>
        deadlyEnemies ? 3 : 2;

    internal static int DebugSlenderWebBinding(bool deadlyEnemies) =>
        deadlyEnemies ? 9 : 6;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            57,
            50);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            60,
            54);

    public override int DefaultChaoResistance => 40;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => SpiderResistance();

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => SpiderResistance();

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                LiteratureFloorEnhancedSmallSpiderCreatureVisuals.ScenePath,
                AttackSfxPath,
                WebSfxPath
            };
            foreach (AbstractIntent intent in EnumerateIntentAssets())
            {
                paths.AddRange(intent.AssetPaths);
            }

            return paths.Distinct();
        }
    }

    public void ConfigureOpeningMove(bool startsWithSharpFangs)
    {
        _startsWithSharpFangs = startsWithSharpFangs;
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _deathReported = false;
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<MinionPower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented
            || creature != Creature
            || _deathReported)
        {
            return;
        }

        _deathReported = true;
        LiteratureFloorRedEyesBoss? redEyes = Creature.CombatState?.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorRedEyesBoss>()
            .FirstOrDefault();
        if (redEyes != null)
        {
            await redEyes.OnEnhancedSmallSpiderDeath();
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _sharpFangsState = new MoveState(
            SharpFangsMoveId,
            SharpFangsMove,
            new CombinedAttackDebuffIntent(
                SharpFangsDamage,
                SharpFangsHits,
                "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER_SHARP_FANGS.description",
                IntentBadge.Flaw(SharpFangsFlaw, DebuffTurns)));
        _slenderWebState = new MoveState(
            SlenderWebMoveId,
            SlenderWebMove,
            new CombinedDefendDebuffIntent(
                SlenderWebBlock,
                "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER_SLENDER_WEB.description",
                IntentBadge.FromPower<LibraryBindingPower>(
                    SlenderWebBinding,
                    DebuffTurns.ToString(),
                    SlenderWebBinding.ToString())));

        _sharpFangsState.FollowUpState = _slenderWebState;
        _slenderWebState.FollowUpState = _sharpFangsState;

        return new MonsterMoveStateMachine(
            [_sharpFangsState, _slenderWebState],
            _startsWithSharpFangs
                ? _sharpFangsState
                : _slenderWebState);
    }

    private async Task SharpFangsMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -1.5f);
        await DamageCmd.Attack(SharpFangsDamage)
            .FromMonster(this)
            .WithHitCount(SharpFangsHits)
            .WithAttackerAnim(
                "Attack",
                LiteratureFloorEnhancedSmallSpiderAnimationContract
                    .AttackDurationSeconds)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        foreach (Creature player in LivingPlayers())
        {
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(
                player,
                SharpFangsFlaw,
                DebuffTurns,
                Creature,
                null);
        }
    }

    private async Task SlenderWebMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(WebSfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(
            Creature,
            "Cast",
            LiteratureFloorEnhancedSmallSpiderAnimationContract
                .CastDurationSeconds);
        await CreatureCmd.GainBlock(
            Creature,
            SlenderWebBlock,
            ValueProp.Move,
            null);

        foreach (Creature player in LivingPlayers())
        {
            await LibraryPowerCmd.Apply<LibraryBindingPower>(
                player,
                SlenderWebBinding,
                DebuffTurns,
                Creature,
                null);
        }
    }

    private IEnumerable<Creature> LivingPlayers() =>
        Creature.CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray() ?? [];

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new CombinedAttackDebuffIntent(
            SharpFangsDamage,
            SharpFangsHits,
            "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER_SHARP_FANGS.description",
            IntentBadge.Flaw(SharpFangsFlaw, DebuffTurns));
        yield return new CombinedDefendDebuffIntent(
            SlenderWebBlock,
            "LITERATURE_FLOOR_ENHANCED_SMALL_SPIDER_SLENDER_WEB.description",
            IntentBadge.FromPower<LibraryBindingPower>(
                SlenderWebBinding,
                DebuffTurns.ToString(),
                SlenderWebBinding.ToString()));
    }

    private static LibraryCreatureResistanceData.Resistance
        SpiderResistance() =>
        new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Vulnerable
        };
}

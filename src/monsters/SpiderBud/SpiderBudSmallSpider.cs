using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.intents;
using LibraryOfRuina.intents.SpiderBud;
using LibraryOfRuina.visuals.SpiderBud;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.monsters.SpiderBud;

public sealed class SpiderBudSmallSpider : CounterIntentMonsterModel
{
    private const string AttackMoveId = "SHARP_FANGS";
    private const string WebMoveId = "SLENDER_WEB";

    private const string Root = "res://images/monsters/spider_bud/";
    public const string IdleTexturePath = Root + "small_spider_idle.png";
    public const string AttackTexturePath = Root + "small_spider_attack.png";
    public const string CastTexturePath = Root + "small_spider_cast.png";

    public const string SfxRoot = "res://audio/sfx/spider_bud/";
    public const string AttackSfxPath = SfxRoot + "small_spider_attack.ogg";
    public const string CastSfxPath = SfxRoot + "small_spider_cast.ogg";

    private static readonly string[] SfxPaths = [AttackSfxPath, CastSfxPath];

    private MoveState _attackState = null!;
    private MoveState _webState = null!;

    private bool _huntQueuedOnBud;

    public bool IsLeftSpider { get; set; }

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 47, 45);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 50, 48);

    public override int DefaultChaoResistance => 50;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Fatal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Fatal
    };

    private int SharpFangsDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private const int SharpFangsHits = 2;

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                SpiderBudSmallSpiderCreatureVisuals.Profile.AssetPaths.Count
                + SfxPaths.Length
                + 4);
            paths.AddRange(
                SpiderBudSmallSpiderCreatureVisuals.Profile.AssetPaths);
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
        _huntQueuedOnBud = false;
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1m, Creature, null, silent: true);
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Creature)
        {
            return;
        }

        if (!_huntQueuedOnBud)
        {
            _huntQueuedOnBud = true;
            SpiderBud? bud = FindSpiderBud();
            bud?.QueueHuntFromSmallSpiderDeath();
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _attackState = new MoveState(
            AttackMoveId,
            SharpFangsMove,
            new MultiAttackIntent(SharpFangsDamage, SharpFangsHits));

        _webState = new MoveState(
            WebMoveId,
            SlenderWebMove,
            new SpiderBudWebDebuffIntent(
                IsLeftSpider ? SpiderBudWebDebuffIntent.WebDebuffType.Bind : SpiderBudWebDebuffIntent.WebDebuffType.Flaw));

        if (IsLeftSpider)
        {
            _attackState.FollowUpState = _webState;
            _webState.FollowUpState = _attackState;
        }
        else
        {
            _webState.FollowUpState = _attackState;
            _attackState.FollowUpState = _webState;
        }

        List<MonsterState> states = [_attackState, _webState];
        MonsterState startState = IsLeftSpider ? _attackState : _webState;
        return new MonsterMoveStateMachine(states, startState);
    }

    private async Task SharpFangsMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(AttackSfxPath, -1.5f);
        await DamageCmd.Attack(SharpFangsDamage)
            .FromMonster(this)
            .WithHitCount(SharpFangsHits)
            .WithAttackerAnim("Attack", 0.225f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);
    }

    private async Task SlenderWebMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(CastSfxPath, -1.5f);
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.45f);

        IReadOnlyList<Creature> webTargets = Creature.CombatState?.Players
            .Select(static player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .ToArray()
            ?? [];

        if (webTargets.Count == 0)
        {
            return;
        }

        if (IsLeftSpider)
        {
            await LibraryPowerCmd.Apply<LibraryBindingPower>(new ThrowingPlayerChoiceContext(), webTargets, 3, 1, false, Creature, null);
        }
        else
        {
            foreach (Creature t in webTargets)
            {
                await LibraryPowerCmd.Apply<LibraryDisarmPower>(t, amount: 1, turns: 2, Creature, null);
            }
            
        }
    }

    private SpiderBud? FindSpiderBud()
    {
        return Creature.CombatState?.Creatures
            .Select(c => c.Monster)
            .OfType<SpiderBud>()
            .FirstOrDefault();
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new MultiAttackIntent(SharpFangsDamage, SharpFangsHits);
        yield return new SpiderBudWebDebuffIntent(SpiderBudWebDebuffIntent.WebDebuffType.Bind);
        yield return new SpiderBudWebDebuffIntent(SpiderBudWebDebuffIntent.WebDebuffType.Flaw);
    }
}

using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

public sealed class QueenBee : LorMonsterModel
{
    private const string RouterId = "QUEEN_BEE_ROUTER";
    private const string VigilanceMoveId = "QUEEN_BEE_VIGILANCE_MOVE";
    private const string WarlikeEnhancementMoveId = "QUEEN_BEE_WARLIKE_ENHANCEMENT_MOVE";
    private const string LoyaltyEnhancementMoveId = "QUEEN_BEE_LOYALTY_ENHANCEMENT_MOVE";

    private const int VigilanceBlock = 9;
    private const int BuffMoveBlock = 11;
    private const int SporeDebuffAmount = 3;
    private const int AllyBuffAmount = 3;
    private const int AllyBuffTurns = 1;
    private const int MaxWorkerCount = 2;
    internal const decimal DeathEmbraceHpThresholdPercent = 0.25m;

    public const string Root = "res://images/monsters/queen_bee/";
    public const string IdleTexturePath = Root + "idle.png";
    public const string HitTexturePath = Root + "hit.png";
    public const string DefendTexturePath = Root + "defend.png";
    public const string CastTexturePath = Root + "cast.png";

    public const string BuffSfxPath = "res://audio/sfx/queen_bee/queen_buff.ogg";
    public const string BlockSfxPath = "res://audio/sfx/queen_bee/queen_evasion.ogg";
    public const string SpawnSfxPath = "res://audio/sfx/queen_bee/queen_spawn.ogg";
    public const string SporeSfxPath = "res://audio/sfx/queen_bee/queen_spore.ogg";
    //private bool _shouldStun;
    private int _currentStep;
    private bool _nextBuffIsWarlike = true;

    internal bool ForceWorkerPromoteGrowthNextTurn { get; private set; }

    internal bool TryClaimWorkerPromoteGrowthNextTurn()
    {
        if (!ForceWorkerPromoteGrowthNextTurn)
        {
            return false;
        }

        ForceWorkerPromoteGrowthNextTurn = false;
        return true;
    }

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Blunt = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Slash = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Blunt = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Endure,
        Slash = LibraryResistanceLevel.Endure
    };

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 130, 124);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 135, 128);

    public override int DefaultChaoResistance => 50;

    public override IEnumerable<string> AssetPaths =>
        QueenBeeCreatureVisuals.Profile.AssetPaths
            .Concat(
            [
                BuffSfxPath,
                BlockSfxPath,
                SpawnSfxPath,
                SporeSfxPath
            ])
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        ForceWorkerPromoteGrowthNextTurn = false;
        await PowerCmdCompat.Apply<QueenBeeSporesPassivePower>(
            Creature,
            1m,
            Creature,
            null,
            silent: true);
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player)
        {
            return;
        }

        _currentStep++;
        if (_currentStep % 4 == 0)
        {
            if (Creature is LibraryCreature libraryCreature)
            {
                await LibraryCreatureCmd.Stun(
                    libraryCreature,
                    VigilanceMoveId);
            }
            else
            {
                await CreatureCmd.Stun(Creature, VigilanceMoveId);
            }
        }
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature == Creature)
        {
            QueenBeeElite.AddPageRewards(Creature);
        }

        await base.AfterDeath(choiceContext, creature, wasRemovalPrevented, deathAnimLength);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        if (target != Creature
            || Creature.IsDead
            || Creature.CurrentHp > Creature.MaxHp * DeathEmbraceHpThresholdPercent)
        {
            return;
        }

        IReadOnlyList<Creature> workers = CombatState?.Enemies
            .Where(static enemy => enemy.IsAlive && enemy.Monster is QueenBeeWorker)
            .ToArray()
            ?? [];
        foreach (Creature worker in workers)
        {
            if (worker.Monster is QueenBeeWorker workerModel)
            {
                await workerModel.ForceDeathEmbraceIfNeeded();
            }
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var vigilance = new MoveState(
            VigilanceMoveId,
            VigilanceMove,
            new DefendIntent(),
            new SummonIntent());

        var warlikeEnhancement = new MoveState(
            WarlikeEnhancementMoveId,
            WarlikeEnhancementMove,
            CreateSporeDefendDebuffIntent(),
            new BadgedBuffIntent(
            [
                IntentBadge.FromPower<QueenBeeNextTurnStrongPower>(
                    AllyBuffAmount,
                    AllyBuffAmount.ToString()),
                IntentBadge.FromPower<QueenBeeNextTurnQuicknessPower>(
                    AllyBuffAmount,
                    AllyBuffAmount.ToString())
            ],
            AllyBuffAmount));

        var loyaltyEnhancement = new MoveState(
            LoyaltyEnhancementMoveId,
            LoyaltyEnhancementMove,
            CreateSporeDefendDebuffIntent(),
            new BadgedBuffIntent(
                IntentBadge.FromPower<LibraryEndurancePower>(
                    AllyBuffAmount,
                    AllyBuffAmount.ToString(),
                    AllyBuffTurns.ToString()),
                AllyBuffAmount));
        var stun = new MoveState(
            stunnedMoveId,
            StunMove,
            new StunIntent());
        var router = new DelegatingMonsterRouterState(
            RouterId,
            ResolveNextState,
            shouldAppearInLogs: false);

        vigilance.FollowUpState = router;
        warlikeEnhancement.FollowUpState = router;
        loyaltyEnhancement.FollowUpState = router;
        stun.FollowUpState = router;
        return new MonsterMoveStateMachine(
            [vigilance, warlikeEnhancement, loyaltyEnhancement, stun,  router],
            vigilance);
    }

    private string ResolveNextState(Creature owner, Rng rng)
    {
        switch (_currentStep % 4)
        {
            case 1:
            case 2:
                return VigilanceMoveId;

            case 3:
                var moveId = _nextBuffIsWarlike
                    ? WarlikeEnhancementMoveId
                    : LoyaltyEnhancementMoveId;
                _nextBuffIsWarlike = !_nextBuffIsWarlike;
                return moveId;

            default: // %4 == 0 时 stun 已注入，router 不会执行到这里
                return VigilanceMoveId;
        }
    }

    private async Task StunMove(IReadOnlyList<Creature> targets)
    {
        //_shouldStun = false;
        
        if (!Creature.IsStunned) await CreatureCmd.Stun(Creature, VigilanceMoveId);
    }

    private async Task VigilanceMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(BlockSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Defend", 0.45f);
        await CreatureCmd.GainBlock(Creature, VigilanceBlock, ValueProp.Move, null);

        if (CombatState?.Encounter is QueenBeeElite encounter)
        {
            await encounter.TrySpawnWorkerIfNeeded(CombatState);
        }
    }

    private async Task WarlikeEnhancementMove(IReadOnlyList<Creature> targets)
    {
        //_shouldStun = true;
        await PerformBuffMove(
            applyStrongAndQuickness: true);

        if (CombatState?.Enemies.Any(
                static enemy => enemy.IsAlive
                    && enemy.Monster is QueenBeeWorker) == true)
        {
            ForceWorkerPromoteGrowthNextTurn = true;
        }
    }

    private async Task LoyaltyEnhancementMove(IReadOnlyList<Creature> targets)
    {
        //_shouldStun = true;
        await PerformBuffMove(
            applyStrongAndQuickness: false);
    }

    private async Task PerformBuffMove(bool applyStrongAndQuickness)
    {
        LocalOggOneShotPlayer.Play(BuffSfxPath, -2f);
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.45f);
        await CreatureCmd.GainBlock(Creature, BuffMoveBlock, ValueProp.Move, null);

        IReadOnlyList<Creature> players = GetLivingPlayers(Creature);
        if (players.Count > 0)
        {
            LocalOggOneShotPlayer.Play(SporeSfxPath, -2f);
            await PowerCmdCompat.Apply<HistoryFloorWaspSporePower>(
                players,
                SporeDebuffAmount,
                Creature,
                null);
        }

        IReadOnlyList<Creature> allies = GetLivingAllies(Creature);
        if (allies.Count == 0)
        {
            return;
        }

        if (applyStrongAndQuickness)
        {
            foreach (Creature ally in allies)
            {
                await PowerCmdCompat.Apply<QueenBeeNextTurnStrongPower>(
                    ally,
                    AllyBuffAmount,
                    Creature,
                    null);
                await PowerCmdCompat.Apply<QueenBeeNextTurnQuicknessPower>(
                    ally,
                    AllyBuffAmount,
                    Creature,
                    null);
            }
        }
        else
        {
            await LibraryPowerCmd.Apply<LibraryEndurancePower>(new ThrowingPlayerChoiceContext(),
                allies,
                AllyBuffAmount,
                AllyBuffTurns,
                false,
                Creature,
                null);
            
        }
    }

    private static IReadOnlyList<Creature> GetLivingPlayers(Creature creature)
    {
        return creature.CombatState!.Creatures
            .Where(static creature => creature.IsAlive && creature.IsPlayer)
            .ToList();
    }

    private static IReadOnlyList<Creature> GetLivingAllies(Creature creature)
    {
        return creature.CombatState!.Enemies
            .Where(enemy => enemy.IsAlive && enemy != creature)
            .ToList();
    }

    private static CombinedDefendDebuffIntent CreateSporeDefendDebuffIntent()
    {
        return new CombinedDefendDebuffIntent(
            BuffMoveBlock,
            null,
            IntentBadge.FromPower<HistoryFloorWaspSporePower>(
                SporeDebuffAmount,
                SporeDebuffAmount.ToString()));
    }

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return new DefendIntent();
        yield return new SummonIntent();
        yield return CreateSporeDefendDebuffIntent();
        yield return new BadgedBuffIntent(
        [
            IntentBadge.FromPower<QueenBeeNextTurnStrongPower>(
                AllyBuffAmount,
                AllyBuffAmount.ToString()),
            IntentBadge.FromPower<QueenBeeNextTurnQuicknessPower>(
                AllyBuffAmount,
                AllyBuffAmount.ToString())
        ],
        AllyBuffAmount);
        yield return new BadgedBuffIntent(
            IntentBadge.FromPower<LibraryEndurancePower>(
                AllyBuffAmount,
                AllyBuffAmount.ToString(),
                AllyBuffTurns.ToString()),
            AllyBuffAmount);
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.RoadHome;

public sealed class ScaredyCatCompanion : LorMonsterModel, ITargetedMonsterAttackProvider
{
    internal const string BlockMoveId = "SCAREDY_CAT_COMPANION_BLOCK";
    internal const string AttackMoveId = "SCAREDY_CAT_COMPANION_ATTACK";
    private const string RouterStateId = "SCAREDY_CAT_COMPANION_ROUTER";

    public const int BaseHp = 50;
    public const int TeamBlock = 3;
    public const int AttackLowDamage = 2;
    public const int AttackHighDamage = 1;
    public const int AttackHits = 2;
    public const int EndTurnHpLoss = 1;

    public const string IdleTexturePath = RoadHomeEncounterHelper.CatTextureRoot + "companion_idle.png";
    public const string HitTexturePath = RoadHomeEncounterHelper.CatTextureRoot + "companion_hit.png";

    public static readonly string[] StaticAssetPaths =
        ScaredyCatCompanionCreatureVisuals
            .Profile.AssetPaths.ToArray();

    private Player? _pageOwner;
    private Creature? _lockedTarget;
    private int _configuredStrengthBonus;
    private int _configuredDexterityBonus;
    private int _configuredMaxHp = BaseHp;
    private int _configuredCurrentHp = BaseHp;
    private bool _summonInitialized;

    public override int MinInitialHp => BaseHp;

    public override int MaxInitialHp => BaseHp;

    public override bool ShowResistanceUi => false;

    public override IEnumerable<string> AssetPaths =>
        StaticAssetPaths
            .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await InitializeSummonedCompanion();
    }

    internal async Task InitializeSummonedCompanion()
    {
        if (_summonInitialized)
        {
            return;
        }

        _summonInitialized = true;
        ApplyConfiguredVitals();
        await ApplyConfiguredPowers();
        await PowerCmdCompat.Apply<ScaredyCatCompanionCowardPower>(Creature, 1, Creature, null, silent: true);
        Creature.PrepareForNextTurn(CombatState.Enemies);
        if (NCombatRoom.Instance?.GetCreatureNode(Creature) is { } creatureNode)
        {
            await creatureNode.RefreshIntents();
        }

        Log.Info($"[LibraryOfRuina.RoadHome] Companion initialized: hp={Creature.CurrentHp}/{Creature.MaxHp} "
            + $"strength={Creature.GetPower<StrengthPower>()?.Amount ?? 0} "
            + $"dexterity={Creature.GetPower<DexterityPower>()?.Amount ?? 0} "
            + $"configuredMaxHp={_configuredMaxHp} "
            + $"nextMove={NextMove.Id} showResistanceUi={ShowResistanceUi}");
    }

    public void ConfigureFromPageOwner(
        Player owner,
        int strengthBonus,
        int dexterityBonus,
        int maxHp,
        int currentHp)
    {
        _pageOwner = owner;
        _configuredStrengthBonus = Math.Max(0, strengthBonus);
        _configuredDexterityBonus = Math.Max(0, dexterityBonus);
        _configuredMaxHp = Math.Max(1, maxHp);
        _configuredCurrentHp = Math.Clamp(currentHp + RoadHomePageRelic.CompanionGrowthMaxHp, 1, _configuredMaxHp);
    }

    public bool IsOwnedBy(Player owner) => ReferenceEquals(_pageOwner, owner);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState block = new(BlockMoveId, BlockMove, new DefendIntent());
        MoveState attack = new(
            AttackMoveId,
            AttackMove,
            new TargetedMonsterAttackIntent(() => GetAttackDamage(), () => AttackHits, "SCAREDY_CAT_COMPANION_ATTACK.description"));
        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolvePlannedMoveId(rng));
        block.FollowUpState = router;
        attack.FollowUpState = router;
        return new MonsterMoveStateMachine([block, attack, router], router);
    }

    internal string ResolvePlannedMoveId(Rng rng)
    {
        foreach (var m in CombatState.Enemies)
        {
            if (m.GetPower<LibraryOfRuinaFocusOfAttentionPower>() != null)
                _lockedTarget = m;
        }
        
        _lockedTarget ??= PickLivingEnemyTarget(rng);
        return rng.NextInt(2) == 0 ? BlockMoveId : AttackMoveId;
    }

    public bool UsesTargetedAttackContract(Creature owner)
    {
        return owner == Creature
            && NextMove.Id == AttackMoveId;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner) =>
        _lockedTarget is { IsAlive: true } ? [_lockedTarget] : [];

    public string GetTargetedAttackTargetName(Creature owner) =>
        _lockedTarget?.Name ?? string.Empty;

    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal amount, ValueProp props, Creature? dealer)
    {
        if (amount <= 0
            || Creature.IsDead
            || !ValuePropCompat.IsPoweredAttack(props)
            || _pageOwner?.Creature != target
            || dealer == Creature)
        {
            return target;
        }

        return Creature;
    }

    private void ApplyConfiguredVitals()
    {
        Creature.SetMaxHpInternal(_configuredMaxHp);
        Creature.SetCurrentHpInternal(_configuredCurrentHp);
    }

    private async Task ApplyConfiguredPowers()
    {
        if (_configuredStrengthBonus > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(Creature, _configuredStrengthBonus, Creature, null, silent: true);
        }

        if (_configuredDexterityBonus > 0)
        {
            await PowerCmdCompat.Apply<DexterityPower>(Creature, _configuredDexterityBonus, Creature, null, silent: true);
        }
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented
            && creature == Creature
            && CombatManager.Instance.IsInProgress)
        {
            _pageOwner?.GetRelic<RoadHomePageRelic>()?.RecordCompanionDeath(creature.MaxHp);
        }

        return base.AfterDeath(choiceContext, creature, wasRemovalPrevented, deathAnimLength);
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature == Creature && creature.IsAlive && CombatManager.Instance.IsInProgress)
        {
            _pageOwner?.GetRelic<RoadHomePageRelic>()?.RecordCompanionCurrentHp(creature.CurrentHp);
        }

        return base.AfterCurrentHpChanged(creature, delta);
    }

    // ShouldAllowTargeting and ShouldAllowHitting are now handled centrally by
    // FriendlyAllyProtectionPatch based on AllyType.Friendly in AllyTurnRegistry.

    // public override async Task BeforeSideTurnStart(
    //     PlayerChoiceContext choiceContext,
    //     CombatSide side,
    //     IReadOnlyList<Creature> participants,
    //     CombatStateLike combatState)
    // {
    //     if (side == CombatSide.Player)
    //     {
    //         foreach (var player in CombatState.PlayerCreatures.Where(c => c.IsAlive))
    //         {
    //             await CreatureCmd.GainBlock(player, TeamBlock, ValueProp.Move, null);
    //         }
    //     }
    // }

    private async Task BlockMove(IReadOnlyList<Creature> targets)
    {
        int dexterityBonus = Math.Max(0, Creature.GetPower<DexterityPower>()?.Amount ?? 0);
        foreach (var ally in CombatState.PlayerCreatures.Where(c => c.IsAlive))
        {
            if (ally is { IsDead: false, IsEnemy: false })
            {
                await CreatureCmd.GainBlock(ally, TeamBlock + dexterityBonus, ValueProp.Move, null);
            }
        }
    }

    private async Task AttackMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = _lockedTarget is { IsAlive: true } ? _lockedTarget : PickLivingEnemyTarget(RunRng.MonsterAi);
        if (target == null)
        {
            return;
        }

        LocalOggOneShotPlayer.Play(ScaredyCat.LionAttackSfxPath, -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, [target]))
        {
            await DamageCmd.Attack(GetAttackDamage())
                .FromMonster(this)
                .WithHitCount(AttackHits)
                .WithAttackerAnim("AttackStrike", 0.2f)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(null);
        }
    }

    private Creature? PickLivingEnemyTarget(Rng rng)
    {
        IReadOnlyList<Creature> enemies = Creature.CombatState?.Enemies
            .Where(creature => creature.IsAlive && creature != Creature && creature.IsPrimaryEnemy)
            .ToArray()
            ?? [];
        return enemies.Count == 0 ? null : rng.NextItem(enemies);
    }

    private int GetAttackDamage() =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            AttackHighDamage,
            AttackLowDamage);

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        MonsterMoveStateMachine stateMachine = GenerateMoveStateMachine();
        foreach (MonsterState state in stateMachine.States.Values)
        {
            if (state is MoveState move)
            {
                foreach (AbstractIntent intent in move.Intents)
                {
                    yield return intent;
                }
            }
        }
    }

}

using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.PunishingBird;

public sealed class PunishingBird : LorMonsterModel, ITargetedMonsterAttackProvider
{
    internal const string TextureRoot = "res://images/monsters/punishing_bird/";
    internal const string IdleTexturePath = TextureRoot + "idle.png";
    internal const string PeckTexturePath = TextureRoot + "peck.png";
    internal const string PunishTexturePath = TextureRoot + "punish.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";
    internal const string BranchTexturePath = TextureRoot + "branch.png";
    internal const string CagePartsRoot = TextureRoot + "cage_parts/";
    internal const string CageBodyAtlasTexturePath = CagePartsRoot + "cage_body_atlas.png";
    internal const string ChainBodyAtlasTexturePath = CagePartsRoot + "chain_body_atlas.png";
    internal const string ChainOneAtlasTexturePath = CagePartsRoot + "chain_1_atlas.png";
    internal const string ChainTwoAtlasTexturePath = CagePartsRoot + "chain_2_atlas.png";
    internal const string ChainThreeAtlasTexturePath = CagePartsRoot + "chain_3_atlas.png";
    internal const string ChainShardOneTexturePath = CagePartsRoot + "chain_shard_1.png";
    internal const string ChainShardTwoTexturePath = CagePartsRoot + "chain_shard_2.png";

    internal const string SfxRoot = "res://audio/sfx/punishing_bird/";
    internal const string PeckSfxPath = SfxRoot + "peck.ogg";
    internal const string PunishSfxPath = SfxRoot + "punish.ogg";
    internal const string ChainBreakSfxPath = SfxRoot + "chain_break.ogg";
    internal const string CageDestroySfxPath = SfxRoot + "cage_destroy.ogg";

    internal const string PeckOneThenTwoMoveId = "PUNISHING_BIRD_PECK_ONE_THEN_TWO";
    internal const string PeckTwoThenOneMoveId = "PUNISHING_BIRD_PECK_TWO_THEN_ONE";
    internal const string PeckOneThenPunishMoveId = "PUNISHING_BIRD_PECK_ONE_THEN_PUNISH";
    internal const string PeckTwoThenPunishMoveId = "PUNISHING_BIRD_PECK_TWO_THEN_PUNISH";
    private const string RouterStateId = "PUNISHING_BIRD_ROUTER";

    public const int PeckHits = 3;
    public const int PeckVulnerable = 1;
    public const int PeckTwoStrength = 1;

    private static readonly string PageRelicTitleLocKey = $"{ModelDb.GetId<PunishingBirdPageRelic>().Entry}.title";
    private Dictionary<string, MoveState> _statesById = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesById = [];
    }

    public override int MinInitialHp => 900;

    public override int MaxInitialHp => 900;

    public override int DefaultChaoResistance => 600;

    public override LibraryCreatureResistanceData.Resistance DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override LibraryCreatureResistanceData.Resistance DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist
    };

    public override IEnumerable<string> AssetPaths =>
        PunishingBirdCreatureVisuals
            .Profile.AssetPaths
        .Concat(base.AssetPaths.Skip(1))
        .Concat(
        [
        PeckSfxPath,
        PunishSfxPath,
        ChainBreakSfxPath,
        CageDestroySfxPath,
        "res://images/powers/punishing_bird_no_bad_power.png",
        "res://images/powers/punishing_bird_punish_power.png",
        "res://images/powers/punishing_bird_cage_chains.png",
        "res://images/powers/punishing_bird_cage_chains_power.png"
        ])
        .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PowerCmdCompat.Apply<PunishingBirdNoBadPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<PunishingBirdPunishPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<PunishingBirdCageChainsPower>(
            Creature,
            PunishingBirdCageChainsPower.InitialChains,
            Creature,
            null,
            silent: true);
    }

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (Creature.CombatState == null)
        {
            return;
        }

        foreach (Player player in Creature.CombatState.Players.Where(static player => player.Creature.IsAlive))
        {
            await CardPileCmdCompat.AddToCombatAndPreview<ForestKeeperLockStatusCard>(
                player.Creature,
                PileType.Draw,
                2,
                addedByPlayer: false,
                CardPilePosition.Top);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesById.Clear();

        MoveState peckOneThenTwo = Register(new MoveState(
            PeckOneThenTwoMoveId,
            _ => PerformTurn(PeckOne, PeckTwo),
            CreateTargetedPeckOneIntent(),
            CreatePeckTwoIntent()));
        MoveState peckTwoThenOne = Register(new MoveState(
            PeckTwoThenOneMoveId,
            _ => PerformTurn(PeckTwo, PeckOne),
            CreateTargetedPeckTwoIntent(),
            CreatePeckOneIntent()));
        MoveState peckOneThenPunish = Register(new MoveState(
            PeckOneThenPunishMoveId,
            _ => PerformTurn(PeckOne, PunishMove),
            CreateTargetedPeckOneIntent(),
            new TargetedMonsterAttackIntent(GetPunishDamage, null, "PUNISHING_BIRD_PUNISH.description")));
        MoveState peckTwoThenPunish = Register(new MoveState(
            PeckTwoThenPunishMoveId,
            _ => PerformTurn(PeckTwo, PunishMove),
            CreateTargetedPeckTwoIntent(),
            new TargetedMonsterAttackIntent(GetPunishDamage, null, "PUNISHING_BIRD_PUNISH.description")));
        MoveState stunned = Register(new MoveState(
            stunnedMoveId,
            static _ => Task.CompletedTask,
            new StunIntent()));
        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) => ResolveNextMove(rng));

        peckOneThenTwo.FollowUpState = router;
        peckTwoThenOne.FollowUpState = router;
        peckOneThenPunish.FollowUpState = router;
        peckTwoThenPunish.FollowUpState = router;
        stunned.FollowUpState = router;
        return new MonsterMoveStateMachine(
            [peckOneThenTwo, peckTwoThenOne, peckOneThenPunish, peckTwoThenPunish, stunned, router],
            router);
    }

    internal async Task TryBreakCageChain(PlayerChoiceContext choiceContext, CardModel cardSource)
    {
        if (Creature.IsDead
            || NextMove.Id is not (PeckOneThenPunishMoveId or PeckTwoThenPunishMoveId)
            || Creature.GetPower<PunishingBirdCageChainsPower>() is not { } chains
            || chains.Amount <= 0)
        {
            return;
        }

        int segmentIndex = PunishingBirdCageChainsPower.InitialChains - chains.Amount;
        bool finalChain = chains.Amount <= 1;
        LocalOggOneShotPlayer.Play(ChainBreakSfxPath, -2f);
        await PunishingBirdCreatureVisuals.PlayChainBreak(Creature, segmentIndex);

        if (Creature.GetPower<PunishingBirdPunishPower>() is { } punishPower)
        {
            punishPower.ClearPunishTrigger();
        }

        await PowerCmd.Decrement(chains);
        if (finalChain)
        {
            AddPageRewards(Creature);
            LocalOggOneShotPlayer.Play(CageDestroySfxPath, -2f);
            await PunishingBirdCreatureVisuals.PlayCageDrop(Creature);
            await EndEncounterAsCageVictory();
            return;
        }

        if (_statesById.TryGetValue(stunnedMoveId, out MoveState? stunned))
        {
            SetMoveImmediate(stunned, forceTransition: true);
        }
    }

    private async Task PerformTurn(Func<IReadOnlyList<Creature>, Task> first, Func<IReadOnlyList<Creature>, Task> second)
    {
        IReadOnlyList<Creature> keepers = PunishingBirdEncounterHelper.LivingKeepers(Creature.CombatState);
        IReadOnlyList<Creature> firstTargets = keepers.Count > 0
            ? [keepers[0]]
            : PunishingBirdEncounterHelper.LivingPlayers(Creature.CombatState);
        await first(firstTargets);
        await second(PunishingBirdEncounterHelper.LivingPlayers(Creature.CombatState));
    }

    private async Task PeckOne(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> livingTargets = targets.Where(static target => target.IsAlive).ToArray();
        LocalOggOneShotPlayer.Play(PeckSfxPath, -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, livingTargets))
        {
            await DamageCmd.Attack(GetPeckDamage())
                .FromMonster(this)
                .WithHitCount(PeckHits)
                .WithAttackerAnim("Peck", 0.42f)
                .Execute(null);
        }

        foreach (Creature target in livingTargets.Where(static target => target.IsAlive))
        {
            await PowerCmdCompat.ApplyDebuff<VulnerablePower>(target, PeckVulnerable, Creature, null);
        }
    }

    private async Task PeckTwo(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> livingTargets = targets.Where(static target => target.IsAlive).ToArray();
        LocalOggOneShotPlayer.Play(PeckSfxPath, -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, livingTargets))
        {
            await DamageCmd.Attack(GetPeckTwoDamage())
                .FromMonster(this)
                .WithAttackerAnim("Peck", 0.42f)
                .Execute(null);
        }

        await PowerCmdCompat.Apply<StrengthPower>(Creature, PeckTwoStrength, Creature, null);
    }

    private async Task PunishMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = PunishingBirdEncounterHelper.LivingKeepers(Creature.CombatState).FirstOrDefault()
            ?? PunishingBirdEncounterHelper.LivingPlayers(Creature.CombatState).FirstOrDefault();
        if (target == null)
        {
            return;
        }
        LocalOggOneShotPlayer.Play(PunishSfxPath, -2f);
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, [target]))
        {
            await DamageCmd.Attack(GetPunishDamage())
                .FromMonster(this)
                .WithAttackerAnim("Punish", 0.72f)
                .Execute(null);
        }
    }

    private string ResolveNextMove(Rng rng)
    {
        if (Creature.GetPower<PunishingBirdPunishPower>() is { } punishPower
            && punishPower.TryConsumePendingPunish())
        {
            return rng.NextBool() ? PeckOneThenPunishMoveId : PeckTwoThenPunishMoveId;
        }

        return rng.NextBool() ? PeckOneThenTwoMoveId : PeckTwoThenOneMoveId;
    }

    private int GetPeckDamage() =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 10, 8);

    private int GetPeckTwoDamage() =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 29, 23);

    private int GetPunishDamage() =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 499, 299);

    public bool UsesTargetedAttackContract(Creature owner) => true;

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        IReadOnlyList<Creature> keepers = PunishingBirdEncounterHelper.LivingKeepers(owner.CombatState);
        return keepers.Count > 0
            ? [keepers[0]]
            : PunishingBirdEncounterHelper.LivingPlayers(owner.CombatState);
    }

    public string GetTargetedAttackTargetName(Creature owner) =>
        GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "Unknown Target";

    private CombinedAttackDebuffIntent CreatePeckOneIntent() => new(
        () => GetPeckDamage(),
        () => PeckHits,
        "PUNISHING_BIRD_PECK_ONE.description",
        IntentBadge.Vulnerable(PeckVulnerable).WithoutVisual());

    private CombinedTargetedAttackDebuffIntent CreateTargetedPeckOneIntent() => new(
        () => GetPeckDamage(),
        () => PeckHits,
        "PUNISHING_BIRD_PECK_ONE.description",
        null,
        false,
        IntentBadge.Vulnerable(PeckVulnerable).WithoutVisual());

    private CombinedAttackBuffIntent CreatePeckTwoIntent() => new(
        () => GetPeckTwoDamage(),
        () => 1,
        "PUNISHING_BIRD_PECK_TWO.description",
        IntentBadge.FromPower<StrengthPower>(() => PeckTwoStrength));

    private CombinedTargetedAttackBuffIntent CreateTargetedPeckTwoIntent() => new(
        () => GetPeckTwoDamage(),
        () => 1,
        "PUNISHING_BIRD_PECK_TWO.description",
        null,
        false,
        IntentBadge.FromPower<StrengthPower>(() => PeckTwoStrength));

    private MoveState Register(MoveState state)
    {
        _statesById[state.Id] = state;
        return state;
    }

    private async Task EndEncounterAsCageVictory()
    {
        if (Creature.CombatState == null || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        foreach (Creature enemy in Creature.CombatState.Enemies.ToArray())
        {
            if (enemy.IsAlive)
            {
                await CreatureCmd.Kill(enemy, force: true);
            }
        }

        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }
    }

    private static void AddPageRewards(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !PunishingBirdEncounterHelper.IsEncounter(deadCreature.CombatState))
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (AbnormalityPageRewardHelper.ShouldAddPageReward<PunishingBirdPageRelic>(
                    room,
                    player,
                    PageRelicTitleLocKey))
            {
                room.AddExtraReward(
                    player,
                    new RelicReward(ModelDb.Relic<PunishingBirdPageRelic>().ToMutable(), player));
            }
        }
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Creature)
        {
            AddPageRewards(creature);
        }

        return Task.CompletedTask;
    }

}

public abstract class ForestKeeperBirdBase : LorMonsterModel
{
    internal const string TextureRoot = "res://images/monsters/forest_keeper_bird/";
    internal const string IdleTexturePath = TextureRoot + "idle.png";
    internal const string HitTexturePath = TextureRoot + "hit.png";
    internal const string ThrustTexturePath = TextureRoot + "thrust.png";
    internal const string SlashTexturePath = TextureRoot + "slash.png";

    internal const string BounceMoveId = "FOREST_KEEPER_BOUNCE";
    internal const string ChimeMoveId = "FOREST_KEEPER_CHIME";
    internal const string SmashMoveId = "FOREST_KEEPER_SMASH";
    private const string RouterStateId = "FOREST_KEEPER_ROUTER";

    public const int BounceBlock = 16;
    public const int ChimeStrength = 20;
    public const int SmashHits = 3;

    private int _cycleIndex;

    protected abstract int InitialCycleIndex { get; }

    public override int MinInitialHp => 150;

    public override int MaxInitialHp => 150;

    public override int DefaultChaoResistance => 120;

    public override LibraryCreatureResistanceData.Resistance DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Endure
    };

    public override LibraryCreatureResistanceData.Resistance DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override IEnumerable<string> AssetPaths =>
        ForestKeeperBirdCreatureVisuals
            .Profile.AssetPaths
        .Concat(
        [
        "res://images/powers/forest_keeper_stolen_chains.png",
        "res://images/powers/forest_keeper_stolen_chains_power.png"
        ]);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _cycleIndex = InitialCycleIndex;
        await PowerCmdCompat.Apply<MinionPower>(Creature, 1, Creature, null, silent: true);
        await PowerCmdCompat.Apply<ForestKeeperStolenChainsPower>(Creature, 1, Creature, null, silent: true);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState bounce = new(
            BounceMoveId,
            BounceMove,
            new CombinedAttackDefendIntent(() => GetBounceDamage(), () => 1));
        MoveState chime = new(
            ChimeMoveId,
            ChimeMove,
            new BuffIntent(),
            new DebuffIntent());
        MoveState smash = new(
            SmashMoveId,
            SmashMove,
            new DynamicAttackIntent(() => GetSmashDamage(), () => SmashHits));
        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, _) => ResolveNextMove());

        bounce.FollowUpState = router;
        chime.FollowUpState = router;
        smash.FollowUpState = router;
        return new MonsterMoveStateMachine([bounce, chime, smash, router], router);
    }

    private string ResolveNextMove()
    {
        var result = (_cycleIndex % 3) switch
        {
            0 => BounceMoveId,
            1 => ChimeMoveId,
            _ => SmashMoveId
        };
        _cycleIndex++;
        return result;
    }

    private async Task BounceMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = PunishingBirdEncounterHelper.FindPunishingBird(Creature.CombatState);
        if (target == null)
        {
            return;
        }
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, [target]))
        {
            await DamageCmd.Attack(GetBounceDamage())
                .FromMonster(this)
                .WithAttackerAnim("Thrust", 0.36f)
                .Execute(null);
        }
        await CreatureCmd.GainBlock(Creature, BounceBlock, ValueProp.Move, null);
    }

    private async Task ChimeMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmdCompat.Apply<StrengthPower>(Creature, ChimeStrength, Creature, null);
        Creature? target = PunishingBirdEncounterHelper.FindPunishingBird(
            Creature.CombatState);
        if (target != null)
        {
            await PowerCmdCompat.Apply<VulnerablePower>(
                target,
                2,
                Creature,
                null);
        }
    }

    private async Task SmashMove(IReadOnlyList<Creature> targets)
    {
        Creature? target = PunishingBirdEncounterHelper.FindPunishingBird(Creature.CombatState);
        if (target == null)
        {
            return;
        }
        using (TargetedMonsterAttackHelper.ForceTargets(Creature, [target]))
        {
            await DamageCmd.Attack(GetSmashDamage())
                .FromMonster(this)
                .WithHitCount(SmashHits)
                .WithAttackerAnim("Slash", 0.42f)
                .Execute(null);
        }
    }

    private int GetBounceDamage() =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 16, 20);

    private int GetSmashDamage() =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 6, 8);

}

public sealed class ForestKeeperBirdLeft : ForestKeeperBirdBase
{
    protected override int InitialCycleIndex => 0;
}

public sealed class ForestKeeperBirdRight : ForestKeeperBirdBase
{
    protected override int InitialCycleIndex => 1;
}

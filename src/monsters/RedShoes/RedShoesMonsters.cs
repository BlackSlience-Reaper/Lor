using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.RedShoes;
using LibraryOfRuina.relics;
using LibraryOfRuina.relics.RedShoes;
using LibraryOfRuina.visuals;
using LibraryOfRuina.visuals.RedShoes;
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

namespace LibraryOfRuina.monsters.RedShoes;

public sealed class RedShoesLeft : CounterIntentMonsterModel
{
    private const string BloodThirstMoveId = "BLOOD_THIRST";
    private const string DesireMoveId = "DESIRE";

    public const string IdleTexturePath = "res://images/monsters/red_shoes/left_shoe.png";
    public const string AttackTexturePath = "res://images/monsters/red_shoes/left_shoe_attack.png";
    public const string HitTexturePath = "res://images/monsters/red_shoes/left_shoe_hit.png";
    public const string ParryTexturePath = "res://images/monsters/red_shoes/left_shoe_parry.png";

    private int BloodThirstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 12, 10);

    private const int DesireBlock = 10;
    private const int DesireRightShoeStrength = 2;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 88, 81);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 90, 87);

    public override int DefaultChaoResistance => 70;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Fatal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                RedShoesLeftCreatureVisuals.Profile.AssetPaths.Count + 4);
            paths.AddRange(
                RedShoesLeftCreatureVisuals.Profile.AssetPaths);
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
        EncounterBgmController.RegisterMonster(Creature);
    }

    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
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

        AddRedShoesPageRewardsFromDeathHook(creature);
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bloodThirst = new MoveState(
            BloodThirstMoveId,
            BloodThirstMove,
            new BadgedAttackIntent(
                BloodThirstDamage,
                "RED_SHOES_LEFT.blood_thirst.description",
                IntentBadge.Bleed(2)));

        var desire = new MoveState(
            DesireMoveId,
            DesireMove,
            new DefendIntent(),
            new DetailedBuffIntent<LibraryOfRuinaNextTurnStrength>(
                DesireRightShoeStrength,
                DetailedBuffTargetScope.AllEnemies));

        bloodThirst.FollowUpState = desire;
        desire.FollowUpState = bloodThirst;

        List<MonsterState> states = [bloodThirst, desire];
        return new MonsterMoveStateMachine(states, bloodThirst);
    }

    private async Task BloodThirstMove(IReadOnlyList<Creature> targets)
    {
        AttackCommand attack = await DamageCmd.Attack(BloodThirstDamage)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 1.47f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        IReadOnlyList<Creature> bleedTargets = AttackCommandCompat.Results(attack)
            .Where(static result => result.Receiver.IsPlayer && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();

        if (bleedTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(bleedTargets, 2m, Creature, null);
        }
    }

    private async Task DesireMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 1.2f);
        await CreatureCmd.GainBlock(Creature, DesireBlock, ValueProp.Move, null);

        IReadOnlyList<Creature>? enemies = Creature.CombatState?.Enemies;
        if (enemies != null && enemies.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(enemies, DesireRightShoeStrength, Creature, null);
        }
    }

    private Creature? FindRightShoe()
    {
        return Creature.CombatState?.Creatures
            .FirstOrDefault(c => c.IsAlive && c.Monster is RedShoesRight);
    }

    private bool IsRedShoesStrongEncounter(CombatRoom room)
    {
        return room.Encounter.MonstersWithSlots.Any(pair => pair.Item1 is RedShoesLeft or RedShoesRight);
    }

    private static readonly string RedShoesPageRelicTitleLocKey =
        $"{ModelDb.GetId<RedShoesPageRelic>().Entry}.title";

    private void AddRedShoesPageRewardsFromDeathHook(Creature deadCreature)
    {
        if (deadCreature.CombatState?.RunState.CurrentRoom is not CombatRoom room
            || !IsRedShoesStrongEncounter(room))
        {
            return;
        }

        foreach (Player player in room.CombatState.Players)
        {
            if (!AbnormalityPageRewardHelper.ShouldAddPageReward<RedShoesPageRelic>(
                room,
                player,
                RedShoesPageRelicTitleLocKey))
            {
                continue;
            }

            room.AddExtraReward(player, new RelicReward(ModelDb.Relic<RedShoesPageRelic>().ToMutable(), player));
        }
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
}

public sealed class RedShoesRight : CounterIntentMonsterModel, ITargetedMonsterAttackProvider
{
    private const string DesireBurstMoveId = "DESIRE_BURST";
    private const string ObsessionMoveId = "OBSESSION";

    public const string IdleTexturePath = "res://images/monsters/red_shoes/right_shoe.png";
    public const string AttackTexturePath = "res://images/monsters/red_shoes/right_shoe_attack.png";
    public const string HitTexturePath = "res://images/monsters/red_shoes/right_shoe_hit.png";

    private MoveState _desireBurstState = null!;
    private MoveState _obsessionState = null!;
    private IReadOnlyList<Creature> _cachedTargets = Array.Empty<Creature>();
    private int _cachedTargetRound = -1;
    private string? _cachedTargetMoveId;

    private int DesireBurstDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

    private int ObsessionBaseDamage =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 7, 5);

    public const int DesireBurstHits = 3;
    public const int BleedBonusDamage = 4;
    public const int BleedHealMultiplier = 2;
    public const int SelfStunTurns = 1;
    public const int ObsessionStrengthGain = 2;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 54, 50);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 57, 53);

    public override int DefaultChaoResistance => 40;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Vulnerable
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Endure,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Fatal
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            var paths = new List<string>(
                RedShoesRightCreatureVisuals.Profile.AssetPaths.Count + 6);
            paths.AddRange(
                RedShoesRightCreatureVisuals.Profile.AssetPaths);
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
        EncounterBgmController.RegisterMonster(Creature);

        await PowerCmdCompat.Apply<LibraryOfRuinaRedShoesBloodAttractionPower>(
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

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _desireBurstState = new MoveState(
            DesireBurstMoveId,
            DesireBurstMove,
            new LocalPreviewAttackIntent(
                (owner, _) => owner.Monster is RedShoesRight right ? right.DesireBurstDamage : 0,
                () => 3,
                "RED_SHOES_RIGHT.desire_burst.description"),
            new HealIntent());

        _obsessionState = new MoveState(
            ObsessionMoveId,
            ObsessionMove,
            new LocalPreviewAttackIntent(
                (owner, previewTarget) => owner.Monster is RedShoesRight right
                    ? right.ObsessionDamageForPreview(previewTarget)
                    : 0,
                () => 1,
                "RED_SHOES_RIGHT.obsession.description"),
            new DetailedBuffIntent<StrengthPower>(ObsessionStrengthGain));

        var stunned = new MoveState(
            stunnedMoveId,
            StunnedMove,
            new StunIntent());

        _desireBurstState.FollowUpState = _obsessionState;
        _obsessionState.FollowUpState = _desireBurstState;
        stunned.FollowUpState = _obsessionState;

        List<MonsterState> states = [_desireBurstState, _obsessionState, stunned];
        return new MonsterMoveStateMachine(states, _desireBurstState);
    }

    private async Task DesireBurstMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> selected = GetCachedOrSelectedTargets();
        if (selected.Count == 0)
        {
            return;
        }

        bool noOneHadBleedAtMoveStart = !selected.Any(static t => GetBleedStacks(t) > 0);

        for (int i = 0; i < DesireBurstHits; i++)
        {
            Creature[] alive = selected.Where(static t => t.IsAlive).ToArray();
            if (alive.Length == 0)
            {
                break;
            }

            var preBleedByTarget = alive.ToDictionary(static t => t, static t => GetBleedStacks(t));

            using var lungeScope = new TargetedAttackLungeScope(this, alive);
            using var forcedTargetScope = TargetedMonsterAttackHelper.ForceTargets(Creature, alive);
            AttackCommand attack = await DamageCmd.Attack(DesireBurstDamage)
                .FromMonster(this)
                .WithAttackerAnim("Attack", 1.47f)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);

            int healThisSegment = 0;
            foreach (Creature t in alive)
            {
                if (preBleedByTarget.GetValueOrDefault(t, 0) <= 0)
                {
                    continue;
                }

                if (!AttackCommandCompat.Results(attack).Any(result => result.Receiver == t && result.TotalDamage > 0))
                {
                    continue;
                }

                healThisSegment += preBleedByTarget[t] * BleedHealMultiplier;
            }

            if (healThisSegment > 0)
            {
                await CreatureCmd.Heal(Creature, MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(Creature, healThisSegment));
            }
        }

        if (noOneHadBleedAtMoveStart)
        {
            await CreatureCmd.Stun(Creature, ObsessionMoveId);
        }
    }

    private async Task ObsessionMove(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<Creature> selected = GetCachedOrSelectedTargets();
        Creature[] alive = selected.Where(static t => t.IsAlive).ToArray();
        if (alive.Length == 0)
        {
            return;
        }

        bool anyBleeds = alive.Any(static t => GetBleedStacks(t) > 0);
        int dmg = ObsessionBaseDamage + (anyBleeds ? BleedBonusDamage : 0);

        using var lungeScope = new TargetedAttackLungeScope(this, alive);
        using var forcedTargetScope = TargetedMonsterAttackHelper.ForceTargets(Creature, alive);
        await DamageCmd.Attack(dmg)
            .FromMonster(this)
            .WithAttackerAnim("Attack", 1.47f)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(null);

        await PowerCmdCompat.Apply<StrengthPower>(Creature, ObsessionStrengthGain, Creature, null);
    }

    private Task StunnedMove(IReadOnlyList<Creature> targets)
    {
        return Task.CompletedTask;
    }

    private static int GetBleedStacks(Creature creature)
    {
        LibraryBleedingPower? bleed = creature.Powers.OfType<LibraryBleedingPower>().FirstOrDefault();
        return bleed != null ? bleed.Amount : 0;
    }

    private int ObsessionDamageForPreview(Creature? previewTarget)
    {
        IReadOnlyList<Creature> selected = SelectTargetsForCurrentIntent();
        if (previewTarget is not { IsAlive: true } || selected.Count == 0)
        {
            return 0;
        }

        if (!selected.Contains(previewTarget))
        {
            return 0;
        }

        bool anyBleeds = selected.Any(static t => GetBleedStacks(t) > 0);
        return ObsessionBaseDamage + (anyBleeds ? BleedBonusDamage : 0);
    }

    public void CacheTargetForCurrentIntent()
    {
        UpdateCachedTarget();
    }

    private IReadOnlyList<Creature> GetCachedOrSelectedTargets()
    {
        UpdateCachedTarget();
        return _cachedTargets;
    }

    private void UpdateCachedTarget()
    {
        _cachedTargets = SelectTargetsForCurrentIntent();
        _cachedTargetRound = Creature.CombatState?.RoundNumber ?? -1;
        _cachedTargetMoveId = NextMove.Id;
    }

    
    
    
    private IReadOnlyList<Creature> SelectTargetsForCurrentIntent()
    {
        IReadOnlyList<Creature> alivePlayers = Creature.CombatState?.Players
            .Select(static player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .OrderBy(static creature => creature.Player?.NetId ?? 0UL)
            .ToArray()
            ?? Array.Empty<Creature>();

        if (alivePlayers.Count == 0)
        {
            return Array.Empty<Creature>();
        }

        IReadOnlyList<Creature> bleedingPlayers = alivePlayers
            .Where(static creature => GetBleedStacks(creature) > 0)
            .ToArray();

        if (bleedingPlayers.Count == 0 || bleedingPlayers.Count == alivePlayers.Count)
        {
            return alivePlayers;
        }

        return bleedingPlayers;
    }

    
    public bool UsesTargetedAttackContract(Creature owner)
    {
        return true;
    }

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        IReadOnlyList<Creature> targets = GetCachedOrSelectedTargets();
        Creature[] alive = targets.Where(static t => t.IsAlive).ToArray();
        if (alive.Length > 0)
        {
            return alive;
        }

        Creature? fallback = owner.CombatState?.Players
            .Select(static p => p.Creature)
            .FirstOrDefault(static c => c.IsAlive);
        return fallback != null ? new[] { fallback } : Array.Empty<Creature>();
    }

    public string GetTargetedAttackTargetName(Creature owner)
    {
        return GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "Unknown Target";
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
}

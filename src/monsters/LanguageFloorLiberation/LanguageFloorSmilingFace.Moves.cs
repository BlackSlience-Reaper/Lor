using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.intents;
using LibraryOfRuina.visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

using BaseSmilingBodies =
    SmilingBodies.SmilingBodies;

public sealed partial class LanguageFloorSmilingFace
{
    public bool UsesTargetedAttackContract(Creature owner) =>
        NextMove.Intents.Any(static intent =>
            intent is ITargetedIntentIndicator);

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        Creature? target = Enumerable.Range(0, GetIntentCapacity(Form))
            .Where(slot => IsTargetedMove(GetPlannedMove(slot)))
            .Select(GetPlannedTarget)
            .FirstOrDefault(static candidate => candidate is { IsAlive: true });
        return target is { IsAlive: true } ? [target] : [];
    }

    public string GetTargetedAttackTargetName(Creature owner) =>
        GetTargetedAttackTargets(owner).FirstOrDefault()?.Name
        ?? "Unknown Target";

    private async Task PerformCompositeMove(IReadOnlyList<Creature> targets)
    {
        int capacity = GetIntentCapacity(Form);
        for (int slot = 0; slot < capacity && Creature.IsAlive; slot++)
        {
            await PerformPlannedMove(GetPlannedMove(slot), slot);
            if (Creature.CombatState?.Encounter
                is LanguageFloorLiberationEncounter { PhaseComplete: true })
            {
                return;
            }
        }
    }

    private Task PerformPlannedMove(
        LanguageFloorSmilingFaceMove move,
        int slot) => move switch
    {
        LanguageFloorSmilingFaceMove.Devour => DevourMove([], slot),
        LanguageFloorSmilingFaceMove.Absorb => AbsorbMove([], slot),
        LanguageFloorSmilingFaceMove.Sit => SitMove([], slot),
        LanguageFloorSmilingFaceMove.Scream => ScreamMove([]),
        _ => VomitMove([])
    };

    private async Task DevourMove(IReadOnlyList<Creature> targets)
    {
        await DevourMove(targets, plannedSlot: null);
    }

    private async Task DevourMove(
        IReadOnlyList<Creature> targets,
        int? plannedSlot)
    {
        LanguageFloorSmilingFaceForm startingForm = Form;
        Creature? target = ResolveMoveTarget(plannedSlot);
        if (target == null)
        {
            return;
        }

        using var lunge = new TargetedAttackLungeScope(this, [target]);
        for (int hit = 0;
             hit < DevourHits
             && Creature.IsAlive
             && target.IsAlive;
             hit++)
        {
            LocalOggOneShotPlayer.Play(BaseSmilingBodies.AbsorbHitSfxPath, -2f);
            await CreatureCmd.TriggerAnim(
                Creature,
                GetNormalAttackAnimation(hit),
                SegmentDelaySeconds);
            await CreatureCmdCompat.Damage(
                new ThrowingPlayerChoiceContext(),
                target,
                GetMoveDamage(LanguageFloorSmilingFaceMove.Devour),
                ValueProp.Move,
                Creature,
                null);

            if (Creature.IsAlive)
            {
                int heal = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        Creature.MaxHp * DevourHealPercentPerHit / 100m));
                await CreatureCmd.Heal(Creature, heal);
            }

            if (Form != startingForm)
            {
                return;
            }
        }
    }

    private async Task AbsorbMove(IReadOnlyList<Creature> targets)
    {
        await AbsorbMove(targets, plannedSlot: null);
    }

    private async Task AbsorbMove(
        IReadOnlyList<Creature> targets,
        int? plannedSlot)
    {
        Creature? target = ResolveMoveTarget(plannedSlot);
        if (target == null)
        {
            return;
        }

        await ExecuteTargetedHits(target, GetMoveDamage(
            LanguageFloorSmilingFaceMove.Absorb), 1);
        if (Creature.IsAlive)
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(
                Creature,
                AbsorbNextTurnStrength,
                Creature,
                null);
        }
    }

    private async Task SitMove(IReadOnlyList<Creature> targets)
    {
        await SitMove(targets, plannedSlot: null);
    }

    private async Task SitMove(
        IReadOnlyList<Creature> targets,
        int? plannedSlot)
    {
        LocalOggOneShotPlayer.Play(BaseSmilingBodies.Phase3SitHitSfxPath, -2f);
        await ExecuteIndiscriminateAttack(
            GetMoveDamage(LanguageFloorSmilingFaceMove.Sit),
            SitHits,
            "Attack",
            "vfx/vfx_attack_blunt");
        foreach (Creature target in GetLivingIndiscriminateTargets())
        {
            await PowerCmdCompat.ApplyDebuff<FrailPower>(
                target,
                SitVulnerable,
                Creature,
                null);
        }
    }

    private async Task ExecuteIndiscriminateAttack(
        int damage,
        int hits,
        string animation,
        string hitFx)
    {
        for (int hit = 0; hit < hits && Creature.IsAlive; hit++)
        {
            IReadOnlyList<Creature> targets =
                GetLivingIndiscriminateTargets();
            if (targets.Count == 0)
            {
                return;
            }

            await IndiscriminateAttackExecutor.Execute(
                this,
                damage,
                targets,
                attack => attack
                    .WithAttackerAnim(animation, SegmentDelaySeconds)
                    .WithHitFx(hitFx),
                new ThrowingPlayerChoiceContext());
        }
    }

    private async Task ExecuteTargetedHits(
        Creature target,
        int damage,
        int hits)
    {
        using var lunge = new TargetedAttackLungeScope(this, [target]);
        for (int hit = 0;
             hit < hits && Creature.IsAlive && target.IsAlive;
             hit++)
        {
            LocalOggOneShotPlayer.Play(BaseSmilingBodies.AbsorbHitSfxPath, -2f);
            await CreatureCmd.TriggerAnim(
                Creature,
                GetNormalAttackAnimation(hit),
                SegmentDelaySeconds);
            await CreatureCmdCompat.Damage(
                new ThrowingPlayerChoiceContext(),
                target,
                damage,
                ValueProp.Move,
                Creature,
                null);
        }
    }

    private async Task ScreamMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(BaseSmilingBodies.Phase2ScreamSfxPath, -2f);
        await ExecuteIndiscriminateAttack(
            GetMoveDamage(LanguageFloorSmilingFaceMove.Scream),
            ScreamHits,
            "Scream",
            "vfx/vfx_attack_blunt");
    }

    private async Task VomitMove(IReadOnlyList<Creature> targets)
    {
        LocalOggOneShotPlayer.Play(BaseSmilingBodies.Phase3VomitSfxPath, -2f);
        await ExecuteIndiscriminateAttack(
            GetMoveDamage(LanguageFloorSmilingFaceMove.Vomit),
            1,
            "Vomit",
            "vfx/vfx_bloody_impact");

        foreach (Creature target in GetLivingIndiscriminateTargets())
        {
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                target,
                VomitDebuffAmount,
                VomitDebuffTurns,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                target,
                VomitDebuffAmount,
                VomitDebuffTurns,
                Creature,
                null);
            await LibraryPowerCmd.Apply<LibraryDisarmPower>(
                target,
                VomitDebuffAmount,
                VomitDebuffTurns,
                Creature,
            null);
        }
    }

    private static string GetNormalAttackAnimation(int hitIndex) =>
        hitIndex % 2 == 0 ? "AttackThrust" : "AttackSlash";

    private Creature? ResolveMoveTarget(int? plannedSlot)
    {
        if (plannedSlot is int slot)
        {
            Creature? planned = GetPlannedTarget(slot);
            if (planned != null)
            {
                return planned;
            }

            Creature? replacement = ChooseRandomTarget(RunRng.MonsterAi);
            SetPlannedTarget(slot, replacement);
            return replacement;
        }

        return ChooseRandomTarget(RunRng.MonsterAi);
    }

    private Creature? ChooseRandomTarget(Rng rng)
    {
        IReadOnlyList<Creature> candidates = GetLivingTargetCandidates();
        return candidates.Count == 0
            ? null
            : candidates[rng.NextInt(candidates.Count)];
    }

    private IReadOnlyList<Creature> GetLivingTargetCandidates()
    {
        if (Creature.CombatState is not { } combatState)
        {
            return [];
        }

        IEnumerable<Creature> players = combatState.PlayerCreatures
            .Where(static target => target.IsAlive)
            .OrderBy(static target => target.Player?.NetId ?? 0UL);
        IEnumerable<Creature> corpses = combatState.Enemies
            .Where(static target =>
                target.IsAlive
                && target.Monster is LanguageFloorMeltingCorpse)
            .OrderBy(static target => target.SlotName);
        return players.Concat(corpses).ToArray();
    }

    private IReadOnlyList<Creature> GetLivingPlayers() =>
        Creature.CombatState?.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray()
        ?? [];

    private IReadOnlyList<Creature> GetLivingIndiscriminateTargets() =>
        LanguageFloorLiberationCombatHelper
            .GetLivingPlayersAndCorpses(Creature);

    internal static int GetMoveDamage(LanguageFloorSmilingFaceMove move) =>
        move switch
        {
            LanguageFloorSmilingFaceMove.Devour =>
                AscensionHelper.GetValueIfAscension(
                    AscensionLevel.DeadlyEnemies,
                    5,
                    3),
            LanguageFloorSmilingFaceMove.Absorb =>
                AscensionHelper.GetValueIfAscension(
                    AscensionLevel.DeadlyEnemies,
                    13,
                    11),
            LanguageFloorSmilingFaceMove.Sit =>
                AscensionHelper.GetValueIfAscension(
                    AscensionLevel.DeadlyEnemies,
                    4,
                    3),
            LanguageFloorSmilingFaceMove.Scream =>
                AscensionHelper.GetValueIfAscension(
                    AscensionLevel.DeadlyEnemies,
                    6,
                    5),
            _ => AscensionHelper.GetValueIfAscension(
                AscensionLevel.DeadlyEnemies,
                11,
                10)
        };

    private static string MoveId(LanguageFloorSmilingFaceMove move) =>
        move switch
        {
            LanguageFloorSmilingFaceMove.Devour => DevourMoveId,
            LanguageFloorSmilingFaceMove.Absorb => AbsorbMoveId,
            LanguageFloorSmilingFaceMove.Sit => SitMoveId,
            LanguageFloorSmilingFaceMove.Scream => ScreamMoveId,
            _ => VomitMoveId
        };

    private MoveState GetMoveState(string id) => id switch
    {
        DevourMoveId => _devourState!,
        AbsorbMoveId => _absorbState!,
        SitMoveId => _sitState!,
        ScreamMoveId => _screamState!,
        VomitMoveId => _vomitState!,
        _ => _devourState!
    };

    private static IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        foreach (LanguageFloorSmilingFaceMove move
                 in Enum.GetValues<LanguageFloorSmilingFaceMove>())
        {
            yield return CreateIntent(move);
        }
    }

    private static AbstractIntent CreateIntent(
        LanguageFloorSmilingFaceMove move,
        Func<Creature, Creature?>? targetResolver = null) => move switch
    {
        LanguageFloorSmilingFaceMove.Devour =>
            new BadgedTargetedAttackIntent(
                () => GetMoveDamage(move),
                () => DevourHits,
                "LANGUAGE_FLOOR_SMILING_FACE_DEVOUR.description",
                "LANGUAGE_FLOOR_SMILING_FACE_DEVOUR_PLAYER.description",
                true,
                targetResolver,
                IntentBadge.Heal(DevourHealPercentPerHit)),
        LanguageFloorSmilingFaceMove.Absorb =>
            new CombinedTargetedAttackBuffIntent(
                () => GetMoveDamage(move),
                () => 1,
                "LANGUAGE_FLOOR_SMILING_FACE_ABSORB.description",
                "LANGUAGE_FLOOR_SMILING_FACE_ABSORB_PLAYER.description",
                true,
                targetResolver,
                IntentBadge.NextTurnStrength(AbsorbNextTurnStrength)),
        LanguageFloorSmilingFaceMove.Sit =>
            new IndiscriminateAttackIntent(
                () => GetMoveDamage(move),
                () => SitHits,
                "LANGUAGE_FLOOR_SMILING_FACE_SIT.description",
                LanguageFloorLiberationCombatHelper.GetLivingPlayersAndCorpses,
                IntentBadge.FromPower<FrailPower>(SitVulnerable)),
        LanguageFloorSmilingFaceMove.Scream =>
            new MultiAttackIntent(GetMoveDamage(move), ScreamHits),
        _ => new IndiscriminateAttackIntent(
            () => GetMoveDamage(move),
            () => 1,
            "LANGUAGE_FLOOR_SMILING_FACE_VOMIT.description",
            LanguageFloorLiberationCombatHelper.GetLivingPlayersAndCorpses,
            IntentBadge.RapidWear(VomitDebuffAmount, VomitDebuffTurns),
            IntentBadge.FromPower<LibraryWeakPower>(
                VomitDebuffAmount,
                 VomitDebuffTurns.ToString(),
                 VomitDebuffAmount.ToString()),
            IntentBadge.Flaw(VomitDebuffAmount, VomitDebuffTurns))
    };
}

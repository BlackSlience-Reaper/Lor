using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using LibraryOfRuina.powers.PhilosophyFloorLiberation;
using LibraryOfRuina.visuals.PhilosophyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using VoidCard = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace LibraryOfRuina.monsters.PhilosophyFloorLiberation;

public sealed partial class PhilosophyFloorTwilight
{
    internal const float AttackHitStopFastSeconds = 0.06f;
    internal const float AttackHitStopStandardSeconds = 0.12f;

    private const int SlamDownBlock = 13;
    private const int TalonHits = 2;
    private const int ProwlBlock = 12;
    private const int ProwlStrong = 3;
    private const int ProtectFrail = 2;
    private const int ProtectWeak = 2;
    private const int TornMouthHits = 3;
    private const int TornMouthBleedPerHit = 3;
    private const int TiltedScaleSin = 9;
    private const int ForestLightBind = 6;
    private const int ForestLightBindTurns = 2;
    private const int ForestLightConfusion = 1;
    private const int PunishmentHits = 4;
    private const int PunishmentHealPercentPerSin = 6;
    private const int BrilliantEyesVoidCount = 2;
    private const int SurveillanceStrength = 4;

    private static int SlamDownDamage => AscensionDamage(12, 13);

    private static int TalonDamage => AscensionDamage(5, 6);

    private static int ProtectBlackForestDamage => AscensionDamage(11, 12);

    private static int TornMouthDamage => AscensionDamage(4, 5);

    private static int TiltedScaleDamage => AscensionDamage(11, 12);

    private static int PunishmentDamage => AscensionDamage(4, 5);

    private static int PeaceForAllDamage => AscensionDamage(25, 26);

    private AbstractIntent CreateActionIntent(
        PhilosophyFloorTwilightAction action,
        int slot = 0) => action switch
        {
            PhilosophyFloorTwilightAction.SlamDown =>
                new CombinedAttackDefendIntent(
                    SlamDownDamage,
                    1,
                    "PHILOSOPHY_FLOOR_TWILIGHT_SLAM_DOWN.description",
                    blockAmount: SlamDownBlock),
            PhilosophyFloorTwilightAction.Talon =>
                new MultiAttackIntent(TalonDamage, TalonHits),
            PhilosophyFloorTwilightAction.Prowl =>
                new CombinedDefendBuffIntent(
                    ProwlBlock,
                    "PHILOSOPHY_FLOOR_TWILIGHT_PROWL.description",
                    IntentBadge.FromPower<LibraryStrongPower>(ProwlStrong)),
            PhilosophyFloorTwilightAction.ProtectBlackForest =>
                new CombinedAttackDebuffIntent(
                    () => ProtectBlackForestDamage,
                    () => 1,
                    "PHILOSOPHY_FLOOR_TWILIGHT_PROTECT_BLACK_FOREST.description",
                    IntentBadge.Frail(ProtectFrail),
                    IntentBadge.Weak(ProtectWeak)),
            PhilosophyFloorTwilightAction.TornMouth =>
                new CombinedAttackDebuffIntent(
                    () => TornMouthDamage,
                    () => TornMouthHits,
                    "PHILOSOPHY_FLOOR_TWILIGHT_TORN_MOUTH.description",
                    IntentBadge.Bleed(TornMouthBleedPerHit)),
            PhilosophyFloorTwilightAction.TiltedScale =>
                new CombinedAttackDebuffIntent(
                    () => TiltedScaleDamage,
                    () => 1,
                    "PHILOSOPHY_FLOOR_TWILIGHT_TILTED_SCALE.description",
                    IntentBadge.FromPower<PhilosophyFloorTwilightSinPower>(
                        TiltedScaleSin)),
            PhilosophyFloorTwilightAction.ForestLight =>
                new PhilosophyFloorTwilightTargetedDebuffIntent(
                    "PHILOSOPHY_FLOOR_TWILIGHT_FOREST_LIGHT.description",
                    owner => ResolvePlannedTargets(slot, owner),
                    new Dictionary<string, decimal>
                    {
                        ["Duration"] = ForestLightBindTurns
                    },
                    IntentBadge.Bind(ForestLightBind),
                    IntentBadge.Confusion(ForestLightConfusion)),
            PhilosophyFloorTwilightAction.Punishment =>
                new PhilosophyFloorTwilightPunishmentIntent(
                    () => PunishmentDamage,
                    () => PunishmentHits,
                    "PHILOSOPHY_FLOOR_TWILIGHT_PUNISHMENT.description",
                    PunishmentHealPercentPerSin),
            PhilosophyFloorTwilightAction.BrilliantEyes =>
                new TargetedDetailedStatusCardIntent<VoidCard>(
                    BrilliantEyesVoidCount,
                    PileType.Discard,
                    "PHILOSOPHY_FLOOR_TWILIGHT_BRILLIANT_EYES.description",
                    owner => ResolvePlannedTargets(slot, owner)),
            PhilosophyFloorTwilightAction.PeaceForAll =>
                new IndiscriminateAttackIntent(
                    PeaceForAllDamage,
                    1,
                    "PHILOSOPHY_FLOOR_TWILIGHT_PEACE_FOR_ALL.description"),
            PhilosophyFloorTwilightAction.Surveillance =>
                new PhilosophyFloorTwilightTargetedDebuffIntent(
                    "PHILOSOPHY_FLOOR_TWILIGHT_SURVEILLANCE.description",
                    owner => ResolvePlannedTargets(slot, owner),
                    new Dictionary<string, decimal>
                    {
                        ["StrengthAmount"] = SurveillanceStrength
                    },
                    IntentBadge
                        .FromPower<PhilosophyFloorTwilightFearPower>()
                        .WithoutVisual(),
                    IntentBadge.Strength(SurveillanceStrength)),
            PhilosophyFloorTwilightAction.Judgment =>
                new PhilosophyFloorTwilightJudgmentIntent(),
            _ => new DebuffIntent()
        };

    private async Task ExecuteAction(
        PhilosophyFloorTwilightAction action,
        IReadOnlyList<Creature> targets,
        int slot)
    {
        IReadOnlyList<Creature> plannedTargets = ResolvePlannedTargets(
            slot,
            Creature);
        switch (action)
        {
            case PhilosophyFloorTwilightAction.SlamDown:
                await ExecuteAllPlayerAttack(
                    SlamDownDamage,
                    1,
                    ["Slash"]);
                await CreatureCmd.GainBlock(
                    Creature,
                    SlamDownBlock,
                    ValueProp.Move,
                    null);
                break;
            case PhilosophyFloorTwilightAction.Talon:
                await ExecuteAllPlayerAttack(
                    TalonDamage,
                    TalonHits,
                    ["Slash", "Penetrate", "Penetrate"]);
                break;
            case PhilosophyFloorTwilightAction.Prowl:
                await CreatureCmd.TriggerAnim(Creature, "Guard", 0.46f);
                await CreatureCmd.GainBlock(
                    Creature,
                    ProwlBlock,
                    ValueProp.Move,
                    null);
                await LibraryPowerCmd.Apply<LibraryStrongPower>(
                    Creature,
                    ProwlStrong,
                    0,
                    Creature,
                    null);
                break;
            case PhilosophyFloorTwilightAction.ProtectBlackForest:
                await ExecuteProtectBlackForest();
                break;
            case PhilosophyFloorTwilightAction.TornMouth:
                await ExecuteTornMouth();
                break;
            case PhilosophyFloorTwilightAction.TiltedScale:
                await ExecuteTiltedScale();
                break;
            case PhilosophyFloorTwilightAction.ForestLight:
                await ExecuteForestLight(plannedTargets);
                break;
            case PhilosophyFloorTwilightAction.Punishment:
                await ExecutePunishment();
                break;
            case PhilosophyFloorTwilightAction.BrilliantEyes:
                await ExecuteBrilliantEyes(plannedTargets);
                break;
            case PhilosophyFloorTwilightAction.PeaceForAll:
                await ExecuteGroupAttack(PeaceForAllDamage, "Peace");
                break;
            case PhilosophyFloorTwilightAction.Surveillance:
                await ExecuteSurveillance(plannedTargets);
                break;
            case PhilosophyFloorTwilightAction.Judgment:
                await ExecuteJudgment();
                break;
        }
    }

    internal AbstractIntent DebugCreateActionIntent(
        PhilosophyFloorTwilightAction action,
        int slot = 0) => CreateActionIntent(action, slot);

    internal async Task DebugExecuteAction(
        PhilosophyFloorTwilightAction action,
        IReadOnlyList<Creature> targets,
        int slot = 0)
    {
        await ExecuteAction(action, targets, slot);
    }

    private async Task ExecuteProtectBlackForest()
    {
        IReadOnlyList<AttackCommand> commands = await ExecuteAllPlayerAttack(
            ProtectBlackForestDamage,
            1,
            ["Penetrate"]);
        foreach (Creature target in ResolveLivingHitPlayers(commands))
        {
            await PowerCmdCompat.ApplyDebuff<FrailPower>(
                target,
                ProtectFrail,
                Creature,
                null);
            await PowerCmdCompat.ApplyDebuff<WeakPower>(
                target,
                ProtectWeak,
                Creature,
                null);
        }
    }

    private async Task ExecuteTornMouth()
    {
        for (int hit = 0;
             hit < TornMouthHits && GetLivingPlayers().Count > 0;
             hit++)
        {
            IReadOnlyList<AttackCommand> commands =
                await ExecuteAllPlayerAttack(
                TornMouthDamage,
                1,
                [hit == 0 ? "Punishment" : "PunishmentFollowup"],
                beforeHit: _ => Task.WhenAll(
                    GetLivingPlayers().Select(target =>
                        PhilosophyFloorLiberationVfx.PlayBloodStrikeAsync(
                            Creature,
                            target,
                            hit))));
            foreach (Creature target in ResolveLivingHitPlayers(commands))
            {
                await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                    target,
                    TornMouthBleedPerHit,
                    Creature,
                    null);
            }
        }
    }

    private async Task ExecuteTiltedScale()
    {
        IReadOnlyList<Creature> players = GetLivingPlayers();
        if (players.Count == 0)
        {
            return;
        }

        Task animationTask = CreatureCmd.TriggerAnim(
            Creature,
            "Judgment",
            0.88f);
        await PhilosophyFloorLiberationVfx.PlayTiltedScaleAsync(
            Creature,
            players,
            async () =>
            {
                IReadOnlyList<AttackCommand> commands =
                    await ExecuteAllPlayerAttack(
                    TiltedScaleDamage,
                    1,
                    ["Judgment"],
                    playAttackerAnimations: false);
                foreach (Creature target in ResolveLivingHitPlayers(commands))
                {
                    await PowerCmdCompat
                        .ApplyDebuff<PhilosophyFloorTwilightSinPower>(
                            target,
                            TiltedScaleSin,
                            Creature,
                            null);
                }
            });
        await animationTask;
    }

    private async Task ExecuteForestLight(
        IReadOnlyList<Creature> targets)
    {
        Creature[] livingTargets = targets
            .Where(static target => target.IsAlive)
            .ToArray();
        if (livingTargets.Length == 0)
        {
            return;
        }

        Task animationTask = CreatureCmd.TriggerAnim(
            Creature,
            "ForestLight",
            0.72f);
        Task vfxTask = PhilosophyFloorLiberationVfx.PlayEyeLaserAsync(
            Creature,
            livingTargets,
            async () =>
            {
                foreach (Creature target in livingTargets.Where(
                             static target => target.IsAlive))
                {
                    await LibraryPowerCmd.Apply<LibraryBindingPower>(
                        target,
                        ForestLightBind,
                        ForestLightBindTurns,
                        Creature,
                        null);
                    await PowerCmdCompat
                        .ApplyDebuff<LibraryOfRuinaConfusionPower>(
                            target,
                            ForestLightConfusion,
                            Creature,
                            null);
                }
            });
        await Task.WhenAll(animationTask, vfxTask);
    }

    private async Task ExecutePunishment()
    {
        IReadOnlyList<Creature> players = GetLivingPlayers();
        if (players.Count == 0)
        {
            return;
        }

        int totalSin = players.Sum(static target => Math.Max(
            0,
            target.GetPower<PhilosophyFloorTwilightSinPower>()?.Amount ?? 0));
        await ExecuteAllPlayerAttack(
            PunishmentDamage,
            PunishmentHits,
            [
                "Punishment",
                "PunishmentFollowup",
                "Punishment",
                "PunishmentFollowup"
            ],
            beforeHit: hit => Task.WhenAll(
                GetLivingPlayers().Select(target =>
                    PhilosophyFloorLiberationVfx.PlayBloodStrikeAsync(
                        Creature,
                        target,
                        hit))));
        if (totalSin > 0 && Creature.IsAlive)
        {
            int heal = (int)Math.Ceiling(
                Creature.MaxHp * PunishmentHealPercentPerSin * totalSin
                / 100m);
            await CreatureCmd.Heal(Creature, heal);
        }
    }

    private async Task ExecuteBrilliantEyes(
        IReadOnlyList<Creature> targets)
    {
        Creature[] players = targets
            .Where(static target => target.IsAlive)
            .ToArray();
        if (players.Length == 0)
        {
            return;
        }

        Task animationTask = CreatureCmd.TriggerAnim(
            Creature,
            "BrilliantEyes",
            0.82f);
        Task vfxTask = PhilosophyFloorLiberationVfx.PlayEyeLaserAsync(
            Creature,
            players,
            () => CardPileCmdCompat.AddToCombatAndPreview<VoidCard>(
                players,
                PileType.Discard,
                BrilliantEyesVoidCount,
                addedByPlayer: false));
        await Task.WhenAll(animationTask, vfxTask);
    }

    private async Task ExecuteSurveillance(
        IReadOnlyList<Creature> targets)
    {
        Task darknessTask =
            PhilosophyFloorLiberationVfx.PlaySurveillanceDarknessAsync(
                Creature);
        Task animationTask = CreatureCmd.TriggerAnim(
            Creature,
            "Watch",
            0.82f);
        int strengthGain = 0;
        foreach (Creature target in targets.Where(
                     static target => target.IsAlive))
        {
            if (target.GetPower<PhilosophyFloorTwilightFearPower>() == null)
            {
                await PowerCmdCompat
                    .ApplyDebuff<PhilosophyFloorTwilightFearPower>(
                        target,
                        1,
                        Creature,
                        null);
            }
            else
            {
                strengthGain += SurveillanceStrength;
            }
        }
        if (strengthGain > 0)
        {
            await PowerCmdCompat.Apply<StrengthPower>(
                Creature,
                strengthGain,
                Creature,
                null);
        }
        await Task.WhenAll(darknessTask, animationTask);
    }

    private async Task ExecuteJudgment()
    {
        IReadOnlyList<Creature> players = GetLivingPlayers();
        if (players.Count == 0)
        {
            return;
        }

        Task animationTask = CreatureCmd.TriggerAnim(
            Creature,
            "Judgment",
            0.88f);
        await PhilosophyFloorLiberationVfx.PlayJudgmentAsync(
            Creature,
            players,
            () => ExecuteJudgmentGroupAttack(players));
        await animationTask;
    }

    private async Task ExecuteJudgmentGroupAttack(
        IReadOnlyList<Creature> targets)
    {
        CombatStateLike combatState = Creature.CombatState
                                      ?? throw new InvalidOperationException(
                                          "Twilight Judgment requires an active combat state.");
        AttackCommand command = DamageCmd.Attack(0)
            .FromMonster(this)
            .Unpowered()
            .WithNoAttackerAnim();
        var choiceContext = new BlockingPlayerChoiceContext();

        await Hook.BeforeAttack(combatState, command);
        await Cmd.CustomScaledWait(
            AttackHitStopFastSeconds,
            AttackHitStopStandardSeconds);
        List<DamageResult> allResults = [];
        foreach (Creature target in targets.Where(static target =>
                     target.IsAlive))
        {
            int damage = PhilosophyFloorTwilightJudgmentIntent
                .CalculateDamage(target);
            IEnumerable<DamageResult> targetResults;
            using (PhilosophyFloorTwilightJudgmentPowerBypassContext
                   .EnterJudgmentDamage(Creature))
            {
                targetResults = await CreatureCmd.Damage(
                    choiceContext,
                    target,
                    damage,
                    command.DamageProps,
                    Creature,
                    null,
                    null);
            }
            allResults.AddRange(targetResults);
        }

        command.AddResultsInternal(allResults);
        CombatManager.Instance.History.CreatureAttacked(
            combatState,
            Creature,
            allResults);
        await Hook.AfterAttack(combatState, choiceContext, command);
    }

    private async Task<IReadOnlyList<AttackCommand>>
        ExecuteAllPlayerAttack(
        int damage,
        int hits,
        IReadOnlyList<string> animations,
        Func<int, Task>? beforeHit = null,
        bool playAttackerAnimations = true)
    {
        if (hits <= 0 || GetLivingPlayers().Count == 0)
        {
            return [];
        }

        List<AttackCommand> commands = [];
        for (int hit = 0;
             hit < hits
             && GetLivingPlayers().Count > 0
             && Creature.IsAlive;
             hit++)
        {
            string animation = animations.Count == 0
                ? "Attack"
                : animations[hit % animations.Count];
            AttackCommand command =
                DamageCmd.Attack(damage)
                    .FromMonster(this)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .WithWaitBeforeHit(
                        AttackHitStopFastSeconds,
                        AttackHitStopStandardSeconds);
            if (playAttackerAnimations)
            {
                command.WithAttackerAnim(
                    animation,
                    ResolveAttackerAnimationDuration(animation));
            }
            else
            {
                command.WithNoAttackerAnim();
            }

            if (ShouldPlayDefaultAttackSfx(
                    beforeHit != null,
                    playAttackerAnimations))
            {
                command.AfterAttackerAnim(() =>
                {
                    PhilosophyFloorLiberationVfx.PlayPunishmentSfx();
                    return Task.CompletedTask;
                });
            }

            int hitIndex = hit;
            if (beforeHit != null)
            {
                command.BeforeDamage(() => beforeHit(hitIndex));
            }

            commands.Add(await command.Execute(null));
        }

        return commands;
    }

    private static IReadOnlyList<Creature> ResolveLivingHitPlayers(
        IEnumerable<AttackCommand> commands) => commands
        .SelectMany(AttackCommandCompat.Results)
        .Select(static result => result.Receiver)
        .Where(static target => target is { IsAlive: true, IsPlayer: true })
        .Distinct()
        .OrderBy(static target => target.CombatId)
        .ToArray();

    internal static bool ShouldPlayDefaultAttackSfx(
        bool hasCustomBeforeHit,
        bool playAttackerAnimations) =>
        !hasCustomBeforeHit && playAttackerAnimations;

    internal static float ResolveAttackerAnimationDuration(
        string animation) => animation switch
    {
        "Punishment" => 0.58f,
        "PunishmentFollowup" => 0.54f,
        _ => 0.48f
    };

    private async Task<AttackCommand?> ExecuteGroupAttack(
        int damage,
        string animation)
    {
        IReadOnlyList<Creature> players = GetLivingPlayers();
        if (players.Count == 0)
        {
            return null;
        }

        AttackCommand? result = null;
        Task animationTask = CreatureCmd.TriggerAnim(
            Creature,
            animation,
            1.06f);
        await PhilosophyFloorLiberationVfx.PlayPeaceSlamAsync(
            Creature,
            players,
            async () =>
            {
                result = await DamageCmd.Attack(damage)
                    .FromMonster(this)
                    .WithNoAttackerAnim()
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .WithWaitBeforeHit(
                        AttackHitStopFastSeconds,
                        AttackHitStopStandardSeconds)
                    .SpawningHitVfxOnEachCreature()
                    .Execute(null);
            });
        await animationTask;
        return result;
    }

    private IReadOnlyList<Creature> GetLivingPlayers() =>
        CombatTargets.DeterministicLiving(
            Creature.CombatState?.PlayerCreatures,
            Creature);

    private static int AscensionDamage(int low, int high) =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            high,
            low);
}

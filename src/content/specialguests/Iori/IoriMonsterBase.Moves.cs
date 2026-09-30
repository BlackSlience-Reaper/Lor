using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.specialguests.Iori;

// 招式执行：复合行动逐槽调用 PerformMove。
public abstract partial class IoriMonsterBase
{
    private async Task PerformMove(IoriMove move)
    {
        IoriMoveDefinition definition = IoriMoveDefinitions.Get(move);
        if (move == IoriMove.StanceShift)
        {
            LibraryDamageType type = IoriStanceController.ResolveDamageType(CurrentStance);
            var attack = new IoriAttackSegment(
                definition.Attacks[0].Damage,
                type,
                "StanceChange",
                definition.Attacks[0].SfxFile);
            await ExecuteAttack(attack, hitCount: 1);
            if (CanContinueMove)
            {
                await ChangeStance(
                    PlannedNextStance == IoriStance.None
                        ? ResolveFallbackNextStance()
                        : PlannedNextStance,
                    applyContributions: true);
            }
            return;
        }

        if (move == IoriMove.PhantomDance)
        {
            IoriCreatureVisuals.BeginAttackChain(Creature);
            try
            {
                foreach (IoriAttackSegment attack in definition.Attacks)
                {
                    if (!CanContinueMove)
                    {
                        break;
                    }

                    IReadOnlyList<DamageResult> results =
                        await ExecuteAttack(attack, hitCount: 1);
                    if (!CanContinueMove)
                    {
                        break;
                    }

                    await ApplyBleedAfterHit(
                        results,
                        definition.PrimaryEffect.Resolve());
                }
            }
            finally
            {
                IoriCreatureVisuals.EndAttackChain(Creature);
            }
            return;
        }

        if (definition.Attacks.Count > 0)
        {
            bool continuous = definition.Attacks.Count > 1;
            if (continuous)
            {
                IoriCreatureVisuals.BeginAttackChain(Creature);
            }

            try
            {
                foreach (IoriAttackSegment attack in definition.Attacks)
                {
                    if (!CanContinueMove)
                    {
                        break;
                    }

                    await ExecuteAttack(attack, hitCount: 1);
                }
            }
            finally
            {
                if (continuous)
                {
                    IoriCreatureVisuals.EndAttackChain(Creature);
                }
            }
        }

        if (!CanContinueMove)
        {
            return;
        }

        if (definition.BlockAmount > 0)
        {
            await PlayGuardAnimation();
            await CreatureCmd.GainBlock(
                Creature,
                definition.BlockAmount,
                ValueProp.Move,
                null);
        }
        else if (definition.Attacks.Count == 0
                 && definition.Effect != IoriMoveEffect.None)
        {
            await PlayGuardAnimation();
        }

        await ApplyMoveEffect(definition);
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteAttack(
        IoriAttackSegment segment,
        int hitCount)
    {
        if (!CanContinueMove)
        {
            return [];
        }

        var allResults = new List<DamageResult>();
        bool continuous = hitCount > 1;
        if (continuous)
        {
            IoriCreatureVisuals.BeginAttackChain(Creature);
        }

        _activeDamageType = segment.DamageType;
        try
        {
            for (int hit = 0;
                 hit < Math.Max(1, hitCount) && CanContinueMove;
                 hit++)
            {
                LocalOggOneShotPlayer.Play(
                    IoriSpecialGuestIds.CombatAudioRoot + segment.SfxFile,
                    -2f);
                await CreatureCmd.TriggerAnim(
                    Creature,
                    segment.Animation,
                    0f);
                await Cmd.Wait(IoriAnimationContract.ActionDurationSeconds);
                if (!CanContinueMove)
                {
                    break;
                }

                AttackCommand command = await DamageCmd
                    .Attack(segment.ResolveDamage())
                    .FromMonster(this)
                    .WithHitCount(1)
                    .WithNoAttackerAnim()
                    .WithHitFx(
                        segment.DamageType == LibraryDamageType.Blunt
                            ? "vfx/vfx_attack_blunt"
                            : "vfx/vfx_attack_slash")
                    .SpawningHitVfxOnEachCreature()
                    .Execute(null);
                allResults.AddRange(
                    AttackCommandCompat.Results(command)
                        .Where(static result => result.Receiver.IsPlayer));
            }

            RecordDirectAttackDamageDealt(allResults);
            return allResults;
        }
        finally
        {
            _activeDamageType = LibraryDamageType.None;
            if (continuous)
            {
                IoriCreatureVisuals.EndAttackChain(Creature);
            }
        }
    }

    private async Task ApplyMoveEffect(IoriMoveDefinition definition)
    {
        Creature[] players = LivingPlayers();
        switch (definition.Effect)
        {
            case IoriMoveEffect.None:
                return;
            case IoriMoveEffect.GainStrength:
                await PowerCmdCompat.Apply<StrengthPower>(
                    Creature,
                    definition.PrimaryEffect.Resolve(),
                    Creature,
                    null);
                return;
            case IoriMoveEffect.ApplyFrailAndWeak:
                foreach (Creature player in players)
                {
                    await PowerCmdCompat.ApplyDebuff<FrailPower>(
                        player,
                        definition.PrimaryEffect.Resolve(),
                        Creature,
                        null);
                    await PowerCmdCompat.ApplyDebuff<WeakPower>(
                        player,
                        definition.SecondaryEffect.Resolve(),
                        Creature,
                        null);
                }
                return;
            case IoriMoveEffect.ApplyBleedAndRapidWear:
                foreach (Creature player in players)
                {
                    await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                        player,
                        definition.PrimaryEffect.Resolve(),
                        Creature,
                        null);
                    await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                        player,
                        definition.SecondaryEffect.Resolve(),
                        definition.EffectTurns,
                        Creature,
                        null);
                }
                return;
            case IoriMoveEffect.OfferPenetratingWoundChoice:
                await IoriPenetratingWoundChoices.ChooseForPlayersAsync(
                    players,
                    definition.PrimaryEffect.Resolve(),
                    definition.SecondaryEffect.Resolve());
                return;
            case IoriMoveEffect.HealPercentMaxHp:
                await CreatureCmd.Heal(
                    Creature,
                    (int)Math.Ceiling(
                        Creature.MaxHp
                        * definition.PrimaryEffect.Resolve()
                        / 100m),
                    playAnim: false);
                return;
            case IoriMoveEffect.AddWounds:
                foreach (Creature player in players)
                {
                    await CardPileCmdCompat.AddToCombatAndPreview<Wound>(
                        player,
                        PileType.Discard,
                        definition.PrimaryEffect.Resolve(),
                        addedByPlayer: false);
                }
                return;
            case IoriMoveEffect.ApplyWeak:
                foreach (Creature player in players)
                {
                    await PowerCmdCompat.ApplyDebuff<WeakPower>(
                        player,
                        definition.PrimaryEffect.Resolve(),
                        Creature,
                        null);
                }
                return;
            case IoriMoveEffect.ApplyBlur:
                await PowerCmdCompat.Apply<BlurPower>(
                    Creature,
                    definition.PrimaryEffect.Resolve(),
                    Creature,
                    null);
                return;
            case IoriMoveEffect.SwitchStance:
            case IoriMoveEffect.ApplyBleedPerHit:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.Effect,
                    null);
        }
    }

    private async Task ApplyBleedAfterHit(
        IReadOnlyList<DamageResult> results,
        int amount)
    {
        foreach (Creature target in results
                     .Select(static result => result.Receiver)
                     .Where(static target => target.IsAlive)
                     .Distinct()
                     .OrderBy(static target => target.CombatId))
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                target,
                amount,
                Creature,
                null);
        }
    }

    private async Task PlayGuardAnimation()
    {
        LocalOggOneShotPlayer.Play(
            IoriSpecialGuestIds.CombatAudioRoot + "Purple_Guard.ogg",
            -2f);
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0f);
        await Cmd.Wait(IoriAnimationContract.ActionDurationSeconds);
    }

    /// <summary>
    /// 每段攻击之后、附带效果之前都要复查：荆棘等反伤可能在攻击中杀死伊织，最后一名玩家倒下或最后的敌人死亡时战斗进入结束流程。
    /// 这些情况下后续的格挡、换姿态、减益与加牌都不应再结算。判定只读同步的战斗状态，两端在同一条命令处得到相同结果。
    /// </summary>
    private bool CanContinueMove =>
        Creature is { IsDead: false, CombatState: not null }
        && !CombatManager.Instance.IsOverOrEnding
        && CanPerformRegularMoves;

    private Creature[] LivingPlayers() =>
        Creature.CombatState?.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .OrderBy(static creature => creature.CombatId)
            .ToArray()
        ?? [];
}

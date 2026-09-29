using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.QueenBee;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.visuals.RedMist;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Kali;

// 各张卡的执行，以及卡莉与玩家手里的红雾卡共用的逐段攻击演出（动画序列由确定性种子决定，联机各端一致）。
public sealed partial class Kali
{

    private void QueueRepeatIfThreshold(IReadOnlyList<DamageResult> results, string cardId)
    {
        decimal unblockedDamage = results.Sum(static result => result.UnblockedDamage);
        if (unblockedDamage >= RepeatUnblockedDamageThreshold)
        {
            QueueExtraCardId(cardId);
        }
    }

    private async Task PlayVerticalSplit(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> results = await ExecuteMoveAttack(
            GetLivingTargets(targets),
            VerticalSplitDamage,
            VerticalSplitHits,
            SlashHitVfx,
            "AttackSlash");
        QueueRepeatIfThreshold(results, RedMistVerticalSplitCardId);
    }

    private async Task PlayThrust(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> results = await ExecuteMoveAttack(
            GetLivingTargets(targets),
            ThrustDamage,
            ThrustHits,
            PierceHitVfx,
            "AttackPierce");
        QueueRepeatIfThreshold(results, RedMistThrustCardId);
    }

    private async Task PlayHorizontalSlash(IReadOnlyList<Creature> targets)
    {
        IReadOnlyList<DamageResult> results = await ExecuteMoveAttack(
            GetLivingTargets(targets),
            HorizontalSlashDamage,
            HorizontalSlashHits,
            SlashHitVfx,
            "AttackSlash");
        IReadOnlyList<Creature> bleedTargets = results
            .Where(static result => result.Receiver.IsPlayer && result.UnblockedDamage > 0m)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();
        if (bleedTargets.Count > 0)
        {
            await PowerCmdCompat.Apply<LibraryBleedingPower>(
                bleedTargets,
                HorizontalSlashBleed,
                Creature,
                null);
        }

        QueueRepeatIfThreshold(results, RedMistHorizontalSlashCardId);
    }

    private async Task PlayBloodMist(IReadOnlyList<Creature> targets)
    {
        await ExecuteMoveAttack(
            GetLivingTargets(targets),
            BloodMistDamage,
            1,
            SlashHitVfx,
            "BloodMist",
            onUnblockedHit: target => ExhaustRandomDrawPileCard(new ThrowingPlayerChoiceContext(), this, target));
    }

    private async Task PlayBattleWill(IReadOnlyList<Creature> targets)
    {
        HashSet<Creature> repeatedTargets = [];
        await ExecuteMoveAttack(
            GetLivingTargets(targets),
            BattleWillDamage,
            1,
            BluntHitVfx,
            "AttackBlunt",
            onUnblockedHit: async hitTarget =>
            {
                if (!repeatedTargets.Add(hitTarget))
                {
                    return;
                }

                await ExecuteMoveAttack([hitTarget], BattleWillDamage, 1, BluntHitVfx, "AttackBlunt");
            });
    }

    private async Task PlayFocusBreath()
    {
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0.4f);
        await CreatureCmd.GainBlock(Creature, FocusBreathBlock, ValueProp.Move, null);
        await LibraryPowerCmd.Apply<QueenBeeNextTurnStrongPower>(
            Creature,
            FocusBreathStrong,
            Creature,
            null);
    }

    private async Task PlayFieldOfCorpses(IReadOnlyList<Creature> targets)
    {
        await ExecuteMoveAttack(
            GetLivingTargets(targets),
            FieldOfCorpsesDamage,
            1,
            SlashHitVfx,
            "FieldOfCorpses",
            onUnblockedHit: target => PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                target,
                FieldOfCorpsesBleed,
                Creature,
                null),
            attackerAnimDelaySeconds: KaliCreatureVisuals.FieldOfCorpsesAnimationSeconds);
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteMoveAttack(
        IReadOnlyList<Creature> targets,
        int damage,
        int hits,
        string hitVfx,
        string anim,
        Func<Creature, Task>? onUnblockedHit = null,
        float? attackerAnimDelaySeconds = null)
    {
        IReadOnlyList<Creature> livingTargets = GetLivingTargets(targets);
        if (livingTargets.Count == 0 || !CanContinueMove)
        {
            return [];
        }

        var results = new List<DamageResult>();
        int hitCount = Math.Max(1, hits);
        IReadOnlyList<string> attackAnimations = ResolveAttackAnimationSequence(
            hitCount,
            anim,
            GetDeterministicAttackAnimationSeed(hitCount, anim));
        bool continuousAttack = hitCount > 1;
        if (continuousAttack)
        {
            KaliCreatureVisuals.BeginAttackChain(Creature);
        }

        try
        {
            for (int i = 0; i < hitCount; i++)
            {
                if (!CanContinueMove)
                {
                    return results;
                }

                string segmentAnim = attackAnimations[i];
                string segmentHitVfx = hitCount > 1
                    ? ResolveAttackSegmentVfx(segmentAnim)
                    : hitVfx;
                AttackCommand command;
                bool deferAttackSfxUntilSettlement =
                    RequiresCompletedAnimationBeforeSettlement(segmentAnim);
                if (!deferAttackSfxUntilSettlement)
                {
                    PlayAttackSegmentSfx(segmentAnim);
                }

                bool animationCompletedBeforeSettlement =
                    await PlayAttackerAnimationBeforeSettlement(
                        Creature,
                        segmentAnim);
                if (deferAttackSfxUntilSettlement)
                {
                    PlayAttackSegmentSfx(segmentAnim);
                }

                using (TargetedMonsterAttackHelper.ForceTargets(Creature, livingTargets))
                {
                    if (!CanContinueMove)
                    {
                        return results;
                    }

                    AttackCommand attack = DamageCmd.Attack(damage)
                        .FromMonster(this)
                        .WithHitCount(1)
                        .WithHitFx(segmentHitVfx);
                    if (animationCompletedBeforeSettlement)
                    {
                        attack.WithNoAttackerAnim();
                    }
                    else
                    {
                        attack.WithAttackerAnim(
                            segmentAnim,
                            attackerAnimDelaySeconds
                            ?? ResolveAttackerAnimationDelaySeconds(segmentAnim));
                    }

                    command = await attack.Execute(null);
                }

                IReadOnlyList<DamageResult> hitResults = AttackCommandCompat.Results(command);
                RecordDirectAttackDamageDealt(hitResults);
                foreach (DamageResult result in hitResults)
                {
                    results.Add(result);
                }

                if (!CanContinueMove)
                {
                    return results;
                }

                foreach (DamageResult result in hitResults)
                {
                    if (onUnblockedHit != null && result.UnblockedDamage > 0m)
                    {
                        await onUnblockedHit(result.Receiver);
                        if (!CanContinueMove)
                        {
                            return results;
                        }
                    }
                }

                if (i + 1 < hitCount)
                {
                    if (!CanContinueMove)
                    {
                        return results;
                    }

                    await Cmd.CustomScaledWait(0.04f, 0.08f);
                }
            }
        }
        finally
        {
            if (continuousAttack)
            {
                KaliCreatureVisuals.EndAttackChain(Creature);
            }
        }

        return results;
    }

    private static IReadOnlyList<Creature> GetLivingTargets(IEnumerable<Creature> targets)
    {
        return targets
            .Where(static target => target is { IsAlive: true })
            .Distinct()
            .ToArray();
    }

    private bool CanContinueMove => Creature is { IsDead: false, CombatState: not null };

    public static async Task<IReadOnlyList<DamageResult>> ExecuteCardAttack(
        PlayerChoiceContext choiceContext,
        CardModel card,
        Creature target,
        int damage,
        int hits,
        string hitVfx,
        string anim,
        Func<Creature, Task>? onUnblockedHit = null)
    {
        var results = new List<DamageResult>();
        int hitCount = Math.Max(1, hits);
        IReadOnlyList<string> attackAnimations = ResolveAttackAnimationSequence(
            hitCount,
            anim,
            GetDeterministicAttackAnimationSeed(hitCount, anim));
        for (int i = 0; i < hitCount; i++)
        {
            string segmentAnim = attackAnimations[i];
            string segmentHitVfx = hitCount > 1
                ? ResolveAttackSegmentVfx(segmentAnim)
                : hitVfx;
            bool deferAttackSfxUntilSettlement =
                RequiresCompletedAnimationBeforeSettlement(segmentAnim);
            if (!deferAttackSfxUntilSettlement)
            {
                PlayAttackSegmentSfx(segmentAnim);
            }

            bool animationCompletedBeforeSettlement =
                await PlayAttackerAnimationBeforeSettlement(
                    card.Owner.Creature,
                    segmentAnim);
            if (deferAttackSfxUntilSettlement)
            {
                PlayAttackSegmentSfx(segmentAnim);
            }

            AttackCommand attack = DamageCmd.Attack(damage)
                .FromCard(card, null)
                .Targeting(target)
                .WithHitCount(1)
                .WithHitFx(segmentHitVfx);
            if (animationCompletedBeforeSettlement)
            {
                attack.WithNoAttackerAnim();
            }
            else
            {
                attack.WithAttackerAnim(
                    segmentAnim,
                    ResolveAttackerAnimationDelaySeconds(segmentAnim));
            }

            AttackCommand command = await attack.Execute(choiceContext);

            foreach (DamageResult result in AttackCommandCompat.Results(command))
            {
                results.Add(result);
                if (onUnblockedHit != null && result.UnblockedDamage > 0m)
                {
                    await onUnblockedHit(result.Receiver);
                }
            }

            if (i + 1 < hitCount)
            {
                await Cmd.CustomScaledWait(0.04f, 0.08f);
            }
        }

        return results;
    }

    internal static float ResolveAttackerAnimationDelaySeconds(string anim) => anim switch
    {
        "BloodMist" => 0f,
        _ => AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds
    };

    internal static bool RequiresCompletedAnimationBeforeSettlement(string anim) =>
        string.Equals(anim, "BloodMist", StringComparison.Ordinal);

    internal static float ResolvePreSettlementAnimationWaitSeconds(string anim) =>
        RequiresCompletedAnimationBeforeSettlement(anim)
            ? KaliCreatureVisuals.BloodMistAnimationSeconds
            : 0f;

    private static async Task<bool> PlayAttackerAnimationBeforeSettlement(
        Creature attacker,
        string anim)
    {
        float waitSeconds = ResolvePreSettlementAnimationWaitSeconds(anim);
        if (waitSeconds <= 0f)
        {
            return false;
        }

        await CreatureCmd.TriggerAnim(attacker, anim, 0f);
        await Cmd.Wait(waitSeconds);
        return true;
    }

    private static void PlayAttackSegmentSfx(string anim)
    {
        LocalOggOneShotPlayer.Play(ResolveAttackSegmentSfx(anim), -2f);
    }

    private static string ResolveAttackSegmentSfx(string anim) => anim switch
    {
        "AttackPierce" => RedMistPierceSfxPath,
        "AttackBlunt" => RedMistBluntSfxPath,
        _ => RedMistSlashSfxPath
    };

    private static string ResolveAttackSegmentVfx(string anim) => anim switch
    {
        "AttackPierce" => PierceHitVfx,
        "AttackBlunt" => BluntHitVfx,
        _ => SlashHitVfx
    };

    private static int GetDeterministicAttackAnimationSeed(
        int hitCount,
        string fallbackAnimation)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + hitCount;
            foreach (char character in fallbackAnimation)
            {
                hash = hash * 31 + character;
            }

            return hash;
        }
    }

    internal static IReadOnlyList<string> ResolveAttackAnimationSequence(
        int hits,
        string fallbackAnimation,
        int seed)
    {
        int hitCount = Math.Max(1, hits);
        if (hitCount == 1)
        {
            return [fallbackAnimation];
        }

        var random = new Random(seed);
        var result = new List<string>(hitCount);
        string? previous = null;
        while (result.Count < hitCount)
        {
            string[] bag = BasicAttackAnimations.ToArray();
            for (int i = bag.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (bag[i], bag[swapIndex]) = (bag[swapIndex], bag[i]);
            }

            if (previous != null && bag[0] == previous)
            {
                int swapIndex = random.Next(1, bag.Length);
                (bag[0], bag[swapIndex]) = (bag[swapIndex], bag[0]);
            }

            foreach (string animation in bag)
            {
                if (result.Count >= hitCount)
                {
                    break;
                }

                result.Add(animation);
                previous = animation;
            }
        }

        return result;
    }

    public static async Task ExhaustRandomDrawPileCard(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature target)
    {
        Player? targetPlayer = target.Player;
        if (targetPlayer == null)
        {
            return;
        }

        IReadOnlyList<CardModel> drawCards = PileType.Draw.GetPile(targetPlayer).Cards;
        if (drawCards.Count == 0)
        {
            return;
        }

        CardModel? card = owner.RunState.Rng.CombatCardSelection.NextItem(drawCards);
        if (card == null)
        {
            return;
        }

        await CardCmd.Exhaust(choiceContext, card);
    }

    public static async Task ExhaustRandomDrawPileCard(
        PlayerChoiceContext choiceContext,
        MonsterModel owner,
        Creature target)
    {
        Player? targetPlayer = target.Player;
        if (targetPlayer == null)
        {
            return;
        }

        IReadOnlyList<CardModel> drawCards = PileType.Draw.GetPile(targetPlayer).Cards;
        if (drawCards.Count == 0)
        {
            return;
        }

        CardModel? card = owner.Creature?.CombatState?.RunState.Rng.CombatCardSelection.NextItem(drawCards);
        if (card == null)
        {
            return;
        }

        await CardCmd.Exhaust(choiceContext, card);
    }

}

using System;
using Godot;
using HarmonyLib;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

/// <summary>
/// 本模组怪物的外观不是 Spine，原版 SetAnimationTrigger 只驱动 Spine 动画机，对它们不起作用；这里把触发转给
/// 非 Spine 外观的处理器或 AnimationPlayer。攻击动画进行中收到的 "Hit" 会被忽略，免得打断攻击动作。
/// 只处理本模组怪物：原来的前缀会对任何 Spine 生物（原版角色、原版与其他模组的怪物）吞掉攻击中的受击触发。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class NonSpineAnimationTriggerBridgePatch
{

    private static void Postfix(NCreature __instance, string trigger)
    {
        if (!ModOwnership.IsOwnMonster(__instance.Entity)
            || AttackAnimationHitSuppression.ShouldSuppress(__instance.Entity, trigger))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(trigger))
        {
            return;
        }

        if (HasSpineAnimator(__instance))
        {
            return;
        }

        NCreatureVisuals? visuals = __instance.Visuals;
        if (visuals == null)
        {
            return;
        }

        if (visuals is INonSpineVisualTriggerHandler triggerHandler
            && triggerHandler.TryPlayTrigger(trigger))
        {
            return;
        }

        AnimationPlayer? animationPlayer = ResolveAnimationPlayer(visuals);
        if (animationPlayer == null)
        {
            return;
        }

        if (!animationPlayer.HasAnimation(trigger))
        {
            return;
        }

        animationPlayer.Play(trigger);
    }

    private static AnimationPlayer? ResolveAnimationPlayer(NCreatureVisuals visuals)
    {
        AnimationPlayer? direct = visuals.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (direct != null)
        {
            return direct;
        }

        foreach (Node child in visuals.GetChildren())
        {
            if (child is AnimationPlayer player)
            {
                return player;
            }

            AnimationPlayer? nested = child.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static bool HasSpineAnimator(NCreature creature)
    {
        if (!VanillaPrivate.CreatureSpineAnimator.IsAvailable)
        {
            return creature.HasSpineAnimation;
        }

        return VanillaPrivate.CreatureSpineAnimator.Get(creature) != null;
    }
}

using System;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.infra.lifecycle;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Commands.Builders;

namespace LibraryOfRuina.patches.visuals;

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class AttackAnimationHitSuppressionAttackPatch
{
    private static void Prefix(AttackCommand __instance, out AttackAnimationHitSuppression.Scope? __state)
    {
        __state = AttackAnimationHitSuppression.Begin(__instance);
    }

    private static void Postfix(ref Task<AttackCommand> __result, AttackAnimationHitSuppression.Scope? __state)
    {
        if (__state == null)
        {
            return;
        }

        __result = ClearWhenComplete(__result, __state);
    }

    private static async Task<AttackCommand> ClearWhenComplete(
        Task<AttackCommand> result,
        AttackAnimationHitSuppression.Scope state)
    {
        try
        {
            return await result;
        }
        finally
        {
            state.Dispose();
        }
    }
}

internal static class AttackAnimationHitSuppression
{



    private static readonly object Gate = new();
    // 攻击动画进行中的生物（计数支持同一生物的嵌套攻击）。只影响表现。按生物弱键存放：攻击命令的任务随战斗中途退出而
    // 永远不完成时，作用域不会被释放，条目不能把生物及其战斗一直留在内存里；离开本局时也会清空。
    private static readonly CombatScoped<Creature, int> ActiveAttackAnimations = new();

    public static Scope? Begin(AttackCommand command)
    {
        if (!ShouldTrack(command))
        {
            return null;
        }

        List<Creature> creatures = new();
        AddDistinct(command.Attacker);
        AddDistinct(VanillaPrivate.AttackCommandVisualAttacker.Get(command) as Creature);

        if (creatures.Count == 0)
        {
            return null;
        }

        lock (Gate)
        {
            foreach (Creature creature in creatures)
            {
                ActiveAttackAnimations.TryGetValue(creature, out int count);
                ActiveAttackAnimations.Set(creature, count + 1);
            }
        }

        return new Scope(creatures);

        void AddDistinct(Creature? creature)
        {
            if (creature != null && !creatures.Contains(creature))
            {
                creatures.Add(creature);
            }
        }
    }

    public static bool ShouldSuppress(Creature? creature, string trigger)
    {
        if (creature == null || !string.Equals(trigger, "Hit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        lock (Gate)
        {
            return ActiveAttackAnimations.TryGetValue(creature, out _);
        }
    }

    private static bool ShouldTrack(AttackCommand command)
    {
        bool shouldPlayAnimation = !VanillaPrivate.AttackCommandShouldPlayAnimation.TryGet(command, out bool shouldPlay) || shouldPlay;
        if (!shouldPlayAnimation)
        {
            return false;
        }

        string? attackerAnimName = VanillaPrivate.AttackCommandAttackerAnimName.Get(command) as string;
        return !string.IsNullOrWhiteSpace(attackerAnimName);
    }

    public sealed class Scope : IDisposable
    {
        private readonly IReadOnlyList<Creature> _creatures;
        private bool _disposed;

        public Scope(IReadOnlyList<Creature> creatures)
        {
            _creatures = creatures;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lock (Gate)
            {
                foreach (Creature creature in _creatures)
                {
                    if (!ActiveAttackAnimations.TryGetValue(creature, out int count))
                    {
                        continue;
                    }

                    if (count <= 1)
                    {
                        ActiveAttackAnimations.Remove(creature);
                    }
                    else
                    {
                        ActiveAttackAnimations.Set(creature, count - 1);
                    }
                }
            }
        }
    }
}

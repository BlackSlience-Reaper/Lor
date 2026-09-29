using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using static LibraryOfRuina.content.reverberation.GearChurch.GearChurchRules;

namespace LibraryOfRuina.content.reverberation.GearChurch;

/// <summary>物理与混乱共用逐段快照；预览使用独立的只读快照，实际命中才提交消耗。</summary>
internal static class GearChurchHitContext
{
    internal sealed class Hit
    {
        internal Hit(Creature target, Creature? dealer)
            : this(target, dealer, target.GetPower<GearChurchSmokePower>()?.Amount ?? 0,
                target.GetPower<EileenNuovoFabricPower>()?.HitsReceived ?? 0, false)
        {
        }

        internal Hit(Creature target, Creature? dealer, int smokeStacks, int fabricHits, bool isPreview)
        {
            Target = target;
            Dealer = dealer;
            SmokeStacks = smokeStacks;
            Protected = target.GetPower<EileenNuovoFabricPower>() != null && fabricHits < FabricProtectedHits;
            Sober = target.GetPower<GearChurchSoberSmokePower>() != null && smokeStacks > 0;
            IsPreview = isPreview;
        }

        internal Creature Target { get; }

        internal Creature? Dealer { get; }

        internal int SmokeStacks { get; }

        internal bool Protected { get; }

        internal bool Sober { get; }

        internal bool IsPreview { get; }

        internal bool Committed { get; set; }
    }

    internal sealed record Scope(Dictionary<Creature, Hit>? Previous, Dictionary<Creature, Hit> Hits);

    private static readonly AsyncLocal<Dictionary<Creature, Hit>?> Current = new();
    private static readonly ConditionalWeakTable<DamageResult, Hit> Results = new();

    internal static Hit? Find(Creature target) =>
        Current.Value?.GetValueOrDefault(target);

    internal static IDisposable Preview(Creature target, Creature? dealer, int smokeStacks, int fabricHits)
    {
        var hits = new Dictionary<Creature, Hit>
        {
            [target] = new Hit(target, dealer, smokeStacks, fabricHits, true)
        };
        var scope = new Scope(Current.Value, hits);
        Current.Value = hits;
        return new PreviewScope(scope);
    }

    private sealed class PreviewScope(Scope scope) : IDisposable
    {
        public void Dispose() => Restore(scope);
    }

    internal static Scope Enter(IEnumerable<Creature> targets, Creature? dealer, ValueProp props,
        IEnumerable<DamageResult>? results = null)
    {
        var hits = new Dictionary<Creature, Hit>();
        if (ValuePropCompat.IsPoweredAttack(props) && dealer is not { IsDead: true })
        {
            foreach (Creature target in targets.Where(creature => creature.IsAlive).Distinct())
            {
                if (results != null)
                {
                    DamageResult? result = results.FirstOrDefault(item => item.Receiver == target);
                    if (result != null && Results.TryGetValue(result, out Hit? hit))
                    {
                        hits[target] = hit;
                    }
                }
                else if (target.GetPower<EileenNuovoFabricPower>() != null
                    || target.GetPower<GearChurchSoberSmokePower>() != null)
                {
                    hits[target] = new Hit(target, dealer);
                }
            }
        }
        var scope = new Scope(Current.Value, hits);
        Current.Value = hits;
        return scope;
    }

    // 其他模组的跳过型前缀可能让本模组的 Enter 前缀不执行，这时 Finalizer/后缀拿到的 __state 为 null。
    internal static void Restore(Scope? scope)
    {
        if (scope != null)
        {
            Current.Value = scope.Previous;
        }
    }

    internal static async Task Commit(PlayerChoiceContext context, Creature target, Creature? dealer, CardModel? source)
    {
        Hit? hit = Find(target);
        if (hit == null || hit.IsPreview || hit.Committed || hit.Dealer != dealer)
        {
            return;
        }
        hit.Committed = true;
        target.GetPower<EileenNuovoFabricPower>()?.RecordHit(hit.Protected, source);
        if (hit.Sober && target.GetPower<GearChurchSmokePower>() is { } smoke)
        {
            await PowerCmdCompat.ModifyAmount(smoke, -SoberSmokeCost, target, source);
        }
    }

    internal static async Task<IEnumerable<DamageResult>> RememberResults(
        Task<IEnumerable<DamageResult>> task, Scope? scope)
    {
        DamageResult[] results = (await task).ToArray();
        if (scope == null)
        {
            return results;
        }

        foreach (DamageResult result in results)
        {
            if (scope.Hits.TryGetValue(result.Receiver, out Hit? hit) && hit.Committed)
            {
                Results.Remove(result);
                Results.Add(result, hit);
            }
        }
        return results;
    }
}

// 原版攻击会被基础库转发到 LibraryCreatureCmd；内层记录实际命中，外层未提交的快照不会覆盖结果。
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
    new[] { typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp),
        typeof(Creature), typeof(CardModel), typeof(CardPlay) })]
internal static class GearChurchVanillaHitPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ref IEnumerable<Creature> targets, Creature? dealer, ValueProp props,
        out GearChurchHitContext.Scope __state)
    {
        targets = targets.ToArray();
        __state = GearChurchHitContext.Enter(targets, dealer, props);
    }

    private static void Postfix(ref Task<IEnumerable<DamageResult>> __result, GearChurchHitContext.Scope __state) =>
        __result = GearChurchHitContext.RememberResults(__result, __state);

    private static Exception? Finalizer(Exception? __exception, GearChurchHitContext.Scope __state)
    {
        GearChurchHitContext.Restore(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class GearChurchLibraryHitPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(LibraryCreatureCmd))
            .Single(method => method.Name == nameof(LibraryCreatureCmd.Damage)
                && method.GetParameters() is { Length: 9 } parameters
                && parameters[1].ParameterType == typeof(IEnumerable<Creature>)
                && parameters[2].ParameterType == typeof(decimal));

    private static void Prefix(ref IEnumerable<Creature> targets, Creature? dealer, ValueProp props,
        out GearChurchHitContext.Scope __state)
    {
        targets = targets.ToArray();
        __state = GearChurchHitContext.Enter(targets, dealer, props);
    }

    private static void Postfix(ref Task<IEnumerable<DamageResult>> __result, GearChurchHitContext.Scope __state) =>
        __result = GearChurchHitContext.RememberResults(__result, __state);

    private static Exception? Finalizer(Exception? __exception, GearChurchHitContext.Scope __state)
    {
        GearChurchHitContext.Restore(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class GearChurchChaoHitPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(LibraryCreatureCmd))
            .Single(method => method.Name == nameof(LibraryCreatureCmd.ChaoDamage)
                && method.GetParameters() is { Length: 9 } parameters
                && parameters[1].ParameterType == typeof(IEnumerable<Creature>));

    private static void Prefix(ref IEnumerable<Creature> targets, Creature? dealer, ValueProp props,
        IEnumerable<DamageResult>? damageResults, out GearChurchHitContext.Scope __state)
    {
        targets = targets.ToArray();
        __state = GearChurchHitContext.Enter(targets, dealer, props, damageResults ?? []);
    }

    private static Exception? Finalizer(Exception? __exception, GearChurchHitContext.Scope __state)
    {
        GearChurchHitContext.Restore(__state);
        return __exception;
    }
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.guests.BrotherhoodOfIron;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.guests.HookOffice;
using LibraryOfRuina.guests.MusiciansOfBremen;
using LibraryOfRuina.guests.WedgeOffice;
using LibraryOfRuina.guests.YunOffice;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.monsters.LittleRedMercenary;
using LibraryOfRuina.monsters.Tomerry;
using LibraryOfRuina.ui;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.patches;

internal static class CombinedIntentDisplayPatch
{
    private static readonly HashSet<Type> LibraryReceptionMonsterTypes = new()
    {
        typeof(Eri),
        typeof(Finn),
        typeof(Gin),
        typeof(LittleRedRidingHoodedMercenary),
        typeof(Meow),
        typeof(MuMu),
        typeof(Oink),
        typeof(Oscar),
        typeof(Philip),
        typeof(Salvador),
        typeof(Sayo),
        typeof(Tomerry),
        typeof(Yang),
        typeof(Yun),
        typeof(Yuna),
        typeof(HistoryFloorPhaseBoss)
    };

    private static readonly HashSet<Type> LibraryReceptionMonsterBaseTypes = new()
    {
        typeof(BrotherhoodOfIronMonster),
        typeof(HookOfficeMonsterBase),
        typeof(WedgeOfficeSpearTwinBase)
    };

    private static readonly IReadOnlyList<string> CombinedDisplayAssetPaths =
        CombinedIntentAnimData.AssetPaths
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    internal static IntentDecoratorOutcome OnUpdateIntent(NCreature __instance, IEnumerable<Creature> targets)
    {
        try
        {
            Creature? owner = __instance.Entity;
            if (owner?.Monster == null || !ShouldSimplifyCreature(owner))
            {
                return IntentDecoratorOutcome.Skipped;
            }

            IReadOnlyList<AbstractIntent> source = owner.Monster.NextMove.Intents;
            IReadOnlyList<AbstractIntent> display = SimplifyForDisplay(source);
            if (ReferenceEquals(display, source))
            {
                return IntentDecoratorOutcome.Unchanged;
            }

            Render(__instance, display, targets as IReadOnlyList<Creature> ?? targets.ToArray());
            return IntentDecoratorOutcome.Applied;
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "CombinedIntentDisplay.UpdateIntent",
                exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    public static IEnumerable<string> AssetPaths => CombinedDisplayAssetPaths;

    public static bool ShouldPreloadFor(MonsterModel monster)
    {
        return IsLibraryOfRuinaModMonster(monster)
            && monster is not IEnemyCardRuntimeOwner;
    }

    private static bool ShouldSimplifyCreature(Creature owner)
    {
        return owner.IsEnemy
            && owner.Monster != null
            && ShouldPreloadFor(owner.Monster)
            && owner.Monster is not ICounterIntentQueueOwner;
    }

    private static bool IsLibraryOfRuinaModMonster(MonsterModel monster)
    {
        if (monster is LibraryMonsterModel
            or ILiberationPrimaryPhaseBoss)
        {
            return true;
        }

        Type type = monster.GetType();
        if (type.Assembly != typeof(CombinedIntentDisplayPatch).Assembly)
        {
            return false;
        }

        if (LibraryReceptionMonsterTypes.Contains(type))
        {
            return true;
        }

        for (Type? baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            if (LibraryReceptionMonsterBaseTypes.Contains(baseType))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<AbstractIntent> SimplifyForDisplay(IReadOnlyList<AbstractIntent> source)
    {
        IReadOnlyList<AbstractIntent> layoutIntents = RemoveHiddenLayoutPlaceholders(source);
        if (layoutIntents.Count < 2
            || layoutIntents.Any(static intent => intent is ICombinedIntentVisual or IEnemyCardIntent))
        {
            return ReplaceBadgedAttackIntents(layoutIntents);
        }

        if (TrySimplifyPair(layoutIntents[0], layoutIntents[1], out AbstractIntent? combined))
        {
            if (layoutIntents.Count == 2)
            {
                return ReplaceBadgedAttackIntents(new[] { combined! });
            }

            if (layoutIntents.Skip(2).All(static intent => intent is ICounterIntent))
            {
                return ReplaceBadgedAttackIntents(
                    new[] { combined! }.Concat(layoutIntents.Skip(2)).ToArray());
            }
        }

        return ReplaceBadgedAttackIntents(layoutIntents);
    }

    private static IReadOnlyList<AbstractIntent> RemoveHiddenLayoutPlaceholders(
        IReadOnlyList<AbstractIntent> source)
    {
        if (source.Count <= 1 || source.All(static intent => intent is not HiddenIntent))
        {
            return source;
        }

        AbstractIntent[] visible = source
            .Where(static intent => intent is not HiddenIntent)
            .ToArray();
        return visible.Length > 0 ? visible : new[] { source[0] };
    }

    private static IReadOnlyList<AbstractIntent> ReplaceBadgedAttackIntents(IReadOnlyList<AbstractIntent> source)
    {
        AbstractIntent[]? display = null;
        for (int i = 0; i < source.Count; i++)
        {
            if (!TryCreateCombinedBadgedAttack(source[i], out AbstractIntent? combined))
            {
                continue;
            }

            display ??= source.ToArray();
            display[i] = combined;
        }

        return display ?? source;
    }

    private static bool TryCreateCombinedBadgedAttack(
        AbstractIntent intent,
        [NotNullWhen(true)] out AbstractIntent? combined)
    {
        combined = null;
        return intent switch
        {
            BadgedAttackIntent badged => badged.TryCreateCombinedDisplayIntent(out combined),
            BadgedTargetedAttackIntent targeted => targeted.TryCreateCombinedDisplayIntent(out combined),
            _ => false
        };
    }

    private static bool TrySimplifyPair(AbstractIntent first, AbstractIntent second, out AbstractIntent? combined)
    {
        if (TryCreateCounterAttackPair(first, second, out combined)
            || TryCreateCounterAttackPair(second, first, out combined)
            || TryCreateCounterDefendPair(first, second, out combined)
            || TryCreateCounterDefendPair(second, first, out combined)
            || TryCreateAttackPair(first, second, out combined)
            || TryCreateAttackPair(second, first, out combined)
            || TryCreateDefendPair(first, second, out combined)
            || TryCreateDefendPair(second, first, out combined))
        {
            return true;
        }

        combined = null;
        return false;
    }

    private static bool TryCreateCounterAttackPair(
        AbstractIntent attackCandidate,
        AbstractIntent effectCandidate,
        out AbstractIntent? combined)
    {
        combined = null;
        if (attackCandidate is not CounterAttackIntent attack)
        {
            return false;
        }

        Func<decimal> damage = attack.DamageCalc ?? (() => 0m);
        Func<int> repeats = () => Math.Max(1, attack.Repeats);

        if (effectCandidate is CounterDefendIntent defend)
        {
            combined = new CombinedCounterAttackDefendIntent(
                damage,
                repeats,
                blockAmount: ToBlockAmount(defend.BlockAmount));
            return true;
        }

        if (effectCandidate is CounterBuffIntent)
        {
            combined = new CombinedCounterAttackBuffIntent(damage, repeats);
            return true;
        }

        if (effectCandidate is CounterDebuffIntent or CounterCardDebuffIntent)
        {
            combined = new CombinedCounterAttackDebuffIntent(damage, repeats);
            return true;
        }

        return false;
    }

    private static bool TryCreateCounterDefendPair(
        AbstractIntent defendCandidate,
        AbstractIntent effectCandidate,
        out AbstractIntent? combined)
    {
        combined = null;
        if (defendCandidate is not CounterDefendIntent defend)
        {
            return false;
        }

        int blockAmount = ToBlockAmount(defend.BlockAmount);
        if (effectCandidate is CounterBuffIntent)
        {
            combined = new CombinedCounterDefendBuffIntent(blockAmount);
            return true;
        }

        if (effectCandidate is CounterDebuffIntent or CounterCardDebuffIntent)
        {
            combined = new CombinedCounterDefendDebuffIntent(blockAmount);
            return true;
        }

        return false;
    }

    private static bool TryCreateAttackPair(
        AbstractIntent attackCandidate,
        AbstractIntent effectCandidate,
        out AbstractIntent? combined)
    {
        combined = null;
        if (!TryGetPlainAttack(attackCandidate, out AttackIntent? attack))
        {
            return false;
        }

        Func<decimal> damage = attack.DamageCalc ?? (() => 0m);
        Func<int> repeats = () => Math.Max(1, attack.Repeats);

        if (IsPlainDefend(effectCandidate))
        {
            combined = new CombinedAttackDefendIntent(damage, repeats);
            return true;
        }

        if (IsPlainBuff(effectCandidate))
        {
            combined = new CombinedAttackBuffIntent(damage, repeats);
            return true;
        }

        if (IsPlainDebuff(effectCandidate))
        {
            combined = new CombinedAttackDebuffIntent(damage, repeats);
            return true;
        }

        return false;
    }

    private static bool TryCreateDefendPair(
        AbstractIntent defendCandidate,
        AbstractIntent effectCandidate,
        out AbstractIntent? combined)
    {
        combined = null;
        if (!IsPlainDefend(defendCandidate))
        {
            return false;
        }

        if (IsPlainBuff(effectCandidate))
        {
            combined = new CombinedDefendBuffIntent(
                blockAmount: 0,
                descriptionKey: "ENEMY_CARD_COMBINED_DEFEND_BUFF.description");
            return true;
        }

        if (IsPlainDebuff(effectCandidate))
        {
            combined = new CombinedDefendDebuffIntent(
                blockAmount: 0,
                descriptionKey: "ENEMY_CARD_COMBINED_DEFEND_DEBUFF.description");
            return true;
        }

        return false;
    }

    private static bool TryGetPlainAttack(AbstractIntent intent, [NotNullWhen(true)] out AttackIntent? attack)
    {
        attack = intent as AttackIntent;
        if (attack == null)
        {
            return false;
        }

        Type type = intent.GetType();
        return type == typeof(SingleAttackIntent)
            || type == typeof(MultiAttackIntent)
            || type == typeof(DynamicAttackIntent);
    }

    private static bool IsPlainDefend(AbstractIntent intent)
    {
        return intent.GetType() == typeof(DefendIntent);
    }

    private static bool IsPlainBuff(AbstractIntent intent)
    {
        return intent.GetType() == typeof(BuffIntent);
    }

    private static bool IsPlainDebuff(AbstractIntent intent)
    {
        return intent.GetType() == typeof(DebuffIntent);
    }

    private static int ToBlockAmount(decimal blockAmount)
    {
        return Math.Max(0, (int)Math.Ceiling(blockAmount));
    }

    private static void Render(
        NCreature creatureNode,
        IReadOnlyList<AbstractIntent> displayIntents,
        IReadOnlyList<Creature> targets)
    {
        Control container = creatureNode.IntentContainer;
        float startOffset = creatureNode.GetHashCode() / 100f;

        for (int i = container.GetChildCount(); i < displayIntents.Count; i++)
        {
            container.AddChildSafely(NIntent.Create(startOffset + i * 0.3f));
        }

        for (int i = 0; i < displayIntents.Count; i++)
        {
            NIntent child = container.GetChild<NIntent>(i);
            child.Modulate = Colors.White;
            child.SetFrozen(isFrozen: false);
            child.UpdateIntent(displayIntents[i], targets, creatureNode.Entity);
        }

        foreach (Node extra in container.GetChildren().TakeLast(container.GetChildCount() - displayIntents.Count).ToArray())
        {
            container.RemoveChildSafely(extra);
            extra.QueueFreeSafely();
        }
    }
}

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.GetAssetPaths))]
internal static class CombinedIntentDisplayAssetPatch
{
    [HarmonyPostfix]
    private static void Postfix(EncounterModel __instance, ref IEnumerable<string> __result)
    {
        try
        {
            if (!__instance.MonstersWithSlots.Any(static entry =>
                    CombinedIntentDisplayPatch.ShouldPreloadFor(entry.Item1)))
            {
                return;
            }

            __result = __result.Concat(CombinedIntentDisplayPatch.AssetPaths).Distinct(StringComparer.Ordinal);
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "CombinedIntentDisplay.EncounterAssetPaths",
                exception);
        }
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AssetPaths), MethodType.Getter)]
internal static class CombinedIntentCombatRoomAssetPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<string> __result)
    {
        try
        {
            __result = __result.Concat(CombinedIntentDisplayPatch.AssetPaths).Distinct(StringComparer.Ordinal);
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "CombinedIntentDisplay.CombatRoomAssetPaths",
                exception);
        }
    }
}

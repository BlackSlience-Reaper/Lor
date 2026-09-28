using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.compat;
using LibraryOfRuina.powers.LittleRedMercenary;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.patches.LittleRedMercenary;

internal static class FocusOfAttentionTargeting
{
    public static Creature? GetFocusedTarget(IEnumerable<Creature>? candidates)
    {
        if (candidates == null)
        {
            return null;
        }

        Creature[] candidateArray = candidates.Where(creature => creature != null).ToArray();
        if (candidateArray.Length == 0)
        {
            return null;
        }

        return candidateArray
            .Where(IsValidFocusedTarget)
            .OrderBy(creature => creature.Side)
            .ThenBy(creature => creature.SlotName ?? string.Empty)
            .ThenBy(creature => creature.CombatId ?? uint.MaxValue)
            .ThenBy(creature => creature.ModelId.Entry)
            .FirstOrDefault();
    }

    public static Creature? GetFocusedTarget(CombatState? combatState)
    {
        return GetFocusedTarget(combatState?.Creatures);
    }

    // Replaces rng.NextItem at vanilla random-target call sites. The roll always uses the caller's
    // own candidate list so that, without a focused target, the result and RNG consumption are
    // exactly vanilla; a focused target only overrides the rolled result.
    public static Creature? ChooseFocusedTargetAfterRngRoll(Rng rng, IEnumerable<Creature> candidates)
    {
        IReadOnlyList<Creature> candidateList = candidates as IReadOnlyList<Creature> ?? candidates.ToList();
        Creature? rolled = rng.NextItem(candidateList);
        return GetFocusedTarget(candidateList) ?? rolled;
    }

    private static bool IsValidFocusedTarget(Creature creature)
    {
        return creature.IsAlive
            && creature.IsHittable
            && creature.HasPower<LibraryOfRuinaFocusOfAttentionPower>();
    }
}

[HarmonyPatch]
internal static class FocusOfAttentionRandomAttackPatch
{
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return FocusOfAttentionPatchTargets.GetAsyncMoveNext(
            typeof(AttackCommand),
            nameof(AttackCommand.Execute),
            [typeof(PlayerChoiceContext)]);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return FocusOfAttentionPatchTargets.ReplaceCreatureRngNextItem(instructions);
    }
}

[HarmonyPatch]
internal static class FocusOfAttentionLightningOrbPatch
{
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return FocusOfAttentionPatchTargets.GetAsyncMoveNext(
            typeof(LightningOrb),
            "ApplyLightningDamage",
            [typeof(decimal), typeof(Creature), typeof(PlayerChoiceContext), typeof(bool)]);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return FocusOfAttentionPatchTargets.ReplaceCreatureRngNextItem(instructions);
    }
}

[HarmonyPatch]
internal static class FocusOfAttentionBeatDownPatch
{
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return FocusOfAttentionPatchTargets.GetAsyncMoveNext(
            typeof(BeatDown),
            "OnPlay",
            [typeof(PlayerChoiceContext), typeof(CardPlay)]);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return FocusOfAttentionPatchTargets.ReplaceCreatureRngNextItem(instructions);
    }
}

[HarmonyPatch]
internal static class FocusOfAttentionBouncingFlaskPatch
{
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return FocusOfAttentionPatchTargets.GetAsyncMoveNext(
            typeof(BouncingFlask),
            "OnPlay",
            [typeof(PlayerChoiceContext), typeof(CardPlay)]);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return FocusOfAttentionPatchTargets.ReplaceCreatureRngNextItem(instructions);
    }
}

[LibraryPatch(
    Optional = true,
    Reason = "CardCmd.AutoPlay 的随机选敌改为尊重集火。目标是 async 状态机，签名随游戏版本变化；"
             + "Prepare 按 ResourceInfo.EnergySpent 的签名判断是否为适配的版本，不匹配时不安装。")]
internal static class FocusOfAttentionCardCmdAutoPlayPatch
{
    [HarmonyPrepare]
    public static bool Prepare()
    {
        try
        {
            MethodInfo? setter = AccessTools.PropertySetter(typeof(ResourceInfo), nameof(ResourceInfo.EnergySpent));
            if (setter == null)
                return false;

            ParameterInfo[] parameters = setter.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(int))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return FocusOfAttentionPatchTargets.GetAsyncMoveNext(
            typeof(CardCmd),
            nameof(CardCmd.AutoPlay),
            [
                typeof(PlayerChoiceContext),
                typeof(CardModel),
                typeof(Creature),
                typeof(AutoPlayType),
                typeof(bool),
                typeof(bool),
            ]);
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return FocusOfAttentionPatchTargets.ReplaceCreatureRngNextItem(instructions);
    }
}

[HarmonyPatch(typeof(DarkOrb), nameof(DarkOrb.Evoke))]
internal static class FocusOfAttentionDarkOrbEvokePatch
{
    private static readonly MethodInfo? _playEvokeSfxMethod =
        AccessTools.Method(typeof(OrbModel), "PlayEvokeSfx");

    [HarmonyPrefix]
    private static bool Prefix(
        DarkOrb __instance,
        PlayerChoiceContext playerChoiceContext,
        ref Task<IEnumerable<Creature>> __result)
    {
        Creature? focusedTarget = FocusOfAttentionTargeting.GetFocusedTarget(__instance.CombatState.HittableEnemies);
        if (focusedTarget == null)
        {
            return true;
        }

        __result = EvokeFocused(__instance, playerChoiceContext, focusedTarget);
        return false;
    }

    private static async Task<IEnumerable<Creature>> EvokeFocused(
        DarkOrb orb,
        PlayerChoiceContext playerChoiceContext,
        Creature focusedTarget)
    {
        _playEvokeSfxMethod?.Invoke(orb, null);
        await CreatureCmdCompat.Damage(
            playerChoiceContext,
            focusedTarget,
            orb.EvokeVal,
            ValueProp.Unpowered,
            orb.Owner.Creature);

        return [focusedTarget];
    }
}

internal static class FocusOfAttentionPatchTargets
{
    private static readonly MethodInfo ChooseFocusedTargetAfterRngRollMethod =
        AccessTools.Method(
            typeof(FocusOfAttentionTargeting),
            nameof(FocusOfAttentionTargeting.ChooseFocusedTargetAfterRngRoll))
        ?? throw new MissingMethodException(
            typeof(FocusOfAttentionTargeting).FullName,
            nameof(FocusOfAttentionTargeting.ChooseFocusedTargetAfterRngRoll));

    public static MethodBase GetAsyncMoveNext(Type type, string methodName, Type[] parameters)
    {
        MethodInfo method = AccessTools.Method(type, methodName, parameters)
            ?? throw new MissingMethodException(type.FullName, methodName);
        AsyncStateMachineAttribute? attribute = method.GetCustomAttribute<AsyncStateMachineAttribute>();
        Type stateMachineType = attribute?.StateMachineType
            ?? throw new MissingMethodException(type.FullName, methodName + " async state machine");
        return AccessTools.Method(stateMachineType, "MoveNext")
            ?? throw new MissingMethodException(stateMachineType.FullName, "MoveNext");
    }

    public static IEnumerable<CodeInstruction> ReplaceCreatureRngNextItem(IEnumerable<CodeInstruction> instructions)
    {
        int replacementCount = 0;

        foreach (CodeInstruction instruction in instructions)
        {
            if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
                && instruction.operand is MethodInfo method
                && IsCreatureRngNextItem(method))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = ChooseFocusedTargetAfterRngRollMethod;
                replacementCount++;
            }

            yield return instruction;
        }

        if (replacementCount == 0)
        {
            Log.Warn("LibraryOfRuina focus-of-attention patch did not find a Creature RNG target selection.");
        }
    }

    internal static bool IsCreatureRngNextItem(MethodInfo method)
    {
        return method.Name == nameof(Rng.NextItem)
            && method.DeclaringType == typeof(Rng)
            && method.IsGenericMethod
            && method.GetGenericArguments().Length == 1
            && method.GetGenericArguments()[0] == typeof(Creature);
    }
}

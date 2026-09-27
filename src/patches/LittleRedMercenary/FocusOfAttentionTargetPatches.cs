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

    public static Creature? ChooseFocusedTargetAfterRngRoll(Rng rng, IEnumerable<Creature> candidates)
    {
        Creature[] candidateArray = candidates
            .Where(c => c != null && c.IsAlive && c.IsHittable)
            .ToArray();

        if (candidateArray.Length == 0)
        {
            
            Creature? originalTarget = rng.NextItem(candidates);
            return GetFocusedTarget(candidates) ?? originalTarget;
        }

        Creature? originalTargetFiltered = rng.NextItem(candidateArray);
        return GetFocusedTarget(candidateArray) ?? originalTargetFiltered;
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

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
internal static class FocusOfAttentionCombatSetupPatch
{
    private static void Prefix()
    {
        // 首场战斗前所有模组均已加载，统一安装外部随机选敌补丁，避免加载顺序造成双端覆盖不同。
        FocusOfAttentionExternalCreatureRngPatches.Apply(new Harmony("FYY.LibraryOfRuina"));
    }
}

internal static class FocusOfAttentionExternalCreatureRngPatches
{
    private static readonly HarmonyMethod CreatureRngTranspiler = new(
        AccessTools.Method(
            typeof(FocusOfAttentionPatchTargets),
            nameof(FocusOfAttentionPatchTargets.ReplaceCreatureRngNextItem))
        ?? throw new MissingMethodException(
            typeof(FocusOfAttentionPatchTargets).FullName,
            nameof(FocusOfAttentionPatchTargets.ReplaceCreatureRngNextItem)));

    private static bool _applied;

    public static void Apply(Harmony harmony)
    {
        if (_applied)
        {
            return;
        }

        _applied = true;
        Assembly ownAssembly = typeof(FocusOfAttentionExternalCreatureRngPatches).Assembly;
        int patchedMethodCount = 0;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!ShouldScanAssembly(assembly, ownAssembly))
            {
                continue;
            }

            foreach (Type type in GetLoadableTypes(assembly))
            {
                foreach (MethodBase method in GetDeclaredMethods(type))
                {
                    if (!CallsCreatureRngNextItem(method))
                    {
                        continue;
                    }

                    harmony.Patch(method, transpiler: CreatureRngTranspiler);
                    patchedMethodCount++;
                }
            }
        }

        Log.Info($"[LibraryOfRuina] Focus of Attention patched {patchedMethodCount} external Creature RNG callsite(s).");
    }

    private static bool ShouldScanAssembly(Assembly assembly, Assembly ownAssembly)
    {
        if (assembly == ownAssembly || assembly.IsDynamic)
        {
            return false;
        }

        string name = assembly.GetName().Name ?? string.Empty;
        return name != "sts2"
            && name != "System.Private.CoreLib"
            && name != "netstandard"
            && !name.StartsWith("System.", StringComparison.Ordinal)
            && !name.StartsWith("Microsoft.", StringComparison.Ordinal)
            && !name.StartsWith("GodotSharp", StringComparison.Ordinal)
            && !name.StartsWith("Harmony", StringComparison.Ordinal)
            && !name.StartsWith("Mono.", StringComparison.Ordinal)
            && !name.StartsWith("MonoMod.", StringComparison.Ordinal);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(static type => type != null)!;
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<MethodBase> GetDeclaredMethods(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Static
            | BindingFlags.Instance
            | BindingFlags.DeclaredOnly;

        return type.GetMethods(flags)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(flags));
    }

    private static bool CallsCreatureRngNextItem(MethodBase method)
    {
        if (method.IsAbstract || method.ContainsGenericParameters)
        {
            return false;
        }

        try
        {
            // Android 运行时读取无 IL 方法体时可能直接抛出异常，必须与 IL 解析共用保护边界。
            if (method.GetMethodBody() == null)
            {
                return false;
            }

            return PatchProcessor.ReadMethodBody(method)
                .Any(instruction =>
                    instruction.Value is MethodInfo calledMethod
                    && FocusOfAttentionPatchTargets.IsCreatureRngNextItem(calledMethod));
        }
        catch
        {
            return false;
        }
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

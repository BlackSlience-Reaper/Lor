using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace LibraryOfRuina.patches;

[HarmonyPatch]
internal static class ModelIdSerializationCacheDuplicateWarningPatch
{
    private const string WarningText = "share an ID! This might break multiplayer.";

    private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];
    private static IReadOnlyList<MethodBase>? targetMethods;

    static ModelIdSerializationCacheDuplicateWarningPatch()
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            ushort value = unchecked((ushort)opCode.Value);
            if (value < 0x100)
            {
                OneByteOpCodes[value] = opCode;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
                TwoByteOpCodes[value & 0xff] = opCode;
            }
        }
    }

    [HarmonyPrepare]
    private static bool Prepare() => TargetComparerMethods.Count > 0;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods() => TargetComparerMethods;

    private static IReadOnlyList<MethodBase> TargetComparerMethods =>
        targetMethods ??= FindTargetComparerMethods().ToList();

    private static IEnumerable<MethodBase> FindTargetComparerMethods()
    {
        return CandidateTypes(typeof(ModelIdSerializationCache))
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Static
                | BindingFlags.Instance))
            .Where(static method =>
                method.ReturnType == typeof(int)
                && method.GetParameters().Length == 2
                && MethodLoadsWarningText(method));
    }

    private static IEnumerable<Type> CandidateTypes(Type type)
    {
        yield return type;

        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (Type candidate in CandidateTypes(nested))
            {
                yield return candidate;
            }
        }
    }

    private static bool Prefix(object?[] __args, ref int __result)
    {
        if (__args.Length < 2
            || !TryGetModelTuple(__args[0], out Type? leftType, out Mod? leftMod)
            || !TryGetModelTuple(__args[1], out Type? rightType, out Mod? rightMod))
        {
            return true;
        }

        if (!ReferenceEquals(leftType, rightType) || !ReferenceEquals(leftMod, rightMod))
        {
            return true;
        }

        __result = 0;
        return false;
    }

    private static bool TryGetModelTuple(object? argument, out Type? modelType, out Mod? mod)
    {
        if (argument is ValueTuple<Type, Mod?> tuple)
        {
            modelType = tuple.Item1;
            mod = tuple.Item2;
            return true;
        }

        modelType = null;
        mod = null;
        return false;
    }

    private static bool MethodLoadsWarningText(MethodBase method)
    {
        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null)
        {
            return false;
        }

        Module module = method.Module;
        int offset = 0;
        while (offset < il.Length)
        {
            OpCode opCode = ReadOpCode(il, ref offset);
            if (opCode.OperandType == OperandType.InlineString)
            {
                int token = BitConverter.ToInt32(il, offset);
                offset += 4;

                try
                {
                    if (module.ResolveString(token).Contains(WarningText, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                }

                continue;
            }

            if (opCode.OperandType == OperandType.InlineSwitch)
            {
                int count = BitConverter.ToInt32(il, offset);
                offset += 4 + count * 4;
                continue;
            }

            offset += GetOperandSize(opCode.OperandType);
        }

        return false;
    }

    private static OpCode ReadOpCode(byte[] il, ref int offset)
    {
        byte value = il[offset++];
        if (value != 0xfe)
        {
            return OneByteOpCodes[value];
        }

        return TwoByteOpCodes[il[offset++]];
    }

    private static int GetOperandSize(OperandType operandType) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget
            or OperandType.ShortInlineI
            or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8
            or OperandType.InlineR => 8,
        OperandType.InlineBrTarget
            or OperandType.InlineField
            or OperandType.InlineI
            or OperandType.InlineMethod
            or OperandType.InlineSig
            or OperandType.InlineTok
            or OperandType.InlineType
            or OperandType.ShortInlineR => 4,
        _ => 0
    };
}

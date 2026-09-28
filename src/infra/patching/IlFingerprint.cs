using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;

namespace LibraryOfRuina.infra.patching;

/// <summary>
/// 方法体的指纹，供原版拷贝守卫比对。原始 IL 里对其他成员的引用是元数据令牌，所在程序集增删任何成员都会让
/// 令牌整体偏移，逻辑没变哈希也会变；这里把令牌解析成成员全名、字符串常量再哈希，局部变量与异常块按类型名计入，
/// 分支偏移是方法内的相对量，原样保留。只依赖 System.Reflection，离线工具可以编译同一份源码做对照。
/// </summary>
internal static class IlFingerprint
{
    private static readonly OpCode[] OneByte = new OpCode[0x100];
    private static readonly OpCode[] TwoByte = new OpCode[0x100];

    static IlFingerprint()
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode code)
            {
                ushort value = unchecked((ushort)code.Value);
                if (value < 0x100)
                {
                    OneByte[value] = code;
                }
                else if ((value & 0xff00) == 0xfe00)
                {
                    TwoByte[value & 0xff] = code;
                }
            }
        }
    }

    public static string Hash(MethodBase method)
    {
        MethodBody? body = method.GetMethodBody();
        byte[]? il = body?.GetILAsByteArray();
        if (body == null || il == null)
        {
            return "no-il";
        }

        var text = new StringBuilder();
        foreach (LocalVariableInfo local in body.LocalVariables)
        {
            text.Append("local ").Append(TypeName(local.LocalType)).Append('\n');
        }

        foreach (ExceptionHandlingClause clause in body.ExceptionHandlingClauses)
        {
            text.Append("clause ").Append(clause.Flags).Append(' ')
                .Append(clause.TryOffset).Append(' ').Append(clause.TryLength).Append(' ')
                .Append(clause.HandlerOffset).Append(' ').Append(clause.HandlerLength).Append(' ')
                .Append(clause.Flags == ExceptionHandlingClauseOptions.Clause ? TypeName(clause.CatchType) : "")
                .Append('\n');
        }

        Type[]? typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
        Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        Module module = method.Module;
        int position = 0;
        while (position < il.Length)
        {
            OpCode code = il[position] == 0xfe ? TwoByte[il[++position]] : OneByte[il[position]];
            position++;
            text.Append(code.Name);
            switch (code.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    text.Append(' ').Append(il[position]);
                    position += 1;
                    break;
                case OperandType.InlineVar:
                    text.Append(' ').Append(BitConverter.ToUInt16(il, position));
                    position += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    text.Append(' ').Append(BitConverter.ToInt64(il, position));
                    position += 8;
                    break;
                case OperandType.InlineSwitch:
                    int count = BitConverter.ToInt32(il, position);
                    position += 4;
                    for (int i = 0; i < count; i++, position += 4)
                    {
                        text.Append(' ').Append(BitConverter.ToInt32(il, position));
                    }

                    break;
                case OperandType.InlineString:
                    text.Append(" \"").Append(module.ResolveString(BitConverter.ToInt32(il, position))).Append('"');
                    position += 4;
                    break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                    text.Append(' ').Append(MemberName(module.ResolveMember(BitConverter.ToInt32(il, position), typeArguments, methodArguments)));
                    position += 4;
                    break;
                case OperandType.InlineSig:
                    // calli 的签名；本模组守卫的方法里没有，按原始签名字节记。
                    text.Append(" sig:").Append(Convert.ToHexString(module.ResolveSignature(BitConverter.ToInt32(il, position))));
                    position += 4;
                    break;
                default:
                    // InlineBrTarget、InlineI、ShortInlineR：方法内的相对量或常量。
                    text.Append(' ').Append(BitConverter.ToInt32(il, position));
                    position += 4;
                    break;
            }

            text.Append('\n');
        }

        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    private static string MemberName(MemberInfo? member) => member switch
    {
        null => "?",
        Type type => TypeName(type),
        MethodBase method => TypeName(method.DeclaringType) + "::" + method.Name
                             + (method.IsGenericMethod ? "<" + string.Join(",", method.GetGenericArguments().Select(TypeName)) + ">" : "")
                             + "(" + string.Join(",", method.GetParameters().Select(p => TypeName(p.ParameterType))) + ")"
                             + (method is MethodInfo info ? ":" + TypeName(info.ReturnType) : ""),
        FieldInfo field => TypeName(field.DeclaringType) + "::" + field.Name + ":" + TypeName(field.FieldType),
        _ => member.ToString() ?? "?",
    };

    // 类型身份带程序集简单名（两个程序集里同名的类型是不同的调用目标），递归处理泛型实参、数组、引用与指针；
    // 不带程序集版本、MVID 或令牌，这些会随构建变化。
    private static string TypeName(Type? type)
    {
        if (type == null)
        {
            return "?";
        }

        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod != null ? "!!" : "!") + type.GenericParameterPosition;
        }

        if (type.HasElementType)
        {
            string element = TypeName(type.GetElementType());
            return type.IsArray ? element + "[" + new string(',', type.GetArrayRank() - 1) + "]"
                : type.IsByRef ? element + "&"
                : type.IsPointer ? element + "*"
                : element;
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            return TypeName(type.GetGenericTypeDefinition()) + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
        }

        return "[" + type.Assembly.GetName().Name + "]" + (type.FullName ?? type.Name);
    }
}

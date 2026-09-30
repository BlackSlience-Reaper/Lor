using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 核对 <see cref="CombatStateProperties.From"/> 与原版 <c>SavedProperties.From</c> 的取值规则一致。
/// 对 ModelDb 里每个用到上表属性的模型各取一个可变实例，先按缺省值、再把表里能写的属性写成非缺省值，各打一次包，
/// 输出 <c>ROW|类型|default/set|JSON</c>。
/// <list type="bullet">
/// <item>属性仍带 <c>[SavedProperty]</c> 的构建（去掉特性之前）：原版 <c>From</c> 与本表 <c>From</c> 的 JSON 必须逐字相同。</item>
/// <item>去掉特性之后：原版 <c>From</c> 对这些模型必须返回 null，类型必须是 <see cref="CombatStateProperties.IsTransient"/>。</item>
/// </list>
/// 同一个验证程序集配去掉特性前后的主模组各跑一次，ROW 行应逐行相同。参数 <c>lor-verify-combat-state-properties</c>。
/// </summary>
internal static class CombatStatePropertiesVerificationPatch
{
    private const string VerifyArg = "lor-verify-combat-state-properties";
    private const string LogPrefix = "[LibraryOfRuina.CombatStateProperties.Verify] ";
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(Run).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(argument => string.Equals(
            argument.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static void Run()
    {
        try
        {
            HashSet<Type> declaring = CombatStateProperties.All.Select(static entry => entry.DeclaringType).ToHashSet();
            AbstractModel[] models = ModelDb.All
                .Where(model => Hierarchy(model.GetType()).Any(declaring.Contains))
                .OrderBy(static model => model.GetType().FullName, StringComparer.Ordinal)
                .ToArray();
            int attributed = 0;
            int written = 0;
            foreach (AbstractModel canonical in models)
            {
                AbstractModel model = canonical switch
                {
                    MonsterModel monster => monster.ToMutable(),
                    PowerModel power => power.ToMutable(),
                    EncounterModel encounter => encounter.ToMutable(),
                    _ => throw new InvalidOperationException(canonical.GetType().FullName + " is not a monster, power or encounter.")
                };
                Type type = model.GetType();
                bool hasAttribute = type.GetProperties(InstanceFlags)
                    .Any(static property => property.GetCustomAttribute<SavedPropertyAttribute>() != null);
                attributed += hasAttribute ? 1 : 0;
                Compare(model, type, "default", hasAttribute);
                written += WriteNonDefaults(model);
                Compare(model, type, "set", hasAttribute);
            }

            if (models.Length == 0 || attributed != 0 && attributed != models.Length)
            {
                throw new InvalidOperationException(
                    $"expected all or none of {models.Length} models to carry [SavedProperty], got {attributed}.");
            }

            Log.Info(LogPrefix + $"COMBAT_STATE_PROPERTIES_OK models={models.Length} attributed={attributed} "
                     + $"written={written} table={CombatStateProperties.All.Count()}");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "COMBAT_STATE_PROPERTIES_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void Compare(AbstractModel model, Type type, string label, bool hasAttribute)
    {
        string ours = JsonSerializer.Serialize(CombatStateProperties.From(model), Json);
        string vanilla = JsonSerializer.Serialize(SavedProperties.From(model), Json);
        Log.Info("ROW|" + type.FullName + "|" + label + "|" + ours);
        if (hasAttribute)
        {
            if (ours != vanilla)
            {
                throw new InvalidOperationException(
                    $"{type.Name} {label}: table {ours} differs from SavedProperties.From {vanilla}.");
            }
        }
        else if (vanilla != "null" || !CombatStateProperties.IsTransient(type))
        {
            throw new InvalidOperationException($"{type.Name} {label}: still saved by the game ({vanilla}).");
        }
    }

    // 把表里的属性写成非缺省值（整数 7、布尔 true、字符串 "x"、整数数组 [1, 2]、枚举取第一个非零值），
    // 让 SaveIfNotTypeDefault 与 AlwaysSave 两种条件都走到“要写”的分支。设置器抛错的属性跳过。
    private static int WriteNonDefaults(AbstractModel model)
    {
        int count = 0;
        foreach ((Type declaringType, string name) in CombatStateProperties.All
                     .Where(entry => entry.DeclaringType.IsInstanceOfType(model))
                     .OrderBy(static entry => entry.Name, StringComparer.Ordinal))
        {
            PropertyInfo property = declaringType.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly)!;
            object? value = property.PropertyType switch
            {
                { } t when t == typeof(int) => 7,
                { } t when t == typeof(bool) => true,
                { } t when t == typeof(string) => "x",
                { } t when t == typeof(int[]) => new[] { 1, 2 },
                { IsEnum: true } t => Enum.GetValues(t).Cast<object>().FirstOrDefault(static item => Convert.ToInt64(item) != 0),
                _ => null
            };
            if (value == null || property.SetMethod == null)
            {
                continue;
            }

            try
            {
                property.SetValue(model, value);
                count++;
            }
            catch (TargetInvocationException)
            {
            }
        }

        return count;
    }

    private static IEnumerable<Type> Hierarchy(Type type)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            yield return current;
        }
    }
}

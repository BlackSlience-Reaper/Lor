using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.ui.DamagePreview;

/// <summary>
/// 在独立字段存储中解释实际监听者的 Hook IL。字段写入、集合修改与异步状态机均留在预览内；
/// 战斗 Cmd 交给模拟器，表现 Cmd 跳过，未知外部调用终止该来源。不会执行原模型的事件或异步任务。
/// </summary>
internal sealed class IncomingDamagePreviewHookReader(IncomingDamageSimulation simulation)
{
    // 单次来源的指令与递归上限，限制复杂模组钩子的预览耗时。
    private const int InstructionBudget = 20000;
    private const int RecursionLimit = 48;
    // 一次完整预测最多解释的指令数，防止复杂钩子阻塞每帧 UI。
    private const int TotalInstructionBudget = 250000;
    // 回合副作用最多占用一半预算，保留数值查询预算，避免无关卡牌回调挤掉已知攻击。
    private const int EffectInstructionBudget = TotalInstructionBudget / 2;
    private static readonly Dictionary<MethodBase, MethodPlan> Plans = [];
    private static readonly Dictionary<(Type Type, MethodInfo Method), MethodInfo> VirtualMethods = [];
    private static readonly Dictionary<MethodBase, ParameterInfo[]> Parameters = [];
    private readonly Dictionary<(object Owner, FieldInfo Field), object?> _fields = new(FieldKeyComparer.Instance);
    private readonly Dictionary<object, HashSet<FieldInfo>> _fieldsByOwner = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, object> _collections = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, object?> _ambientValues = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _ownedCollections = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Rng, Rng> _previewRngs = new(ReferenceEqualityComparer.Instance);
    private readonly PlayerChoiceContext _previewChoiceContext = new ThrowingPlayerChoiceContext();
    private int _remaining;
    private int _totalRemaining = TotalInstructionBudget;
    private int _effectRemaining = EffectInstructionBudget;
    private int _depth;
    private AbstractModel? _source;
    private bool _isReadingQuery;
    private Dictionary<(object Owner, FieldInfo Field), (bool Exists, object? Value)>? _queryWrites;

    internal bool IsReadingQuery => _isReadingQuery;

    internal sealed class Reference(Func<object?> read, Action<object?> write)
    {
        internal object? Value { get => read(); set => write(value); }
    }

    private sealed record PreviewTask(object? Result);

    private sealed record PreviewDelegate(object? Target, MethodInfo Method);

    /// <summary>同参数数量的目标列表/单体等重载，按实际参数类型选择。</summary>
    internal static MethodInfo ResolveHookOverload(Type type, string name, object?[] arguments)
    {
        return type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == name && !method.ContainsGenericParameters
                && method.GetParameters().Length == arguments.Length
                && method.GetParameters().Select((parameter, index) =>
                {
                    Type parameterType = parameter.ParameterType;
                    object? argument = arguments[index];
                    if (parameterType.IsByRef)
                    {
                        return argument is Reference;
                    }

                    if (argument == null)
                    {
                        return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null;
                    }

                    return parameterType.IsInstanceOfType(argument);
                }).All(static matches => matches));
    }

    internal bool TryInvoke(AbstractModel source, MethodInfo method, params object?[] arguments)
        => TryExecute(source, method, () => Invoke(method, source, arguments));

    internal bool TryExecute(AbstractModel source, MethodInfo method, Action action, bool isQuery = false)
    {
        if (_totalRemaining <= 0 || !isQuery && !_isReadingQuery && _effectRemaining <= 0)
        {
            simulation.RecordUnsupportedHook(source, method, "Preview instruction budget exhausted");
            return false;
        }

        var fieldsBefore = isQuery ? null : new Dictionary<(object, FieldInfo), object?>(_fields, FieldKeyComparer.Instance);
        var previousWrites = _queryWrites;
        if (isQuery)
        {
            _queryWrites = new(FieldKeyComparer.Instance);
        }
        var copiedCollections = isQuery ? null : new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        object SnapshotCollection(object collection)
        {
            if (!copiedCollections!.TryGetValue(collection, out object? copy))
            {
                copiedCollections[collection] = copy = CopyCollection(collection);
            }

            return copy;
        }

        var collectionsBefore = isQuery ? null : _collections.ToDictionary(static pair => pair.Key,
            pair => SnapshotCollection(pair.Value), ReferenceEqualityComparer.Instance);
        var ambientBefore = new Dictionary<object, object?>(_ambientValues, ReferenceEqualityComparer.Instance);
        var rngsBefore = _previewRngs.Count == 0 ? null : _previewRngs.ToDictionary(
            static pair => pair.Key, static pair => pair.Value.ToSerializable());
        Action? restore = isQuery ? null : simulation.CaptureState();
        AbstractModel? previous = _source;
        int previousRemaining = _remaining;
        int startingTotal = _totalRemaining;
        bool previousQuery = _isReadingQuery;
        _isReadingQuery = isQuery || previousQuery;
        _source = source;
        _remaining = InstructionBudget;
        try
        {
            action();
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException and not AccessViolationException)
        {
            if (fieldsBefore != null)
            {
                _fields.Clear();
                foreach (var entry in fieldsBefore)
                {
                    _fields.Add(entry.Key, entry.Value);
                }
            }

            if (collectionsBefore != null)
            {
                _collections.Clear();
                foreach (var entry in collectionsBefore)
                {
                    _collections.Add(entry.Key, entry.Value);
                }

                foreach (object copy in collectionsBefore.Values)
                {
                    _collections.TryAdd(copy, copy);
                }
            }

            _ambientValues.Clear();
            foreach (var entry in ambientBefore)
            {
                _ambientValues.Add(entry.Key, entry.Value);
            }

            restore?.Invoke();
            _previewRngs.Clear();
            if (rngsBefore != null)
            {
                foreach (var entry in rngsBefore)
                {
                    _previewRngs[entry.Key] = new Rng(entry.Value);
                }
            }

            simulation.RecordUnsupportedHook(source, method, error.Message);
            return false;
        }
        finally
        {
            _source = previous;
            _isReadingQuery = previousQuery;
            if (isQuery)
            {
                foreach (var entry in _queryWrites!)
                {
                    if (entry.Value.Exists)
                    {
                        _fields[entry.Key] = entry.Value.Value;
                    }
                    else
                    {
                        _fields.Remove(entry.Key);
                    }
                }
            }

            _queryWrites = previousWrites;
            // 嵌套的 Cmd/后置回调共享外层预算，返回后恢复外层剩余量。
            _remaining = previous == null ? previousRemaining
                : Math.Max(0, previousRemaining - (startingTotal - _totalRemaining));
        }
    }

    internal object? Invoke(MethodBase method, object? instance, object?[] arguments, bool dispatchVirtual = true)
    {
        if (method.DeclaringType?.FullName?.StartsWith("System.Nullable`", StringComparison.Ordinal) == true)
        {
            object? value = Dereference(instance);
            switch (method.Name)
            {
                case "get_HasValue": return value != null;
                case "get_Value":
                    return Coerce(value ?? throw new InvalidOperationException("Nullable object must have a value."),
                        method.DeclaringType.GetGenericArguments()[0]);
                case "GetValueOrDefault":
                    return Coerce(value ?? arguments.FirstOrDefault()
                        ?? DefaultValue(method.DeclaringType.GetGenericArguments()[0]), method.DeclaringType.GetGenericArguments()[0]);
                case ".ctor" when instance is Reference reference:
                    reference.Value = Dereference(arguments[0]);
                    return null;
            }
        }

        instance = Dereference(instance);
        // IL 的 call（尤其 base 调用）保持声明实现，只有 callvirt 才重新分派。
        if (dispatchVirtual && method is MethodInfo { IsVirtual: true } virtualMethod && instance != null)
        {
            var key = (instance.GetType(), virtualMethod);
            if (!VirtualMethods.TryGetValue(key, out MethodInfo? implementation))
            {
                if (IsSystemCollection(instance.GetType()))
                {
                    implementation = virtualMethod;
                }
                else if (virtualMethod.DeclaringType?.IsInterface == true)
                {
                    InterfaceMapping mapping = instance.GetType().GetInterfaceMap(virtualMethod.DeclaringType);
                    MethodInfo definition = virtualMethod.IsGenericMethod ? virtualMethod.GetGenericMethodDefinition() : virtualMethod;
                    int index = Array.IndexOf(mapping.InterfaceMethods, definition);
                    implementation = index >= 0 ? mapping.TargetMethods[index] : virtualMethod;
                }
                else
                {
                    MethodInfo definition = virtualMethod.GetBaseDefinition();
                    implementation = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(candidate => candidate.GetBaseDefinition() == definition) ?? virtualMethod;
                }

                if (implementation.IsGenericMethodDefinition && virtualMethod.IsGenericMethod)
                {
                    implementation = implementation.MakeGenericMethod(virtualMethod.GetGenericArguments());
                }

                VirtualMethods[key] = implementation;
            }

            method = implementation;
        }

        string name = method.Name;
        Type? type = method.DeclaringType;
        if (type != null && typeof(PlayerChoiceContext).IsAssignableFrom(type)
            && name is "PushModel" or "PopModel")
        {
            // 预测不进入真实动作/选择栈；null choiceContext 同样允许这两个纯上下文操作。
            return null;
        }

        if (type != null && typeof(ArgumentException).IsAssignableFrom(type)
            && method.IsStatic && name.StartsWith("ThrowIf", StringComparison.Ordinal))
        {
            return InvokeTrusted(method, instance, arguments);
        }

        if (instance is Rng rng && method.Name is "NextInt" or "NextFloat" or "NextDouble" or "NextBool" or "NextItem")
        {
            if (_isReadingQuery)
            {
                throw new UnsupportedHookException(method);
            }

            // 从快照创建独立 RNG，预览的随机选择绝不推进真实 RunRng。
            if (!_previewRngs.TryGetValue(rng, out Rng? previewRng))
            {
                _previewRngs[rng] = previewRng = new Rng(rng.ToSerializable());
            }

            return InvokeTrusted(method, previewRng, arguments);
        }

        if (type?.FullName is "System.Threading.Lock" or "System.Threading.Lock+Scope" or "System.Threading.Monitor")
        {
            return name switch
            {
                "EnterScope" => new PreviewLockScope(),
                "get_IsHeldByCurrentThread" or "IsEntered" => true,
                _ => null
            };
        }
        // 数值 Hook 的 trace transpiler 已由 Invoke 返回处记录，预览中无需再次解释表现记录器。
        if (type == typeof(DamagePreviewTrace) && name == nameof(DamagePreviewTrace.Record))
        {
            return null;
        }

        ParameterInfo[] argumentTypes = GetParameters(method);
        object?[] values = new object?[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            // IL 中 enum/bool 使用整数栈槽，在进入命令模拟前恢复声明类型。
            values[i] = argumentTypes[i].ParameterType.IsByRef ? arguments[i]
                : Coerce(Dereference(arguments[i]), argumentTypes[i].ParameterType);
            if (values[i] == null && typeof(PlayerChoiceContext).IsAssignableFrom(argumentTypes[i].ParameterType))
            {
                values[i] = _previewChoiceContext;
            }
        }

        // 方法体读取同一套已规范化的参数，包括无交互的预览 choiceContext。
        arguments = values;
        if (simulation.TryInterpretCommand(method, instance, values, _source, out object? commandResult))
        {
            return method is MethodInfo info && typeof(Task).IsAssignableFrom(info.ReturnType)
                ? new PreviewTask(commandResult)
                : commandResult;
        }

        if (name is "GetAwaiter" or "ConfigureAwait" && instance is PreviewTask)
        {
            return instance;
        }

        if (instance is PreviewTask task)
        {
            return name switch
            {
                "get_IsCompleted" => true,
                "GetResult" => task.Result,
                _ => throw new UnsupportedHookException(method)
            };
        }

        if (type?.Namespace == "System.Runtime.CompilerServices"
            && type.Name.StartsWith("Async", StringComparison.Ordinal))
        {
            if (name == "SetException")
            {
                throw new UnsupportedHookException(method);
            }

            return null;
        }

        if (type == typeof(Task))
        {
            return name switch
            {
                "get_CompletedTask" or "Delay" or "WhenAll" => new PreviewTask(null),
                "FromResult" => new PreviewTask(values[0]),
                _ => throw new UnsupportedHookException(method)
            };
        }

        if (name == "Invoke" && instance is PreviewDelegate callback)
        {
            return Invoke(callback.Method, callback.Target, arguments);
        }

        if (instance is Delegate originalCallback && name == "Invoke")
        {
            return Invoke(originalCallback.Method, originalCallback.Target, arguments);
        }

        if (typeof(Delegate).IsAssignableFrom(type) && name == "Invoke")
        {
            // 模型事件只负责通知真实宿主，预览不调用订阅者。
            return null;
        }

        if (name is "AssertMutable" or "AssertCanonical" or "Flash" or "InvokeDisplayAmountChanged"
            or "InvokeExecutionFinished" && typeof(AbstractModel).IsAssignableFrom(type))
        {
            return null;
        }

        // 任何场景/UI 调用、日志及动画音效均不进入真实宿主。
        string fullName = type?.FullName ?? "";
        if (method is MethodInfo visualQuery && typeof(Godot.GodotObject).IsAssignableFrom(visualQuery.ReturnType))
        {
            return null;
        }

        if (fullName.StartsWith("System.Threading.AsyncLocal`", StringComparison.Ordinal) && instance != null)
        {
            if (name == "set_Value")
            {
                _ambientValues[instance] = values[0];
                return null;
            }

            if (name == "get_Value")
            {
                return _ambientValues.TryGetValue(instance, out object? ambient) ? ambient
                    : InvokeTrusted(method, instance, arguments);
            }
        }

        if (fullName == "Godot.Mathf" && name is "Clamp" or "Min" or "Max" or "Abs" or "CeilToInt" or "FloorToInt" or "RoundToInt")
        {
            return InvokeTrusted(method, instance, arguments);
        }

        if (fullName.StartsWith("Godot.", StringComparison.Ordinal)
            || fullName.Contains(".Nodes.", StringComparison.Ordinal)
            || fullName.Contains(".Logging.", StringComparison.Ordinal)
            || type?.Name is "SfxCmd" or "VfxCmd")
        {
            return DefaultReturn(method);
        }

        if (type?.Name == "Cmd" && (name.Contains("Wait", StringComparison.Ordinal) || name.Contains("Anim", StringComparison.Ordinal)))
        {
            return new PreviewTask(null);
        }

        if (type == typeof(Enumerable))
        {
            return EnumerableCall((MethodInfo)method, values);
        }

        if (instance is IEnumerator enumerator && IsSystemCollection(instance.GetType()))
        {
            return name switch
            {
                "MoveNext" => enumerator.MoveNext(),
                "get_Current" => enumerator.Current,
                "Dispose" => null,
                _ => throw new UnsupportedHookException(method)
            };
        }

        if (instance is IEnumerable sequence && name == "GetEnumerator" && IsSystemCollection(instance.GetType()))
        {
            return ((IEnumerable)GetCollection(sequence)).GetEnumerator();
        }

        if (instance is Array array)
        {
            if (name == "get_Count")
            {
                return array.Length;
            }

            if (name == "get_Item")
            {
                return array.GetValue(Convert.ToInt32(values[0]));
            }
        }

        if (type == typeof(decimal) || type == typeof(Math) || type == typeof(MathF)
            || type == typeof(Convert) || type == typeof(string) || type == typeof(Enum)
            || type == typeof(Type) || type == typeof(RuntimeTypeHandle)
            || instance is Type && name.StartsWith("get_", StringComparison.Ordinal)
            || type?.IsPrimitive == true
            || fullName.StartsWith("System.Nullable`", StringComparison.Ordinal))
        {
            return InvokeTrusted(method, instance, arguments);
        }

        if ((instance is IList or IDictionary || type?.Namespace == "System.Collections.Generic")
            && (instance == null || IsSystemCollection(instance.GetType())))
        {
            object? collection = instance == null ? null : GetCollection(instance);
            if (_isReadingQuery && collection != null && !_ownedCollections.Contains(collection)
                && !name.StartsWith("get_", StringComparison.Ordinal)
                && name is not ("Contains" or "ContainsKey" or "TryGetValue" or "GetEnumerator"))
            {
                throw new UnsupportedHookException(method);
            }
            if (instance != null && ReferenceEquals(instance, collection)
                && !_ownedCollections.Contains(instance)
                && !_collections.Any(pair => !ReferenceEquals(pair.Key, pair.Value) && ReferenceEquals(pair.Value, instance))
                && !name.StartsWith("get_", StringComparison.Ordinal)
                && name is not ("Contains" or "ContainsKey" or "TryGetValue"))
            {
                throw new UnsupportedHookException(method);
            }

            return InvokeTrusted(method, collection, arguments);
        }

        if (type == typeof(object))
        {
            return name switch
            {
                ".ctor" => null,
                "GetType" => instance?.GetType(),
                "ReferenceEquals" => ReferenceEquals(values[0], values[1]),
                "Equals" => values.Length == 2 ? Equals(values[0], values[1]) : Equals(instance, values[0]),
                _ => throw new UnsupportedHookException(method)
            };
        }

        if (type?.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
        {
            throw new UnsupportedHookException(method);
        }

        // Model、基础库及模组方法均解释 IL，禁止通过反射调用它们的真实方法。
        if (method.GetCustomAttribute<AsyncStateMachineAttribute>() is { } asyncMethod)
        {
            Type stateType = asyncMethod.StateMachineType;
            if (stateType.ContainsGenericParameters && method is MethodInfo generic)
            {
                stateType = stateType.MakeGenericType(generic.GetGenericArguments());
            }

            object state = RuntimeHelpers.GetUninitializedObject(stateType);
            foreach (FieldInfo field in stateType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.Name == "<>1__state")
                {
                    WriteField(state, field, -1);
                }
                else if (field.Name == "<>4__this")
                {
                    WriteField(state, field, instance);
                }
                else
                {
                    int index = Array.FindIndex(method.GetParameters(), parameter => parameter.Name == field.Name);
                    if (index >= 0)
                    {
                        WriteField(state, field, values[index]);
                    }
                }
            }

            object? previousResult = _asyncResult;
            _asyncResult = null;
            try
            {
                Execute(AccessTools.Method(stateType, "MoveNext"), state, []);
                return new PreviewTask(_asyncResult);
            }
            finally
            {
                _asyncResult = previousResult;
            }
        }

        object? evaluated = Execute(method, instance, arguments);
        if (instance is AbstractModel model && evaluated is decimal numeric
            && name.StartsWith("Modify", StringComparison.Ordinal))
        {
            int amountIndex = Array.FindIndex(method.GetParameters(), static parameter => parameter.Name == "amount");
            decimal input = amountIndex >= 0 ? Convert.ToDecimal(values[amountIndex]) : numeric;
            DamagePreviewTrace.Record(model, input, numeric, name);
        }

        return evaluated;
    }

    private object? _asyncResult;

    private object? Execute(MethodBase method, object? instance, object?[] supplied)
    {
        if (++_depth > RecursionLimit)
        {
            _depth--;
            throw new UnsupportedHookException(method);
        }

        try
        {
            if (!Plans.TryGetValue(method, out MethodPlan? plan))
            {
                if (method.GetMethodBody() == null)
                {
                    throw new UnsupportedHookException(method);
                }

                plan = CreatePlan(method);
                Plans.Add(method, plan);
            }

            List<CodeInstruction> code = plan.Code;
            Dictionary<Label, int> labels = plan.Labels;
            List<(int Start, int Finally, int End)> finallyRegions = plan.FinallyRegions;

            object?[] args = method.IsStatic ? supplied : new[] { instance }.Concat(supplied).ToArray();
            object?[] locals = plan.LocalTypes.Select(DefaultValue).ToArray();
            var stack = new Stack<object?>();
            var pendingLeaves = new Stack<Queue<int>>();
            for (int pc = 0; pc < code.Count; pc++)
            {
                if (--_remaining < 0 || --_totalRemaining < 0 || !_isReadingQuery && --_effectRemaining < 0)
                {
                    throw new UnsupportedHookException(method);
                }

                CodeInstruction instruction = code[pc];
                string op = instruction.opcode.Name!;
                object? operand = instruction.operand;
                switch (op)
                {
                    case "nop": case "constrained.": case "readonly.": case "tail.": case "volatile.":
                        break;
                    case "ldnull": stack.Push(null); break;
                    case "ldstr": stack.Push(operand); break;
                    case "dup": stack.Push(stack.Peek()); break;
                    case "pop": stack.Pop(); break;
                    case "ldarg.0": case "ldarg.1": case "ldarg.2": case "ldarg.3":
                        stack.Push(args[op[^1] - '0']); break;
                    case "ldarg": case "ldarg.s": stack.Push(args[Convert.ToInt32(operand)]); break;
                    case "ldarga": case "ldarga.s":
                    {
                        int index = Convert.ToInt32(operand);
                        stack.Push(new Reference(() => Dereference(args[index]), value => args[index] = value));
                        break;
                    }
                    case "starg": case "starg.s": args[Convert.ToInt32(operand)] = stack.Pop(); break;
                    case "ldloc.0": case "ldloc.1": case "ldloc.2": case "ldloc.3":
                        stack.Push(locals[op[^1] - '0']); break;
                    case "ldloc": case "ldloc.s": stack.Push(locals[LocalIndex(operand)]); break;
                    case "stloc.0": case "stloc.1": case "stloc.2": case "stloc.3":
                        locals[op[^1] - '0'] = stack.Pop(); break;
                    case "stloc": case "stloc.s": locals[LocalIndex(operand)] = stack.Pop(); break;
                    case "ldloca": case "ldloca.s":
                    {
                        int index = LocalIndex(operand);
                        stack.Push(new Reference(() => locals[index], value => locals[index] = value));
                        break;
                    }
                    case "ldc.i4.m1": stack.Push(-1); break;
                    case "ldc.i4.0": case "ldc.i4.1": case "ldc.i4.2": case "ldc.i4.3":
                    case "ldc.i4.4": case "ldc.i4.5": case "ldc.i4.6": case "ldc.i4.7": case "ldc.i4.8":
                        stack.Push(op[^1] - '0'); break;
                    case "ldc.i4": case "ldc.i4.s": case "ldc.i8": case "ldc.r4": case "ldc.r8":
                        stack.Push(operand); break;
                    case "ldfld": case "ldsfld":
                        stack.Push(ReadField(op == "ldsfld" ? null : Dereference(stack.Pop()), (FieldInfo)operand!)); break;
                    case "ldflda": case "ldsflda":
                    {
                        object? owner = op == "ldsflda" ? null : Dereference(stack.Pop());
                        FieldInfo field = (FieldInfo)operand!;
                        stack.Push(new Reference(() => ReadField(owner, field), value => WriteField(owner, field, value)));
                        break;
                    }
                    case "stfld": case "stsfld":
                    {
                        object? value = Dereference(stack.Pop());
                        WriteField(op == "stsfld" ? null : Dereference(stack.Pop()), (FieldInfo)operand!, value);
                        break;
                    }
                    case "initobj": ((Reference)stack.Pop()!).Value = DefaultValue((Type)operand!); break;
                    case "ldobj": stack.Push(Dereference(stack.Pop())); break;
                    case "stobj": case "stind.ref": case "stind.i4": case "stind.i8":
                    {
                        object? value = stack.Pop();
                        ((Reference)stack.Pop()!).Value = value is IList or IDictionary ? GetCollection(value) : value;
                        break;
                    }
                    case "ldind.ref": case "ldind.i4": case "ldind.i8": stack.Push(Dereference(stack.Pop())); break;
                    case "box":
                        stack.Push(Coerce(stack.Pop(), (Type)operand!)); break;
                    case "unbox.any": case "castclass": break;
                    case "isinst":
                    {
                        object? value = Dereference(stack.Pop());
                        stack.Push(value != null && ((Type)operand!).IsInstanceOfType(value) ? value : null);
                        break;
                    }
                    case "ldtoken":
                        stack.Push(operand is Type tokenType ? tokenType.TypeHandle : operand); break;
                    case "ldftn": case "ldvirtftn": stack.Push(operand); break;
                    case "call": case "callvirt": case "newobj":
                    {
                        MethodBase called = (MethodBase)operand!;
                        object?[] parameters = new object?[GetParameters(called).Length];
                        for (int i = parameters.Length - 1; i >= 0; i--)
                        {
                            parameters[i] = stack.Pop();
                        }

                        object? receiver = null;
                        object? result;
                        if (op == "newobj")
                        {
                            Type constructed = called.DeclaringType!;
                            if (typeof(Godot.GodotObject).IsAssignableFrom(constructed))
                            {
                                // 预览只模拟战斗值，场景对象由真实战斗创建。
                                result = null;
                            }
                            else if (typeof(Delegate).IsAssignableFrom(constructed))
                            {
                                result = new PreviewDelegate(parameters[0], (MethodInfo)parameters[1]!);
                            }
                            else if (constructed == typeof(decimal) || constructed == typeof(object)
                                || IsSystemCollection(constructed)
                                || constructed.FullName?.StartsWith("System.Nullable`", StringComparison.Ordinal) == true)
                            {
                                ParameterInfo[] constructorParameters = called.GetParameters();
                                result = ((ConstructorInfo)called).Invoke(parameters.Select((parameter, index) =>
                                {
                                    object? value = Dereference(parameter);
                                    if (value is IList or IDictionary)
                                    {
                                        value = GetCollection(value);
                                    }

                                    return Coerce(value, constructorParameters[index].ParameterType);
                                }).ToArray());
                            }
                            else if (constructed.Namespace?.StartsWith("System", StringComparison.Ordinal) != true)
                            {
                                result = RuntimeHelpers.GetUninitializedObject(constructed);
                                Invoke(called, result, parameters);
                                // 新闭包/记录对象属于预览，初始化其字段，供原生集合的 Equals/枚举读取。
                                MaterializeFields(result);
                            }
                            else
                            {
                                throw new UnsupportedHookException(called);
                            }
                        }
                        else
                        {
                            if (!called.IsStatic)
                            {
                                receiver = stack.Pop();
                            }

                            if (called.Name == "SetResult" && called.DeclaringType?.Name.StartsWith("Async", StringComparison.Ordinal) == true)
                            {
                                _asyncResult = parameters.FirstOrDefault();
                            }

                            result = Invoke(called, receiver, parameters, dispatchVirtual: op == "callvirt");
                        }

                        if (op == "newobj" || called is MethodInfo { ReturnType: var returnType } && returnType != typeof(void))
                        {
                            if (op == "newobj" && result != null && IsSystemCollection(result.GetType()))
                            {
                                OwnCollection(result);
                            }

                            stack.Push(result);
                        }

                        break;
                    }
                    case "br": case "br.s":
                        pc = labels[(Label)operand!] - 1; break;
                    case "leave": case "leave.s":
                    {
                        int target = labels[(Label)operand!];
                        int position = pc;
                        var route = new Queue<int>(finallyRegions
                            .Where(region => region.Start <= position && position < region.Finally
                                && (target < region.Start || target > region.End))
                            .OrderByDescending(static region => region.Start)
                            .Select(static region => region.Finally));
                        route.Enqueue(target);
                        pc = route.Dequeue() - 1;
                        if (route.Count > 0)
                        {
                            pendingLeaves.Push(route);
                        }

                        stack.Clear();
                        break;
                    }
                    case "endfinally":
                    {
                        Queue<int> route = pendingLeaves.Peek();
                        pc = route.Dequeue() - 1;
                        if (route.Count == 0)
                        {
                            pendingLeaves.Pop();
                        }

                        break;
                    }
                    case "brtrue": case "brtrue.s": case "brfalse": case "brfalse.s":
                        if (Truthy(stack.Pop()) == op.StartsWith("brtrue", StringComparison.Ordinal))
                        {
                            pc = labels[(Label)operand!] - 1;
                        }

                        break;
                    case "beq": case "beq.s": case "bne.un": case "bne.un.s":
                    case "bge": case "bge.s": case "bge.un": case "bge.un.s":
                    case "bgt": case "bgt.s": case "bgt.un": case "bgt.un.s":
                    case "ble": case "ble.s": case "ble.un": case "ble.un.s":
                    case "blt": case "blt.s": case "blt.un": case "blt.un.s":
                    {
                        object? right = stack.Pop();
                        object? left = stack.Pop();
                        if (CompareBranch(op, left, right))
                        {
                            pc = labels[(Label)operand!] - 1;
                        }

                        break;
                    }
                    case "switch":
                    {
                        int index = Convert.ToInt32(stack.Pop());
                        Label[] targets = (Label[])operand!;
                        if (index >= 0 && index < targets.Length)
                        {
                            pc = labels[targets[index]] - 1;
                        }

                        break;
                    }
                    case "ceq": case "cgt": case "cgt.un": case "clt": case "clt.un":
                    {
                        object? right = stack.Pop();
                        object? left = stack.Pop();
                        stack.Push(CompareBranch(op, left, right) ? 1 : 0);
                        break;
                    }
                    case "add": case "sub": case "mul": case "div": case "div.un": case "rem":
                    case "and": case "or": case "xor": case "shl": case "shr": case "shr.un":
                    {
                        object? right = stack.Pop();
                        stack.Push(Binary(op, stack.Pop(), right));
                        break;
                    }
                    case "neg": stack.Push(-Convert.ToDecimal(stack.Pop())); break;
                    case "not": stack.Push(~Convert.ToInt64(stack.Pop())); break;
                    case "conv.i4": case "conv.u4": case "conv.i": case "conv.u":
                        stack.Push((int)Convert.ToDecimal(stack.Pop())); break;
                    case "conv.i8": case "conv.u8": stack.Push((long)Convert.ToDecimal(stack.Pop())); break;
                    case "conv.r4": stack.Push(Convert.ToSingle(stack.Pop())); break;
                    case "conv.r8": case "conv.r.un": stack.Push(Convert.ToDouble(stack.Pop())); break;
                    case "newarr":
                    {
                        Array array = Array.CreateInstance((Type)operand!, Convert.ToInt32(stack.Pop()));
                        OwnCollection(array);
                        stack.Push(array);
                        break;
                    }
                    case "ldlen": stack.Push(((Array)stack.Pop()!).Length); break;
                    case "ldelem.ref": case "ldelem.i4": case "ldelem":
                    {
                        int index = Convert.ToInt32(stack.Pop());
                        stack.Push(((Array)GetCollection(stack.Pop()!)).GetValue(index));
                        break;
                    }
                    case "stelem.ref": case "stelem.i4": case "stelem":
                    {
                        object? value = stack.Pop();
                        int index = Convert.ToInt32(stack.Pop());
                        ((Array)GetCollection(stack.Pop()!)).SetValue(MaterializeValue(value), index);
                        break;
                    }
                    case "ret":
                        // IL 的 bool、byte 等返回值使用 i4 栈槽，跨方法边界恢复声明类型。
                        return method is MethodInfo { ReturnType: var resultType } && resultType != typeof(void)
                            ? Coerce(Dereference(stack.Pop()), resultType) : null;
                    default: throw new UnsupportedHookException(method);
                }
            }

            return null;
        }
        finally
        {
            _depth--;
        }
    }

    private object? ReadField(object? owner, FieldInfo field)
    {
        object key = owner ?? field.DeclaringType!;
        if (_fields.TryGetValue((key, field), out object? value))
        {
            return value is IList or IDictionary ? GetCollection(value) : value;
        }

        if (simulation.TryReadPreviewField(owner, field, out value))
        {
            return value;
        }

        value = field.GetValue(owner);
        return value is IList or IDictionary ? GetCollection(value) : value;
    }

    private void WriteField(object? owner, FieldInfo field, object? value)
    {
        // 数值查询只读取战斗模型，允许局部结构体/闭包字段，拒绝写入真实模型的模拟状态。
        if (_isReadingQuery && (owner is AbstractModel or Creature || ReferenceEquals(owner, simulation.Combat)))
        {
            throw new InvalidOperationException($"A preview query cannot write {field.DeclaringType?.FullName}.{field.Name}");
        }

        if (simulation.TryWritePreviewField(owner, field, Dereference(value)))
        {
            return;
        }

        var key = (owner ?? field.DeclaringType!, field);
        if (_queryWrites != null && !_queryWrites.ContainsKey(key))
        {
            bool exists = _fields.TryGetValue(key, out object? before);
            _queryWrites[key] = (exists, before);
        }

        _fields[key] = Dereference(value);
        if (!_fieldsByOwner.TryGetValue(key.Item1, out HashSet<FieldInfo>? fields))
        {
            _fieldsByOwner[key.Item1] = fields = [];
        }

        fields.Add(field);
    }

    private object GetCollection(object original)
    {
        if (!_collections.TryGetValue(original, out object? copy))
        {
            copy = CopyCollection(original);
            _collections.Add(original, copy);
            _collections.TryAdd(copy, copy);
        }

        return copy;
    }

    private void OwnCollection(object collection)
    {
        _ownedCollections.Add(collection);
        _collections.TryAdd(collection, collection);
    }

    private static bool IsSystemCollection(Type type) => type.IsArray
        || type == typeof(PreviewOrderedSequence)
        // C# 编译器生成的只读集合构造器只保存元素；允许在新实例上执行，以保留真实枚举语义。
        || type.Assembly == typeof(CardModel).Assembly
            && (type.Name.Contains("z__ReadOnly", StringComparison.Ordinal)
                || type.DeclaringType?.Name.Contains("z__ReadOnly", StringComparison.Ordinal) == true)
        || type.Assembly == typeof(List<>).Assembly
            && (type.Namespace?.StartsWith("System.Collections", StringComparison.Ordinal) == true
                || type.FullName == "System.ArrayEnumerator"
                || type.FullName?.StartsWith("System.SZGenericArrayEnumerator`", StringComparison.Ordinal) == true);

    private static object CopyCollection(object original)
    {
        if (original is Array array)
        {
            return array.Clone();
        }

        if (original is IDictionary dictionary)
        {
            var copy = (IDictionary)Activator.CreateInstance(original.GetType())!;
            foreach (DictionaryEntry entry in dictionary)
            {
                copy.Add(entry.Key, entry.Value);
            }

            return copy;
        }

        if (original is IList list && !list.IsReadOnly)
        {
            var copy = (IList)Activator.CreateInstance(original.GetType())!;
            foreach (object? item in list)
            {
                copy.Add(item);
            }

            return copy;
        }

        if (original.GetType().Namespace == "System.Collections.Generic")
        {
            ConstructorInfo? constructor = original.GetType().GetConstructors().FirstOrDefault(candidate =>
                candidate.GetParameters() is [var parameter] && parameter.ParameterType.IsInstanceOfType(original));
            if (constructor != null)
            {
                return constructor.Invoke([original]);
            }
        }

        // 未支持的集合仅允许读取，写操作由调用白名单拒绝。
        return original;
    }

    private object? EnumerableCall(MethodInfo method, object?[] arguments)
    {
        string name = method.Name;
        if (name == "Empty")
        {
            return Array.CreateInstance(method.GetGenericArguments()[0], 0);
        }

        object?[] items = ((IEnumerable)GetCollection(arguments[0]!)).Cast<object?>().ToArray();
        object? Evaluate(object? item)
        {
            object? callback = arguments[1];
            return callback switch
            {
                PreviewDelegate preview => Invoke(preview.Method, preview.Target, [item]),
                Delegate existing => Invoke(existing.Method, existing.Target, [item]),
                _ => throw new UnsupportedHookException(method)
            };
        }

        Type element = method.GetGenericArguments().LastOrDefault() ?? typeof(object);
        object Pack(IEnumerable<object?> values)
        {
            object?[] result = values.ToArray();
            Array array = Array.CreateInstance(element, result.Length);
            OwnCollection(array);
            for (int i = 0; i < result.Length; i++)
            {
                array.SetValue(MaterializeValue(result[i]), i);
            }

            return array;
        }

        if (name is "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending")
        {
            PreviewOrderedSequence? previous = name.StartsWith("Then", StringComparison.Ordinal)
                ? arguments[0] as PreviewOrderedSequence : null;
            bool descending = name.EndsWith("Descending", StringComparison.Ordinal);
            bool[] directions = previous == null ? [descending] : [.. previous.Descending, descending];
            var keys = new object?[items.Length][];
            for (int i = 0; i < items.Length; i++)
            {
                object? key = Evaluate(items[i]);
                if (key != null && key.GetType().Namespace != "System" && !key.GetType().IsEnum)
                {
                    throw new UnsupportedHookException(method);
                }

                keys[i] = previous == null ? [key] : [.. previous.Keys[i], key];
            }

            int Compare(int left, int right)
            {
                for (int key = 0; key < directions.Length; key++)
                {
                    int comparison = Comparer<object?>.Default.Compare(keys[left][key], keys[right][key]);
                    if (comparison != 0)
                    {
                        return directions[key] ? -Math.Sign(comparison) : Math.Sign(comparison);
                    }
                }

                return left.CompareTo(right);
            }

            int[] order = Enumerable.Range(0, items.Length).ToArray();
            Array.Sort(order, Compare);
            return new PreviewOrderedSequence(order.Select(index => items[index]).ToArray(),
                order.Select(index => keys[index]).ToArray(), directions);
        }

        return name switch
        {
            "Contains" => items.Contains(arguments[1]),
            "ToArray" or "Cast" => Pack(items),
            "ToList" => Activator.CreateInstance(typeof(List<>).MakeGenericType(element), Pack(items)),
            "OfType" => Pack(items.Where(item => item != null && element.IsInstanceOfType(item))),
            "Where" => Pack(items.Where(item => Truthy(Evaluate(item)))),
            "Select" => Pack(items.Select(Evaluate)),
            "Concat" => Pack(items.Concat(((IEnumerable)arguments[1]!).Cast<object?>())),
            "Distinct" => Pack(items.Distinct()),
            "Reverse" => Pack(items.Reverse()),
            "Take" => Pack(items.Take(Convert.ToInt32(arguments[1]))),
            "Skip" => Pack(items.Skip(Convert.ToInt32(arguments[1]))),
            "Any" => arguments.Length == 1 ? items.Length > 0 : items.Any(item => Truthy(Evaluate(item))),
            "All" => items.All(item => Truthy(Evaluate(item))),
            "Count" => arguments.Length == 1 ? items.Length : items.Count(item => Truthy(Evaluate(item))),
            "FirstOrDefault" => arguments.Length == 1 ? items.FirstOrDefault() : items.FirstOrDefault(item => Truthy(Evaluate(item))),
            "First" => arguments.Length == 1 ? items.First() : items.First(item => Truthy(Evaluate(item))),
            "LastOrDefault" => arguments.Length == 1 ? items.LastOrDefault() : items.LastOrDefault(item => Truthy(Evaluate(item))),
            "Last" => arguments.Length == 1 ? items.Last() : items.Last(item => Truthy(Evaluate(item))),
            "ElementAt" => items[Convert.ToInt32(arguments[1])],
            "ElementAtOrDefault" => items.ElementAtOrDefault(Convert.ToInt32(arguments[1])),
            "Sum" => items.Sum(item => Convert.ToDecimal(arguments.Length == 1 ? item : Evaluate(item))),
            _ => throw new UnsupportedHookException(method)
        };
    }

    private sealed class PreviewOrderedSequence(object?[] items, object?[][] keys, bool[] descending) : IEnumerable
    {
        internal object?[][] Keys { get; } = keys;

        internal bool[] Descending { get; } = descending;

        public IEnumerator GetEnumerator() => items.GetEnumerator();
    }

    private object? InvokeTrusted(MethodBase method, object? instance, object?[] arguments)
    {
        ParameterInfo[] parameters = method.GetParameters();
        object?[] values = arguments.Select((argument, index) =>
        {
            object? value = Dereference(argument);
            if (value is IList or IDictionary)
            {
                value = GetCollection(value);
            }

            return Coerce(MaterializeValue(value), parameters[index].ParameterType);
        }).ToArray();
        object? result = method.Invoke(instance, values);
        if (result != null && method.Name is "ToArray" or "ToList" && IsSystemCollection(result.GetType()))
        {
            OwnCollection(result);
        }
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] is Reference reference)
            {
                reference.Value = values[i] is IList or IDictionary ? GetCollection(values[i]!) : values[i];
            }
        }

        return result;
    }

    private static object? Coerce(object? value, Type type)
    {
        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        if (value == null || type.IsInstanceOfType(value))
        {
            return value;
        }

        if (type.IsEnum)
        {
            return Enum.ToObject(type, value);
        }

        return value is IConvertible && typeof(IConvertible).IsAssignableFrom(type)
            ? Convert.ChangeType(value, type) : value;
    }

    private object? MaterializeValue(object? value)
    {
        if (value != null && value.GetType().IsValueType)
        {
            // 装箱值是独立副本，写回本地字段后，数组/List 的值复制才保留解释后的内容。
            MaterializeFields(value);
        }

        return value;
    }

    private void MaterializeFields(object value)
    {
        if (!_fieldsByOwner.TryGetValue(value, out HashSet<FieldInfo>? fields))
        {
            return;
        }

        foreach (FieldInfo field in fields)
        {
            if (_fields.TryGetValue((value, field), out object? fieldValue))
            {
                field.SetValue(value, Coerce(fieldValue, field.FieldType));
            }
        }
    }

    private sealed class FieldKeyComparer : IEqualityComparer<(object Owner, FieldInfo Field)>
    {
        internal static readonly FieldKeyComparer Instance = new();

        public bool Equals((object Owner, FieldInfo Field) left, (object Owner, FieldInfo Field) right) =>
            ReferenceEquals(left.Owner, right.Owner) && left.Field.Equals(right.Field);

        public int GetHashCode((object Owner, FieldInfo Field) value) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(value.Owner), value.Field);
    }

    private static object? Dereference(object? value) => value is Reference reference ? reference.Value : value;

    private static ParameterInfo[] GetParameters(MethodBase method)
    {
        if (!Parameters.TryGetValue(method, out ParameterInfo[]? parameters))
        {
            Parameters[method] = parameters = method.GetParameters();
        }

        return parameters;
    }

    private sealed record MethodPlan(List<CodeInstruction> Code, Dictionary<Label, int> Labels,
        List<(int Start, int Finally, int End)> FinallyRegions, Type[] LocalTypes);

    private static MethodPlan CreatePlan(MethodBase method)
    {
        // 使用当前 transpiler 结果，保留本模组与基础库对查询路径的修正。
        List<CodeInstruction> code = PatchProcessor.GetCurrentInstructions(method);
        var labels = new Dictionary<Label, int>();
        var exceptionStack = new Stack<(int Start, int Finally)>();
        var finallyRegions = new List<(int Start, int Finally, int End)>();
        for (int i = 0; i < code.Count; i++)
        {
            foreach (Label label in code[i].labels)
            {
                labels[label] = i;
            }

            foreach (ExceptionBlock block in code[i].blocks)
            {
                switch (block.blockType)
                {
                    case ExceptionBlockType.BeginExceptionBlock:
                        exceptionStack.Push((i, -1));
                        break;
                    case ExceptionBlockType.BeginFinallyBlock:
                    {
                        var region = exceptionStack.Pop();
                        exceptionStack.Push((region.Start, i));
                        break;
                    }
                    case ExceptionBlockType.EndExceptionBlock:
                    {
                        var region = exceptionStack.Pop();
                        if (region.Finally >= 0)
                        {
                            finallyRegions.Add((region.Start, region.Finally, i));
                        }

                        break;
                    }
                }
            }
        }

        var localTypes = method.GetMethodBody()!.LocalVariables.Select(static local => local.LocalType).ToList();
        foreach (CodeInstruction instruction in code)
        {
            if (instruction.operand is LocalBuilder local)
            {
                while (localTypes.Count <= local.LocalIndex)
                {
                    localTypes.Add(typeof(object));
                }

                localTypes[local.LocalIndex] = local.LocalType;
            }
        }

        return new MethodPlan(code, labels, finallyRegions, localTypes.ToArray());
    }

    private static int LocalIndex(object? operand) => operand is LocalBuilder local
        ? local.LocalIndex : Convert.ToInt32(operand);

    private static bool Truthy(object? value)
    {
        value = Dereference(value);
        return value != null && (value is string || value is not IConvertible || Convert.ToDecimal(value) != 0);
    }

    private static bool CompareBranch(string op, object? left, object? right)
    {
        left = Dereference(left);
        right = Dereference(right);
        bool equal = left is IConvertible && right is IConvertible && left is not string && right is not string
            ? Convert.ToDecimal(left) == Convert.ToDecimal(right) : Equals(left, right);
        if (op.StartsWith("beq", StringComparison.Ordinal) || op == "ceq")
        {
            return equal;
        }

        if (op.StartsWith("bne", StringComparison.Ordinal))
        {
            return !equal;
        }

        if (right == null && left != null && op == "cgt.un")
        {
            return true;
        }

        decimal a = left == null ? 0 : Convert.ToDecimal(left);
        decimal b = right == null ? 0 : Convert.ToDecimal(right);
        if (op.StartsWith("bge", StringComparison.Ordinal))
        {
            return a >= b;
        }

        if (op.StartsWith("ble", StringComparison.Ordinal))
        {
            return a <= b;
        }

        if (op.StartsWith("bgt", StringComparison.Ordinal) || op.StartsWith("cgt", StringComparison.Ordinal))
        {
            return a > b;
        }

        return a < b;
    }

    private static object Binary(string op, object? left, object? right)
    {
        decimal a = Convert.ToDecimal(left);
        decimal b = Convert.ToDecimal(right);
        return op switch
        {
            "add" => a + b,
            "sub" => a - b,
            "mul" => a * b,
            "div" or "div.un" => left is float or double || right is float or double ? a / b : decimal.Truncate(a / b),
            "rem" => a % b,
            "and" => (long)a & (long)b,
            "or" => (long)a | (long)b,
            "xor" => (long)a ^ (long)b,
            "shl" => (long)a << (int)b,
            "shr" or "shr.un" => (long)a >> (int)b,
            _ => throw new InvalidOperationException(op)
        };
    }

    private sealed class PreviewLockScope;

    private static object? DefaultValue(Type type)
    {
        if (type == typeof(System.Threading.Lock.Scope))
        {
            return new PreviewLockScope();
        }

        return type != typeof(void) && type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static object? DefaultReturn(MethodBase method)
    {
        if (method is not MethodInfo info)
        {
            return null;
        }

        if (typeof(Task).IsAssignableFrom(info.ReturnType))
        {
            return new PreviewTask(null);
        }

        return DefaultValue(info.ReturnType);
    }

    private sealed class UnsupportedHookException(MethodBase method) : Exception(method.ToString());
}

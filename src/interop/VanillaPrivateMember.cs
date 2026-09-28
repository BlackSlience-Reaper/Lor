using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;

namespace LibraryOfRuina.interop;

/// <summary>
/// 一个按名字解析的原版非公开成员。<see cref="VanillaPrivate"/> 里每个成员一个实例，类型初始化时解析；
/// 初始化步骤 VanillaPrivate 汇总缺失项，调用方拿到“不可用”时走自己的降级路径，不抛异常。
/// </summary>
internal abstract class VanillaPrivateMember
{
    private static readonly List<VanillaPrivateMember> Registered = [];

    protected VanillaPrivateMember(string name, bool optional)
    {
        Name = name;
        Optional = optional;
        lock (Registered)
        {
            Registered.Add(this);
        }
    }

    /// <summary>“类型.成员”，与原版源码里的名字一致。</summary>
    public string Name { get; }

    /// <summary>只在部分游戏版本存在（兼容分支），缺失不算错误。</summary>
    public bool Optional { get; }

    public abstract bool IsAvailable { get; }

    internal static IReadOnlyList<VanillaPrivateMember> All
    {
        get
        {
            lock (Registered)
            {
                return Registered.ToArray();
            }
        }
    }

    protected const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    protected const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
}

/// <summary>实例字段。<typeparamref name="TValue"/> 可以是字段类型的基类（只读时）。</summary>
internal sealed class VanillaPrivateField<TOwner, TValue> : VanillaPrivateMember
{
    private readonly FieldInfo? _field;

    public VanillaPrivateField(string field, bool optional = false)
        : this(typeof(TOwner), field, optional)
    {
    }

    /// <summary>字段声明在无法直接引用的类型上（例如私有嵌套类型）时用这个重载。</summary>
    public VanillaPrivateField(Type? declaringType, string field, bool optional = false)
        : base((declaringType?.Name ?? typeof(TOwner).Name) + "." + field, optional)
    {
        _field = declaringType == null ? null : AccessTools.Field(declaringType, field);
    }

    public override bool IsAvailable => _field != null;

    public TValue? Get(TOwner owner) => TryGet(owner, out TValue? value) ? value : default;

    /// <summary>调用方离不开这个成员时用：缺失或值为空时抛出带成员名的异常，而不是之后的空引用。</summary>
    public TValue GetRequired(TOwner owner) =>
        TryGet(owner, out TValue? value) && value != null
            ? value
            : throw new InvalidOperationException("Vanilla member " + Name + " is not available.");

    public bool TryGet(TOwner owner, out TValue? value)
    {
        if (_field != null && owner != null && _field.GetValue(owner) is TValue typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public bool Set(TOwner owner, TValue value)
    {
        if (_field == null || owner == null)
        {
            return false;
        }

        _field.SetValue(owner, value);
        return true;
    }
}

/// <summary>
/// 每帧都要读的实例字段（伤害预览等），用 Harmony 生成的 FieldRef 委托，不走反射调用。
/// <typeparamref name="TValue"/> 必须与字段类型一致；缺失时 <see cref="Get"/> 返回默认值。
/// </summary>
internal sealed class VanillaPrivateFieldRef<TOwner, TValue> : VanillaPrivateMember
    where TOwner : class
{
    private readonly AccessTools.FieldRef<TOwner, TValue>? _ref;

    public VanillaPrivateFieldRef(string field, bool optional = false)
        : base(typeof(TOwner).Name + "." + field, optional)
    {
        try
        {
            _ref = AccessTools.Field(typeof(TOwner), field) == null ? null : AccessTools.FieldRefAccess<TOwner, TValue>(field);
        }
        catch (ArgumentException)
        {
            _ref = null;
        }
    }

    public override bool IsAvailable => _ref != null;

    public TValue? Get(TOwner owner) => _ref != null ? _ref(owner) : default;
}

internal sealed class VanillaPrivateStaticField<TOwner, TValue> : VanillaPrivateMember
{
    private readonly FieldInfo? _field;

    public VanillaPrivateStaticField(string field, bool optional = false)
        : base(typeof(TOwner).Name + "." + field, optional)
    {
        _field = typeof(TOwner).GetField(field, AnyStatic);
    }

    public override bool IsAvailable => _field != null;

    public TValue? Get() => _field?.GetValue(null) is TValue typed ? typed : default;
}

/// <summary>实例属性（非公开，或公开属性的非公开 setter）。</summary>
internal sealed class VanillaPrivateProperty<TOwner, TValue> : VanillaPrivateMember
{
    private readonly MethodInfo? _getter;
    private readonly MethodInfo? _setter;

    public VanillaPrivateProperty(string property, bool optional = false)
        : base(typeof(TOwner).Name + "." + property, optional)
    {
        PropertyInfo? info = typeof(TOwner).GetProperty(property, AnyInstance);
        _getter = info?.GetGetMethod(nonPublic: true);
        _setter = info?.GetSetMethod(nonPublic: true);
    }

    public override bool IsAvailable => _getter != null || _setter != null;

    public TValue? Get(TOwner owner) =>
        _getter != null && owner != null && _getter.Invoke(owner, null) is TValue typed ? typed : default;

    public bool Set(TOwner owner, TValue value)
    {
        if (_setter == null || owner == null)
        {
            return false;
        }

        _setter.Invoke(owner, [value]);
        return true;
    }
}

/// <summary>实例方法。参数类型为空时按名字解析（原版没有重载）。</summary>
internal sealed class VanillaPrivateMethod<TOwner> : VanillaPrivateMember
{
    public VanillaPrivateMethod(string method, Type[]? parameters = null, bool optional = false)
        : base(typeof(TOwner).Name + "." + method, optional)
    {
        Method = AccessTools.Method(typeof(TOwner), method, parameters);
    }

    public MethodInfo? Method { get; }

    public override bool IsAvailable => Method != null;

    /// <summary>调用并返回结果；方法缺失时返回 null。原方法抛出的异常原样抛出（不包在 TargetInvocationException 里）。</summary>
    public object? Invoke(TOwner owner, params object?[] arguments)
    {
        if (Method == null || owner == null)
        {
            return null;
        }

        try
        {
            return Method.Invoke(owner, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    public Task InvokeAsync(TOwner owner, params object?[] arguments) =>
        Invoke(owner, arguments) as Task ?? Task.CompletedTask;
}

using System;
using System.Runtime.CompilerServices;

namespace LibraryOfRuina.infra.lifecycle;

/// <summary>
/// 局级、战斗级静态状态的登记表。容器在构造时登记，离开本局时由
/// <see cref="LibraryOfRuina.patches.dispatch.RunLifecycle"/> 统一复位。
/// <para>
/// 容器只能声明成 <c>static readonly</c> 字段：登记表只增不减，实例字段会让登记表无限增长。
/// 包含容器的类型第一次被访问时才登记；还没登记的容器里也不会有值，不需要复位。
/// </para>
/// <para>
/// 新增承载局内或战内玩法状态的静态字段时优先用这两个容器；<c>tools/static_state.txt</c> 登记全部有状态的静态字段，
/// <c>tools/check.sh</c> 会拦下没有登记的字段。
/// </para>
/// </summary>
internal static class ScopedState
{
    private static readonly object Gate = new();
    private static readonly List<IScopedState> Registered = [];

    internal static void Register(IScopedState state)
    {
        lock (Gate)
        {
            Registered.Add(state);
        }
    }

    /// <summary>把全部已登记的容器恢复初值。只写内存，不抛异常。</summary>
    internal static void ResetAll()
    {
        IScopedState[] states;
        lock (Gate)
        {
            states = Registered.ToArray();
        }

        foreach (IScopedState state in states)
        {
            state.Reset();
        }
    }
}

internal interface IScopedState
{
    void Reset();
}

/// <summary>
/// 一局内有效的静态状态，离开本局（<c>RunManager.CleanUp</c>）时回到初值。
/// 读档之前总会先离开上一局，所以读档后也是初值：需要跨读档保留的值要写进存档（SavedProperty、遭遇或房间的存档字典），
/// 或者在读档后能从存档内容重新算出来。
/// </summary>
internal sealed class RunScoped<T> : IScopedState
{
    private readonly Func<T> _initial;

    public RunScoped(Func<T> initial)
    {
        _initial = initial;
        Value = initial();
        ScopedState.Register(this);
    }

    public T Value { get; set; }

    void IScopedState.Reset() => Value = _initial();
}

/// <summary>
/// 一场战斗内有效的静态状态，按战斗里的对象（战斗状态、生物、卡牌等）分别存放。
/// <para>
/// 键是弱引用：键对象被回收后条目随之消失，不会像静态字典那样把整场战斗留在内存里。
/// 每场战斗（包括读档后重开的战斗）都是新对象，所以新战斗天然读到初值，不依赖战斗开始或结束时的复位；
/// 离开本局时仍会整表清空。
/// </para>
/// <para>只做查找，不提供遍历：遍历顺序与回收时机有关，两端不一定相同。</para>
/// </summary>
internal sealed class CombatScoped<TKey, TValue> : IScopedState
    where TKey : class
{
    private readonly ConditionalWeakTable<TKey, StrongBox<TValue>> _values = new();

    public CombatScoped()
    {
        ScopedState.Register(this);
    }

    public bool TryGetValue(TKey? key, out TValue value)
    {
        if (key != null && _values.TryGetValue(key, out StrongBox<TValue>? box))
        {
            value = box.Value!;
            return true;
        }

        value = default!;
        return false;
    }

    public TValue GetValueOrDefault(TKey? key, TValue fallback) =>
        TryGetValue(key, out TValue value) ? value : fallback;

    public void Set(TKey key, TValue value) => _values.AddOrUpdate(key, new StrongBox<TValue>(value));

    public bool Remove(TKey? key) => key != null && _values.Remove(key);

    public void Clear() => _values.Clear();

    void IScopedState.Reset() => _values.Clear();
}

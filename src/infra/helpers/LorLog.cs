using System;
using System.Collections.Concurrent;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.infra.helpers;

/// <summary>
/// 本模组的日志入口：级别门控加按键去重，文本原样交给原版 <see cref="Log"/>。
/// 前缀（<c>[LibraryOfRuina.Xxx]</c> 等）由调用方写在文本里，所以换成这里之后日志行与原来逐字相同。
/// <para>
/// Debug 走原版 <see cref="Log.Debug"/>，默认不输出：原版 Generic 类型的日志级别默认是 Info。
/// 不重编译就能打开：启动参数 <c>-log Generic Debug</c>，或开发者控制台执行 <c>log debug</c>
/// （原版 <c>LogConsoleCmd</c>，不是 DebugOnly 命令；局中也能切换）。
/// 不另设环境变量或设置项：原版开关玩家本来就能用，报 bug 时只需让对方加启动参数，也不会多出玩家可见的设置。
/// 代价是同时打开原版 Generic 类型的 Debug 日志。
/// </para>
/// <para>
/// 去重表以字符串为键、存活到进程结束。键由调用方加类型或功能前缀（如 <c>"Presentation:" + surface</c>），
/// 避免不同调用点撞键；不要把每回合都会变化的值放进键里，否则表会一直增长
/// （这类只在 <see cref="IsDebugEnabled"/> 时去重，见 <see cref="FirstTime"/> 的用法）。
/// </para>
/// </summary>
internal static class LorLog
{
    private const int PatchFailureLogLimit = 3;

    // 包一层之后调用栈多一帧：原版 Log.* 的默认 skipFrames 是 2，这里传 3，Error 打出的调用栈仍从调用方开始。
    private const int SkipFrames = 3;

    private static readonly ConcurrentDictionary<string, int> Counts = new(StringComparer.Ordinal);

    /// <summary>原版 Generic 类型当前是否输出 Debug。热路径拼接日志文本前先判断它。</summary>
    public static bool IsDebugEnabled =>
        (Logger.logLevelTypeMap.TryGetValue(LogType.Generic, out LogLevel level) ? level : Logger.GlobalLogLevel)
        <= LogLevel.Debug;

    public static void Debug(string message) => Log.Debug(message, SkipFrames);

    public static void Info(string message) => Log.Info(message, SkipFrames);

    public static void Warn(string message) => Log.Warn(message, SkipFrames);

    public static void Error(string message) => Log.Error(message, SkipFrames);

    public static void InfoOnce(string key, string message)
    {
        if (FirstTime(key))
        {
            Log.Info(message, SkipFrames);
        }
    }

    public static void WarnOnce(string key, string message)
    {
        if (FirstTime(key))
        {
            Log.Warn(message, SkipFrames);
        }
    }

    public static void ErrorOnce(string key, string message)
    {
        if (FirstTime(key))
        {
            Log.Error(message, SkipFrames);
        }
    }

    /// <summary>同一个键只记前 <paramref name="limit"/> 次 Warn，用来节流会反复出现的已知异常。</summary>
    public static void WarnFirst(string key, int limit, string message)
    {
        if (Increment(key) <= limit)
        {
            Log.Warn(message, SkipFrames);
        }
    }

    /// <summary>
    /// 表现层补丁吞掉异常时记录：每个调用点只记前 3 次，文本带序号，
    /// 格式为 <c>[LibraryOfRuina.PatchFailure] 调用点 failed (n/3): 异常</c>。
    /// </summary>
    public static void PatchFailure(string surface, Exception exception)
    {
        int count = Increment("PatchFailure:" + surface);
        if (count > PatchFailureLogLimit)
        {
            return;
        }

        Log.Warn(
            "[LibraryOfRuina.PatchFailure] "
            + surface
            + " failed ("
            + count
            + "/"
            + PatchFailureLogLimit
            + "): "
            + exception,
            SkipFrames);
    }

    /// <summary>
    /// 这个键第一次出现时返回 true。文本拼接很贵、或键会不断变化时用它，并先判断 <see cref="IsDebugEnabled"/>：
    /// <c>if (LorLog.IsDebugEnabled &amp;&amp; LorLog.FirstTime(key)) LorLog.Debug(...)</c>。
    /// </summary>
    public static bool FirstTime(string key) => Counts.TryAdd(key, 1);

    private static int Increment(string key) =>
        Counts.AddOrUpdate(key, 1, static (_, value) => value == int.MaxValue ? value : value + 1);
}

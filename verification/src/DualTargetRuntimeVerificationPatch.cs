using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 双版本发行包的运行期检查：三个加载器都关联了实现程序集，且实现里的 Godot 节点带有源码生成器产出的
/// 回调派发代码。缺少生成代码时编译照常通过，但引擎永远不会调用 _Process/_Ready/_Draw。
/// </summary>
internal static class DualTargetRuntimeVerificationPatch
{
    private const string VerifyArg = "lor-verify-dual-target";
    private const string LogPrefix = "[LibraryOfRuina.DualTarget.Verify] ";
    private static readonly string[] ImplementationIds = ["LibraryOfRuinaLib", "ActLikeIt2", "LibraryOfRuina"];
    private static readonly string[] EngineCallbacks =
        ["_Process", "_PhysicsProcess", "_Ready", "_Draw", "_Input", "_UnhandledInput", "_EnterTree", "_ExitTree"];
    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() => { _ = RunAsync(); }).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(argument => string.Equals(
            argument.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            // 主菜单就绪后原版还在后台预加载场景；立即退出会打断这些加载并留下与本检查无关的解析报错。
            SceneTree tree = NGame.Instance?.GetTree() ?? throw new InvalidOperationException("Scene tree is not available.");
            await tree.ToSignal(tree.CreateTimer(15.0), SceneTreeTimer.SignalName.Timeout);
            List<Assembly> implementations = VerifyLoadedImplementations();
            int nodeTypes = implementations.Sum(VerifyGeneratedGodotGlue);
            await VerifyProcessingEnabled(implementations.Single(a => a.GetName().Name == "LibraryOfRuinaLib"));
            Log.Info(LogPrefix + "DUAL_TARGET_OK implementations=" + implementations.Count + " callbackNodeTypes=" + nodeTypes);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + "DUAL_TARGET_FAILED: " + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static List<Assembly> VerifyLoadedImplementations()
    {
        var result = new List<Assembly>();
        foreach (string id in ImplementationIds)
        {
            Mod mod = ModManager.Mods.FirstOrDefault(m => m.manifest?.id == id)
                      ?? throw new InvalidOperationException("Mod not found: " + id);
            if (mod.state != ModLoadState.Loaded)
            {
                throw new InvalidOperationException($"{id} state is {mod.state}.");
            }

            // 0.111.0 记在 assemblies 集合里，0.107.1 是改写后的单个 assembly 字段；按字段名取，避免整份套件分两版。
            IEnumerable<Assembly> assemblies = typeof(Mod).GetField("assemblies")?.GetValue(mod) is IEnumerable list
                ? list.OfType<Assembly>()
                : typeof(Mod).GetField("assembly")?.GetValue(mod) is Assembly single ? [single] : [];
            Assembly implementation = assemblies.FirstOrDefault(a => a.GetName().Name == id)
                ?? throw new InvalidOperationException(id + " implementation assembly is not associated with its mod record.");
            Log.Info(LogPrefix + $"{id}: implementation associated ({implementation.GetName().Version}).");
            result.Add(implementation);
        }

        return result;
    }

    private static int VerifyGeneratedGodotGlue(Assembly assembly)
    {
        const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        int count = 0;
        foreach (Type type in assembly.GetTypes().Where(t => typeof(GodotObject).IsAssignableFrom(t) && !t.IsAbstract))
        {
            string[] callbacks = EngineCallbacks.Where(name => type.GetMethod(name, declared) != null).ToArray();
            if (callbacks.Length == 0)
            {
                continue;
            }

            if (type.GetMethod("InvokeGodotClassMethod", declared) == null
                || type.GetMethod("HasGodotClassMethod", declared) == null)
            {
                throw new InvalidOperationException(
                    $"{type.FullName} overrides {string.Join(", ", callbacks)} but has no generated Godot dispatch; the engine will never call them.");
            }

            count++;
        }

        Log.Info(LogPrefix + $"{assembly.GetName().Name}: {count} node type(s) with engine callbacks carry generated dispatch.");
        return count;
    }

    private static async Task VerifyProcessingEnabled(Assembly library)
    {
        SceneTree tree = NGame.Instance?.GetTree() ?? throw new InvalidOperationException("Scene tree is not available.");
        foreach (Type type in library.GetTypes().Where(t => typeof(Node).IsAssignableFrom(t) && !t.IsAbstract
                     && t.GetConstructor(Type.EmptyTypes) != null
                     && t.GetMethod("_Process", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null))
        {
            Node node;
            try
            {
                node = (Node)Activator.CreateInstance(type)!;
            }
            catch (Exception exception)
            {
                Log.Info(LogPrefix + $"skip {type.FullName}: constructor threw {exception.GetType().Name}");
                continue;
            }

            tree.Root.AddChild(node);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            bool processing = node.IsProcessing();
            node.QueueFree();
            if (!processing)
            {
                throw new InvalidOperationException(type.FullName + " declares _Process but the engine did not enable processing.");
            }

            Log.Info(LogPrefix + type.FullName + ": engine enabled _Process.");
            return;
        }

        throw new InvalidOperationException("No LibraryOfRuinaLib node type with _Process could be instantiated for the runtime check.");
    }
}

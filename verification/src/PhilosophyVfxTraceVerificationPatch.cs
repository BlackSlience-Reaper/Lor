using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.liberation.Philosophy;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 哲学层解放战招式特效的表现层转储，用于改动前后逐行对照。
/// <para>
/// 在真实的哲学层战斗里逐个直接调用 <c>PhilosophyFloorLiberationVfx</c> 的各招式入口，期间用 Harmony 记录特效代码发起的
/// Godot 调用：<c>CreateTween</c>、<c>TweenProperty</c>/<c>TweenMethod</c> 及其 <c>SetDelay/SetTrans/SetEase</c>、
/// <c>SetParallel/SetLoops</c>、<c>SceneTree.CreateTimer</c>（即每次等待的时长）、音效与震屏。只记录调用栈里有特效代码的调用，
/// 其他界面动画不进轨迹。每次创建计时器时，把特效根节点下第一次出现的节点连同初始属性（类型、层级路径、位置、缩放、旋转、
/// 颜色、层级、纹理路径、材质混合模式、粒子参数）记一行 <c>NODE|</c>：这些值在同一段同步代码里设好，补间要到下一帧才开始，
/// 所以与帧时序无关。补间过程中的中间值不记。
/// </para>
/// </summary>
internal static class PhilosophyVfxTraceVerificationPatch
{
    private const string VerifyArg = "lor-verify-philosophy-vfx-trace";
    private const string LogPrefix = "[LibraryOfRuina.PhilosophyVfxTrace.Verify] ";
    private const string VfxNamespace = "LibraryOfRuina.content.liberation.Philosophy";
    private const string CommonVfxNamespace = "LibraryOfRuina.framework.visuals.common";
    private const string VfxRootPrefix = "PhilosophyTwilight";

    private static readonly List<string> Failures = [];
    private static readonly Dictionary<ulong, int> TweenIds = [];
    private static readonly Dictionary<ulong, string> TweenerIds = [];
    private static readonly HashSet<ulong> DumpedNodes = [];
    private static bool _started;
    private static bool _capturing;
    private static string _label = "";
    private static int _sequence;

    internal static void Start()
    {
        if (_started || !HasArg(VerifyArg))
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasArg(string arg) =>
        CommandLineHelper.HasArg(arg)
        || Environment.GetCommandLineArgs().Any(value => string.Equals(
            value.TrimStart('-'),
            arg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            InstallHooks();
            CombatState state = await StartFight("PHILOSOPHYVFXTRACE");
            Creature boss = state.Enemies.Single(static enemy => enemy.Monster is PhilosophyFloorTwilight);
            Creature player = state.PlayerCreatures.Single();
            int eyeWaitFrames = 0;
            while (eyeWaitFrames < 300
                   && !PhilosophyFloorLiberationBackgroundController.TryGetEndBirdEyeGlobalPosition(0, out _))
            {
                await WaitFrames(1);
                eyeWaitFrames++;
            }
            await WaitFrames(30);
            Log.Info(LogPrefix + "TRACE|setup|eyesReady="
                + PhilosophyFloorLiberationBackgroundController.TryGetEndBirdEyeGlobalPosition(0, out _)
                + "|boss=" + Describe(NCombatRoom.Instance?.GetCreatureNode(boss)?.VfxSpawnPosition)
                + "|player=" + Describe(NCombatRoom.Instance?.GetCreatureNode(player)?.VfxSpawnPosition));

            (string Label, Func<Func<Task>, Task> Play)[] moves =
            [
                ("eye-laser", impact => PhilosophyFloorLiberationVfx.PlayEyeLaserAsync(boss, [player], impact)),
                ("eye-laser-default", _ => PhilosophyFloorLiberationVfx.PlayEyeLaserAsync(boss, [player])),
                ("judgment", impact => PhilosophyFloorLiberationVfx.PlayJudgmentAsync(boss, [player], impact)),
                ("judgment-no-target", impact => PhilosophyFloorLiberationVfx.PlayJudgmentAsync(boss, [], impact)),
                ("judgment-default", _ => PhilosophyFloorLiberationVfx.PlayJudgmentAsync(boss, [player])),
                ("tilted-scale-list", impact => PhilosophyFloorLiberationVfx.PlayTiltedScaleAsync(boss, [player], impact)),
                ("tilted-scale-single", impact => PhilosophyFloorLiberationVfx.PlayTiltedScaleAsync(boss, player, impact)),
                ("peace-slam", impact => PhilosophyFloorLiberationVfx.PlayPeaceSlamAsync(boss, [player], impact)),
                ("peace-slam-default", _ => PhilosophyFloorLiberationVfx.PlayPeaceSlamAsync(boss, [player])),
                ("punishment-sfx", _ =>
                {
                    PhilosophyFloorLiberationVfx.PlayPunishmentSfx();
                    return Task.CompletedTask;
                }),
                ("surveillance", _ => PhilosophyFloorLiberationVfx.PlaySurveillanceDarknessAsync(boss)),
                ("blood-strike-0", impact => PhilosophyFloorLiberationVfx.PlayBloodStrikeAsync(boss, player, 0, impact)),
                ("blood-strike-1", impact => PhilosophyFloorLiberationVfx.PlayBloodStrikeAsync(boss, player, 1, impact)),
                ("blood-strike-2", impact => PhilosophyFloorLiberationVfx.PlayBloodStrikeAsync(boss, player, 2, impact)),
                ("blood-strike-default", _ => PhilosophyFloorLiberationVfx.PlayBloodStrikeAsync(boss, player, 5))
            ];
            foreach ((string label, Func<Func<Task>, Task> play) in moves)
            {
                await Capture(label, play);
            }

            if (Failures.Count > 0)
            {
                throw new InvalidOperationException(
                    Failures.Count + " move(s) threw: " + string.Join("; ", Failures));
            }

            Log.Info(LogPrefix + "PHILOSOPHY_VFX_TRACE_OK");
            CleanupRun();
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "PHILOSOPHY_VFX_TRACE_FAILED: " + ex);
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static async Task Capture(string label, Func<Func<Task>, Task> play)
    {
        _label = label;
        _sequence = 0;
        TweenIds.Clear();
        TweenerIds.Clear();
        DumpedNodes.Clear();
        _capturing = true;
        int impacts = 0;
        try
        {
            Emit("begin");
            await play(() =>
            {
                impacts++;
                Emit("impact#" + impacts);
                return Task.CompletedTask;
            });
            DumpNewNodes("done");
            Emit("done|impacts=" + impacts + "|roots=" + string.Join(",", VfxRoots().Select(static root =>
                root.Name + (root.IsQueuedForDeletion() ? ":queued" : ":live"))));
        }
        catch (Exception ex)
        {
            Emit("exception|" + ex.GetType().Name + ": " + ex.Message);
            Failures.Add(label + ": " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            _capturing = false;
        }

        await WaitFrames(10);
        Log.Info(LogPrefix + "TRACE|" + label + "|after|roots=" + VfxRoots().Count());
    }

    private static void Emit(string text) =>
        Log.Info(LogPrefix + "TRACE|" + _label + "|" + (_sequence++).ToString("000", CultureInfo.InvariantCulture)
            + "|" + text);

    private static bool FromVfx()
    {
        if (!_capturing)
        {
            return false;
        }

        foreach (StackFrame frame in new StackTrace(2, false).GetFrames())
        {
            Type? type = frame.GetMethod()?.DeclaringType;
            while (type?.DeclaringType != null)
            {
                type = type.DeclaringType;
            }

            if (type == null)
            {
                continue;
            }

            if (type.Namespace == CommonVfxNamespace
                || (type.Namespace == VfxNamespace && type.Name.Contains("Vfx", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Node> VfxRoots()
    {
        Control? host = NCombatRoom.Instance?.CombatVfxContainer;
        if (host == null || !GodotObject.IsInstanceValid(host))
        {
            return [];
        }

        return host.GetChildren()
            .Where(static child => GodotObject.IsInstanceValid(child)
                && child.Name.ToString().StartsWith(VfxRootPrefix, StringComparison.Ordinal))
            .ToArray();
    }

    private static void DumpNewNodes(string reason)
    {
        foreach (Node root in VfxRoots())
        {
            DumpSubtree(root, root.Name.ToString(), reason);
        }
    }

    private static void DumpSubtree(Node node, string path, string reason)
    {
        if (DumpedNodes.Add(node.GetInstanceId()))
        {
            Emit("NODE|" + reason + "|" + path + "|" + DescribeNode(node));
        }

        foreach (Node child in node.GetChildren())
        {
            DumpSubtree(child, path + "/" + child.Name, reason);
        }
    }

    private static string DescribeNode(Node node)
    {
        var text = new StringBuilder(node.GetType().Name);
        if (node is CanvasItem canvas)
        {
            text.Append("|vis=").Append(canvas.Visible)
                .Append("|z=").Append(canvas.ZIndex)
                .Append("|mod=").Append(Describe(canvas.Modulate))
                .Append("|self=").Append(Describe(canvas.SelfModulate))
                .Append("|mat=").Append(DescribeMaterial(canvas.Material));
        }

        switch (node)
        {
            case Node2D node2D:
                text.Append("|pos=").Append(Describe(node2D.Position))
                    .Append("|scale=").Append(Describe(node2D.Scale))
                    .Append("|rot=").Append(Describe(node2D.Rotation));
                if (node2D.GetParent() is Control)
                {
                    text.Append("|gpos=").Append(Describe(node2D.GlobalPosition));
                }
                break;
            case Control control:
                text.Append("|pos=").Append(Describe(control.Position))
                    .Append("|size=").Append(Describe(control.Size))
                    .Append("|mouse=").Append(control.MouseFilter);
                break;
        }

        switch (node)
        {
            case Sprite2D sprite:
                text.Append("|tex=").Append(sprite.Texture?.ResourcePath)
                    .Append("|centered=").Append(sprite.Centered)
                    .Append("|offset=").Append(Describe(sprite.Offset))
                    .Append("|flip=").Append(sprite.FlipH).Append(',').Append(sprite.FlipV)
                    .Append("|frames=").Append(sprite.Hframes).Append('x').Append(sprite.Vframes)
                    .Append('@').Append(sprite.Frame);
                break;
            case GpuParticles2D particles:
                text.Append("|tex=").Append(particles.Texture?.ResourcePath)
                    .Append("|amount=").Append(particles.Amount)
                    .Append("|life=").Append(Describe(particles.Lifetime))
                    .Append("|oneShot=").Append(particles.OneShot)
                    .Append("|emitting=").Append(particles.Emitting)
                    .Append("|explosive=").Append(Describe(particles.Explosiveness))
                    .Append("|random=").Append(Describe(particles.Randomness))
                    .Append("|local=").Append(particles.LocalCoords)
                    .Append("|rect=").Append(Describe(particles.VisibilityRect.Position))
                    .Append(',').Append(Describe(particles.VisibilityRect.Size));
                if (particles.ProcessMaterial is ParticleProcessMaterial process)
                {
                    text.Append("|pm=").Append(process.EmissionShape)
                        .Append(",noZ=").Append(process.ParticleFlagDisableZ)
                        .Append(",dir=").Append(Describe(process.Direction))
                        .Append(",spread=").Append(Describe(process.Spread))
                        .Append(",grav=").Append(Describe(process.Gravity))
                        .Append(",vel=").Append(Describe(process.InitialVelocityMin))
                        .Append('-').Append(Describe(process.InitialVelocityMax))
                        .Append(",damp=").Append(Describe(process.DampingMin))
                        .Append('-').Append(Describe(process.DampingMax))
                        .Append(",scale=").Append(Describe(process.ScaleMin))
                        .Append('-').Append(Describe(process.ScaleMax))
                        .Append(",angle=").Append(Describe(process.AngleMin))
                        .Append('-').Append(Describe(process.AngleMax))
                        .Append(",color=").Append(Describe(process.Color));
                }
                break;
            case ColorRect rect:
                text.Append("|color=").Append(Describe(rect.Color));
                break;
            case TextureRect textureRect:
                text.Append("|tex=").Append(textureRect.Texture?.ResourcePath)
                    .Append("|expand=").Append(textureRect.ExpandMode)
                    .Append("|stretch=").Append(textureRect.StretchMode)
                    .Append("|flip=").Append(textureRect.FlipH);
                break;
        }

        return text.ToString();
    }

    private static string DescribeMaterial(Material? material) =>
        material switch
        {
            null => "-",
            CanvasItemMaterial canvasMaterial => "CanvasItem:" + canvasMaterial.BlendMode,
            _ => material.GetType().Name
        };

    private static string Describe(object? value) =>
        value switch
        {
            null => "null",
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            Vector2 vector => "(" + Describe(vector.X) + "," + Describe(vector.Y) + ")",
            Vector3 vector => "(" + Describe(vector.X) + "," + Describe(vector.Y) + "," + Describe(vector.Z) + ")",
            Color color => "(" + Describe(color.R) + "," + Describe(color.G) + "," + Describe(color.B) + ","
                + Describe(color.A) + ")",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    private static string DescribeVariant(Variant value) =>
        value.VariantType switch
        {
            Variant.Type.Float => Describe(value.AsDouble()),
            Variant.Type.Int => value.AsInt64().ToString(CultureInfo.InvariantCulture),
            Variant.Type.Vector2 => Describe(value.AsVector2()),
            Variant.Type.Color => Describe(value.AsColor()),
            Variant.Type.Bool => value.AsBool().ToString(),
            _ => value.VariantType + ":" + value
        };

    private static string DescribeTarget(GodotObject? target)
    {
        if (target is not Node node || !GodotObject.IsInstanceValid(node))
        {
            return target?.GetType().Name ?? "null";
        }

        var parts = new List<string>();
        Node? current = node;
        while (current != null)
        {
            parts.Add(current.Name.ToString());
            if (current.Name.ToString().StartsWith(VfxRootPrefix, StringComparison.Ordinal))
            {
                parts.Reverse();
                return string.Join("/", parts);
            }

            current = current.GetParent();
        }

        return "outside:" + node.GetType().Name + ":" + node.Name;
    }

    private static string TweenLabel(Tween tween) =>
        TweenIds.TryGetValue(tween.GetInstanceId(), out int id) ? "tw" + id : "tw?";

    private static void InstallHooks()
    {
        var harmony = new Harmony("LibraryOfRuina.Verification.PhilosophyVfxTrace");
        Type hooks = typeof(Hooks);
        void Patch(MethodBase? target, string? prefix, string? postfix)
        {
            if (target == null)
            {
                throw new MissingMethodException("PhilosophyVfxTrace hook target missing (" + prefix + postfix + ").");
            }

            harmony.Patch(
                target,
                prefix: prefix == null ? null : new HarmonyMethod(AccessTools.Method(hooks, prefix)),
                postfix: postfix == null ? null : new HarmonyMethod(AccessTools.Method(hooks, postfix)));
        }

        Patch(AccessTools.Method(typeof(Node), nameof(Node.CreateTween)), null, nameof(Hooks.CreateTweenPostfix));
        Patch(AccessTools.Method(typeof(Tween), nameof(Tween.TweenProperty)), null, nameof(Hooks.TweenPropertyPostfix));
        Patch(AccessTools.Method(typeof(Tween), nameof(Tween.TweenMethod)), null, nameof(Hooks.TweenMethodPostfix));
        Patch(AccessTools.Method(typeof(Tween), nameof(Tween.SetParallel)), nameof(Hooks.SetParallelPrefix), null);
        Patch(AccessTools.Method(typeof(Tween), nameof(Tween.SetLoops)), nameof(Hooks.SetLoopsPrefix), null);
        Patch(AccessTools.Method(typeof(Tween), nameof(Tween.SetTrans)), nameof(Hooks.TweenSetTransPrefix), null);
        Patch(AccessTools.Method(typeof(Tween), nameof(Tween.SetEase)), nameof(Hooks.TweenSetEasePrefix), null);
        Patch(AccessTools.Method(typeof(PropertyTweener), nameof(PropertyTweener.SetDelay)),
            nameof(Hooks.TweenerSetDelayPrefix), null);
        Patch(AccessTools.Method(typeof(PropertyTweener), nameof(PropertyTweener.SetTrans)),
            nameof(Hooks.TweenerSetTransPrefix), null);
        Patch(AccessTools.Method(typeof(PropertyTweener), nameof(PropertyTweener.SetEase)),
            nameof(Hooks.TweenerSetEasePrefix), null);
        Patch(AccessTools.Method(typeof(SceneTree), nameof(SceneTree.CreateTimer)), nameof(Hooks.CreateTimerPrefix), null);
        Patch(AccessTools.Method(typeof(LocalOggOneShotPlayer), nameof(LocalOggOneShotPlayer.Play)),
            nameof(Hooks.PlaySfxPrefix), null);
        Patch(AccessTools.Method(typeof(NGame), nameof(NGame.ScreenShake)), nameof(Hooks.ScreenShakePrefix), null);
    }

    private static class Hooks
    {
        internal static void CreateTweenPostfix(Node __instance, Tween __result)
        {
            if (!FromVfx())
            {
                return;
            }

            int id = TweenIds.Count;
            TweenIds[__result.GetInstanceId()] = id;
            Emit("tween tw" + id + " on " + DescribeTarget(__instance));
        }

        internal static void TweenPropertyPostfix(
            Tween __instance,
            GodotObject @object,
            NodePath property,
            Variant finalVal,
            double duration,
            PropertyTweener __result)
        {
            if (!FromVfx())
            {
                return;
            }

            string id = TweenLabel(__instance) + "." + TweenerIds.Count;
            TweenerIds[__result.GetInstanceId()] = id;
            Emit(id + " property " + DescribeTarget(@object) + ":" + property + " -> " + DescribeVariant(finalVal)
                + " in " + Describe(duration));
        }

        internal static void TweenMethodPostfix(
            Tween __instance,
            Variant from,
            Variant to,
            double duration,
            MethodTweener __result)
        {
            if (!FromVfx())
            {
                return;
            }

            string id = TweenLabel(__instance) + "." + TweenerIds.Count;
            TweenerIds[__result.GetInstanceId()] = id;
            Emit(id + " method " + DescribeVariant(from) + " -> " + DescribeVariant(to) + " in " + Describe(duration));
        }

        internal static void SetParallelPrefix(Tween __instance, bool parallel)
        {
            if (FromVfx())
            {
                Emit(TweenLabel(__instance) + " parallel=" + parallel);
            }
        }

        internal static void SetLoopsPrefix(Tween __instance, int loops)
        {
            if (FromVfx())
            {
                Emit(TweenLabel(__instance) + " loops=" + loops);
            }
        }

        internal static void TweenSetTransPrefix(Tween __instance, Tween.TransitionType trans)
        {
            if (FromVfx())
            {
                Emit(TweenLabel(__instance) + " trans=" + trans);
            }
        }

        internal static void TweenSetEasePrefix(Tween __instance, Tween.EaseType ease)
        {
            if (FromVfx())
            {
                Emit(TweenLabel(__instance) + " ease=" + ease);
            }
        }

        internal static void TweenerSetDelayPrefix(PropertyTweener __instance, double delay)
        {
            if (FromVfx())
            {
                Emit(TweenerLabel(__instance) + " delay=" + Describe(delay));
            }
        }

        internal static void TweenerSetTransPrefix(PropertyTweener __instance, Tween.TransitionType trans)
        {
            if (FromVfx())
            {
                Emit(TweenerLabel(__instance) + " trans=" + trans);
            }
        }

        internal static void TweenerSetEasePrefix(PropertyTweener __instance, Tween.EaseType ease)
        {
            if (FromVfx())
            {
                Emit(TweenerLabel(__instance) + " ease=" + ease);
            }
        }

        internal static void CreateTimerPrefix(double timeSec)
        {
            if (!FromVfx())
            {
                return;
            }

            DumpNewNodes("wait");
            Emit("wait " + Describe(timeSec));
        }

        internal static void PlaySfxPrefix(string audioPath, float volumeDb)
        {
            if (FromVfx())
            {
                Emit("sfx " + audioPath + " " + Describe(volumeDb));
            }
        }

        internal static void ScreenShakePrefix(ShakeStrength strength, ShakeDuration duration, float degAngle)
        {
            if (FromVfx())
            {
                Emit("shake " + strength + " " + duration + " " + Describe(degAngle));
            }
        }

        private static string TweenerLabel(PropertyTweener tweener) =>
            TweenerIds.TryGetValue(tweener.GetInstanceId(), out string? id) ? id : "tweener?";
    }

    private static async Task<CombatState> StartFight(string seed)
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        ArmActLikeIt2OneShotVanillaEntry();
        await game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(),
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            ascensionLevel: 0);
        ArmActLikeIt2OneShotVanillaEntry();
        await RunManager.Instance.EnterAct(1, doTransition: false);
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Boss,
            MapPointType.Boss,
            ModelDb.Encounter<PhilosophyFloorLiberationEncounter>().ToMutable(),
            showTransition: false);
        await WaitUntil(
            static () => CombatManager.Instance.IsInProgress && NCombatRoom.Instance != null,
            seed + " combat start");
        await WaitFrames(8);
        return CombatManager.Instance.DebugOnlyGetState()
            ?? throw new InvalidOperationException("Combat state is null.");
    }

    private static void ArmActLikeIt2OneShotVanillaEntry()
    {
        Type? gateType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ActLikeIt2.Runtime.ActSelectionGate", throwOnError: false))
            .FirstOrDefault(static type => type != null);
        PropertyInfo? skipNextFork = gateType?.GetProperty(
            "SkipNextFork",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (skipNextFork?.CanWrite == true)
        {
            skipNextFork.SetValue(null, true);
        }
    }

    private static void CleanupRun()
    {
        if (RunManager.Instance.DebugOnlyGetState() != null)
        {
            RunManager.Instance.CleanUp(graceful: true);
        }
    }

    private static async Task WaitFrames(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task WaitUntil(Func<bool> predicate, string description, int maxFrames = 900)
    {
        for (int frame = 0; frame < maxFrames; frame++)
        {
            if (predicate())
            {
                return;
            }

            SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        throw new TimeoutException("Timed out waiting for " + description + ".");
    }
}

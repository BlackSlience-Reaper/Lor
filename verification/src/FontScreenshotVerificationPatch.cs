using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.abnormalities.PunishingBird;
using LibraryOfRuina.content.abnormalities.ScorchedGirl;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.core.settings.ui;
using LibraryOfRuina.features.ftue;
using LibraryOfRuina.features.intentgraph;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 界面字体与布局的截图场景（窗口模式，不能 headless）：在主菜单就绪后依次打开本模组设置界面（模组名按钮的
/// 选中、悬停、未选中三种颜色）、两页的教程弹窗（两页各一张），再依次打实验体、惩戒鸟、最后的火柴三场战斗，
/// 悬停第一个敌人打开意图图。
/// 每一步把游戏视口存成 PNG，控件在图里的矩形和实际解析到的字体写进 manifest.json，供外部脚本裁剪和拼对比图。
/// <para>
/// 只经过修复前后都存在的入口，同一个验证 DLL 可以配修复前、修复后两个主模组 DLL 各跑一次，两次的截图一一对应。
/// 输出目录由 <c>LOR_FONT_SHOT_DIR</c> 指定。窗口尺寸与语言取自设置存档，对比的两次要保持一致。
/// 意图图默认关闭，除了这里打开的设置项，用户目录里的 <c>LibraryOfRuina/config/intentgraph_display.jsonc</c>
/// 也要预先写成 <c>"enabled": true</c>。
/// </para>
/// </summary>
internal static class FontScreenshotVerificationPatch
{
    private const string VerifyArg = "lor-verify-font-screenshots";
    private const string OutputDirVariable = "LOR_FONT_SHOT_DIR";
    private const string LogPrefix = "[LibraryOfRuina.FontShots.Verify] ";
    private const string ModConfigButtonName = "ExtModConfigButton";

    // 设置后改截其他意图图模组（如 Intent Graph 的 IntentGraph2.Scenes.NIntentGraphPanel）的面板作对照，本模组意图图不打开。
    private static readonly string? ReferencePanelType = Environment.GetEnvironmentVariable("LOR_FONT_SHOT_REFERENCE_PANEL");

    private static readonly List<ShotRecord> Shots = [];
    private static bool _started;
    private static string _outputDir = string.Empty;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            _ = TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            _outputDir = Environment.GetEnvironmentVariable(OutputDirVariable)
                         ?? throw new InvalidOperationException("Set " + OutputDirVariable + " to the screenshot output directory.");
            Directory.CreateDirectory(_outputDir);
            await WaitSeconds(2.0);

            NMainMenu mainMenu = NGame.Instance?.MainMenu ?? throw new InvalidOperationException("Main menu is not available.");
            Log.Info(LogPrefix + "language=" + LocManager.Instance.Language
                     + " viewport=" + mainMenu.GetViewport().GetVisibleRect().Size
                     + " texture=" + mainMenu.GetViewport().GetTexture().GetSize());

            await CaptureSettings(mainMenu);
            await CaptureFtuePopup();
            // 原版实验体（带分支、条件标签和配置注释），另加两只本模组怪物。
            await CaptureIntentGraph("combat_hover", ModelDb.Encounter<TestSubjectBoss>().ToMutable(), RoomType.Boss, MapPointType.Boss);
            await CaptureIntentGraph("combat_hover_bird", ModelDb.Encounter<PunishingBirdStrong>().ToMutable(), RoomType.Monster, MapPointType.Monster);
            await CaptureIntentGraph("combat_hover_scorched", ModelDb.Encounter<ScorchedGirl>().ToMutable(), RoomType.Monster, MapPointType.Monster);

            WriteManifest();
            Log.Info(LogPrefix + "FONT_SHOTS_OK shots=" + Shots.Count + " dir=" + _outputDir);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "FONT_SHOTS_FAILED: " + ex);
            WriteManifest();
            CleanupRun();
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    // ---------------------------------------------------------------- settings

    private static async Task CaptureSettings(NMainMenu mainMenu)
    {
        NMainMenuSubmenuStack stack = mainMenu.SubmenuStack;
        NSettingsScreen settings = stack.PushSubmenuType<NSettingsScreen>();
        await WaitSeconds(1.0);

        // 与玩家点“模组设置”走同一个入口：设置界面里注入的按钮发出 Released。
        NClickableControl entry = settings.FindChild(ModConfigButtonName, true, false) as NClickableControl
                                  ?? throw new InvalidOperationException(ModConfigButtonName + " was not injected into the settings screen.");
        entry.EmitSignal(NClickableControl.SignalName.Released, entry);
        await WaitSeconds(1.5);

        NExtSettingsSubmenu submenu = FindAll<NExtSettingsSubmenu>(stack).FirstOrDefault(static menu => menu.Visible)
                                      ?? throw new InvalidOperationException("Mod settings submenu did not open.");
        NExtModButton modButton = FindAll<NExtModButton>(submenu).FirstOrDefault()
                                  ?? throw new InvalidOperationException("No NExtModButton in the mod settings submenu.");
        NExtActionButton[] actionButtons = FindAll<NExtActionButton>(submenu)
            .Where(static button => button.IsVisibleInTree())
            .Take(3)
            .ToArray();
        if (actionButtons.Length == 0)
        {
            throw new InvalidOperationException("No visible NExtActionButton in the mod settings submenu.");
        }

        // 同一个模组按钮的三种文字颜色：选中未悬停（金色）、悬停（白色）、未选中（灰色）。
        modButton.GetViewport().GuiReleaseFocus();
        await WaitSeconds(0.5);
        var crops = new List<CropRecord> { Crop("mod_button", modButton) };
        for (int i = 0; i < actionButtons.Length; i++)
        {
            crops.Add(Crop("action_button_" + i, actionButtons[i]));
        }

        await Capture("settings_selected", crops);

        modButton.GrabFocus();
        await WaitSeconds(0.5);
        await Capture("settings_hover", [Crop("mod_button", modButton)]);

        modButton.GetViewport().GuiReleaseFocus();
        modButton.SetActiveState(false);
        await WaitSeconds(0.5);
        await Capture("settings_normal", [Crop("mod_button", modButton)]);
        modButton.SetActiveState(true);

        stack.Pop();
        await WaitSeconds(0.5);
        stack.Pop();
        await WaitSeconds(0.8);
    }

    // ---------------------------------------------------------------- ftue

    private static async Task CaptureFtuePopup()
    {
        NModalContainer modal = NModalContainer.Instance ?? throw new InvalidOperationException("NModalContainer is not available.");
        // 翻页按钮只在多页弹窗里出现；用已有的两条教程文本拼一个两页弹窗。
        NLibraryOfRuinaFtuePopup popup = NLibraryOfRuinaFtuePopup.CreateMultiPage(
            "lor_font_screenshot_ftue",
            ["LOR_UPDATE_LOG_LOCATION_FTUE_TITLE", "LOR_ABNORMALITY_PAGE_REWARD_FTUE_TITLE"],
            ["LOR_UPDATE_LOG_LOCATION_FTUE_BODY", "LOR_ABNORMALITY_PAGE_REWARD_FTUE_BODY"]);
        modal.Add(popup, true);
        await WaitSeconds(1.2);

        var crops = new List<CropRecord>();
        foreach (string name in new[] { "NextBtn", "PageCount" })
        {
            if (popup.FindChild(name, true, false) is Control control)
            {
                crops.Add(Crop("ftue_" + name, control));
            }
        }

        crops.Add(Crop("ftue_panel", (Control)popup.FindChild("FtuePopup", true, false)!));
        await Capture("ftue_popup", crops);

        // 第二页：上一页按钮与确认按钮出现。
        if (popup.FindChild("NextBtn", true, false) is Button next)
        {
            next.EmitSignal(BaseButton.SignalName.Pressed);
            await WaitSeconds(1.0);
            var pageTwo = new List<CropRecord> { Crop("ftue_panel", (Control)popup.FindChild("FtuePopup", true, false)!) };
            foreach (string name in new[] { "PrevBtn", "PageCount", "ConfirmBtn" })
            {
                if (popup.FindChild(name, true, false) is Control control)
                {
                    pageTwo.Add(Crop("ftue_" + name, control));
                }
            }

            await Capture("ftue_popup_page2", pageTwo);
        }

        modal.Clear();
        await WaitSeconds(0.8);
    }

    // ---------------------------------------------------------------- intent graph

    private static async Task CaptureIntentGraph(string shotName, EncounterModel encounter, RoomType roomType, MapPointType mapPointType)
    {
        // 意图图默认关闭：这里相当于玩家在模组设置里打开；另一个开关（用户目录里的 intentgraph_display.jsonc）由启动脚本预置。
        LibraryOfRuinaSettings.IntentGraphEnabled = ReferencePanelType == null;
        CombatState state = await StartFight(encounter, roomType, mapPointType, "LORFONTSHOTS");
        try
        {
            await WaitSeconds(3.0);
            // 首次进战斗会弹原版或本模组的教程，先关掉，免得挡住意图图。
            for (int i = 0; i < 5 && NModalContainer.Instance?.OpenModal != null; i++)
            {
                NModalContainer.Instance.Clear();
                await WaitSeconds(0.8);
            }

            Creature enemy = state.Enemies.First(static creature => creature.IsAlive);
            NCreature node = NCombatRoom.Instance?.GetCreatureNode(enemy)
                             ?? throw new InvalidOperationException("No creature node for the enemy.");

            // 与鼠标移到怪物上相同：命中框发出 MouseEntered，原版 OnFocus 显示悬停提示，本模组的后缀再打开意图图。
            node.Hitbox.EmitSignal(Control.SignalName.MouseEntered);
            await WaitSeconds(1.2);

            Node hoverTipsContainer = NGame.Instance?.HoverTipsContainer
                ?? throw new InvalidOperationException("Hover tip container is unavailable.");

            if (ReferencePanelType is { } referenceType)
            {
                // 对照组：只截参考模组自己画的意图图，本模组的意图图保持关闭。
                Control referencePanel = FindAll<Control>(hoverTipsContainer)
                                             .FirstOrDefault(candidate => candidate.Visible && candidate.GetType().FullName == referenceType)
                                         ?? throw new InvalidOperationException(referenceType + " did not open on hover.");
                await Capture(shotName, [Crop("intent_panel", referencePanel)]);
                return;
            }

            NMonsterIntentGraphPanel panel = FindAll<NMonsterIntentGraphPanel>(hoverTipsContainer)
                                                 .FirstOrDefault(static candidate => candidate.Visible)
                                             ?? throw new InvalidOperationException("Intent graph panel did not open on hover.");
            Control monsterName = panel.FindChild("MonsterName", true, false) as Control
                                  ?? throw new InvalidOperationException("Intent graph panel has no MonsterName.");
            NIntentGraph graph = FindAll<NIntentGraph>(panel).First();
            var crops = new List<CropRecord>
            {
                Crop("intent_panel", panel),
                Crop("monster_name", monsterName),
                Crop("intent_graph", graph),
            };

            await Capture(shotName, crops);
            node.Hitbox.EmitSignal(Control.SignalName.MouseExited);
            await WaitSeconds(0.6);
        }
        finally
        {
            CleanupRun();
            await WaitUntil(
                static () => !CombatManager.Instance.IsInProgress && RunManager.Instance.DebugOnlyGetState() == null,
                "run cleanup");
        }
    }

    // ---------------------------------------------------------------- capture

    private static async Task Capture(string name, List<CropRecord> crops)
    {
        SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Image image = tree.Root.GetViewport().GetTexture().GetImage();
        string file = name + ".png";
        Error error = image.SavePng(Path.Combine(_outputDir, file));
        if (error != Error.Ok)
        {
            throw new InvalidOperationException("SavePng failed for " + file + ": " + error);
        }

        Shots.Add(new ShotRecord(name, file, image.GetWidth(), image.GetHeight(), crops));
        Log.Info(LogPrefix + "SHOT|" + name + "|" + image.GetWidth() + "x" + image.GetHeight() + "|"
                 + string.Join(";", crops.Select(static crop => crop.Name + "=" + crop.Font)));
    }

    private static CropRecord Crop(string name, Control control)
    {
        // 控件矩形换算到视口贴图的像素坐标：画布（含 CanvasLayer）变换，再乘窗口拉伸变换。
        Transform2D transform = control.GetViewport().GetFinalTransform() * control.GetGlobalTransformWithCanvas();
        Vector2[] corners =
        [
            transform * Vector2.Zero,
            transform * new Vector2(control.Size.X, 0f),
            transform * new Vector2(0f, control.Size.Y),
            transform * control.Size,
        ];
        float left = corners.Min(static point => point.X);
        float top = corners.Min(static point => point.Y);
        float right = corners.Max(static point => point.X);
        float bottom = corners.Max(static point => point.Y);
        return new CropRecord(
            name,
            [Mathf.FloorToInt(left), Mathf.FloorToInt(top), Mathf.CeilToInt(right - left), Mathf.CeilToInt(bottom - top)],
            DescribeFont(control));
    }

    /// <summary>控件（或它的第一个文字子节点）实际用来绘制的字体资源路径与字体名。</summary>
    private static string DescribeFont(Control control)
    {
        Control? textControl = control is Label or Button or RichTextLabel
            ? control
            : FindAll<Control>(control).FirstOrDefault(static child => child is Label or Button or RichTextLabel);
        if (textControl == null)
        {
            return "-";
        }

        Font font = textControl switch
        {
            Label { LabelSettings.Font: { } settingsFont } => settingsFont,
            RichTextLabel => textControl.GetThemeFont("normal_font", "RichTextLabel"),
            Button => textControl.GetThemeFont("font", "Button"),
            _ => textControl.GetThemeFont("font", "Label"),
        };
        string source = textControl is Label { LabelSettings.Font: not null } ? "label_settings" : "theme";
        string text = textControl switch
        {
            Label label => label.Text,
            RichTextLabel rich => rich.Text,
            _ => ((Button)textControl).Text,
        };
        return source + ":" + (string.IsNullOrEmpty(font.ResourcePath) ? "<no path>" : font.ResourcePath)
               + " (" + font.GetFontName() + ") text=" + text.Replace("\n", " ");
    }

    private static void WriteManifest()
    {
        if (string.IsNullOrEmpty(_outputDir))
        {
            return;
        }

        var manifest = new
        {
            language = LocManager.Instance?.Language,
            shots = Shots.Select(static shot => new
            {
                name = shot.Name,
                file = shot.File,
                width = shot.Width,
                height = shot.Height,
                crops = shot.Crops.Select(static crop => new { name = crop.Name, rect = crop.Rect, font = crop.Font }),
            }),
        };
        File.WriteAllText(
            Path.Combine(_outputDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ---------------------------------------------------------------- run helpers

    private static async Task<CombatState> StartFight(
        EncounterModel encounter,
        RoomType roomType,
        MapPointType mapPointType,
        string seed)
    {
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame.Instance is null.");
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
        await RunManager.Instance.EnterRoomDebug(roomType, mapPointType, encounter, showTransition: false);
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
            .Select(static assembly => assembly.GetType("ActLikeIt2.Runtime.ActSelectionGate", throwOnError: false))
            .FirstOrDefault(static type => type != null);
        System.Reflection.PropertyInfo? skipNextFork = gateType?.GetProperty(
            "SkipNextFork",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
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

    private static IEnumerable<T> FindAll<T>(Node root) where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (T nested in FindAll<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static async Task WaitSeconds(double seconds)
    {
        SceneTree tree = NGame.Instance?.GetTree() ?? (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
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

    private sealed record CropRecord(string Name, int[] Rect, string Font);

    private sealed record ShotRecord(string Name, string File, int Width, int Height, List<CropRecord> Crops);
}

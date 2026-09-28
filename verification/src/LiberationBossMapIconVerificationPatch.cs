using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class LiberationBossMapIconVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-liberation-boss-icons";
    private const string LogPrefix =
        "[LibraryOfRuina.LiberationBossIcons.Verify] ";

    internal static readonly string[] FloorIds =
    [
        "history",
        "technology",
        "art",
        "language",
        "social",
        "philosophy",
        "literature"
    ];

    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() =>
        {
            TaskHelper.RunSafely(RunAsync());
        }).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static Task RunAsync()
    {
        try
        {
            VerifyAll();
            Log.Info(LogPrefix + "LIBERATION_BOSS_ICONS_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "LIBERATION_BOSS_ICONS_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }

        return Task.CompletedTask;
    }

    internal static void VerifyAll()
    {
        foreach (string floorId in FloorIds)
        {
            VerifyFloor(floorId);
        }
    }

    internal static void VerifyFloor(string floorId)
    {
        string mapRoot =
            $"res://images/map/placeholder/{floorId}_floor_liberation_encounter_icon";
        Image main = LoadImage(mapRoot + ".png");
        Image outline = LoadImage(mapRoot + "_outline.png");
        Require(main.GetSize() == outline.GetSize(),
            floorId + " map icon pair dimensions differ.");

        IconMetrics mainMetrics = MeasureWhiteMask(
            main,
            floorId + " main");
        IconMetrics outlineMetrics = MeasureWhiteMask(
            outline,
            floorId + " outline");
        int totalPixels = main.GetWidth() * main.GetHeight();
        double mainOccupancy = mainMetrics.PixelCount / (double)totalPixels;
        double outlineOccupancy =
            outlineMetrics.PixelCount / (double)totalPixels;
        double expansionRatio = outlineMetrics.PixelCount
            / (double)mainMetrics.PixelCount;

        Require(mainOccupancy is >= 0.035d and <= 0.085d,
            floorId + " main line mask occupancy is outside 3.5%-8.5%: "
            + mainOccupancy.ToString("P2") + ".");
        Require(outlineOccupancy is >= 0.14d and <= 0.31d,
            floorId + " outline mask occupancy is outside 14%-31%: "
            + outlineOccupancy.ToString("P2") + ".");
        Require(expansionRatio is >= 2.5d and <= 5.2d,
            floorId + " outline is not a compact dilation of its line mask: "
            + expansionRatio.ToString("F2") + ".");
        Require(outlineMetrics.MinX <= mainMetrics.MinX - 3
                && outlineMetrics.MinY <= mainMetrics.MinY - 3
                && outlineMetrics.MaxX >= mainMetrics.MaxX + 3
                && outlineMetrics.MaxY >= mainMetrics.MaxY + 3,
            floorId + " outline does not expand at least three pixels on every side.");
        Require(OutlineCoversMain(main, outline),
            floorId + " outline does not cover every main-line pixel.");

        string historyRoot =
            $"res://images/ui/run_history/{floorId}_floor_liberation_encounter";
        Image historyMain = LoadImage(historyRoot + ".png");
        Image historyOutline = LoadImage(historyRoot + "_outline.png");
        Require(historyMain.GetSize() == historyOutline.GetSize()
                && historyMain.GetData().SequenceEqual(
                    historyOutline.GetData()),
            floorId + " run-history colored icon pair changed independently.");

        Log.Info(LogPrefix + floorId
            + $" main={mainOccupancy:P2} outline={outlineOccupancy:P2} "
            + $"ratio={expansionRatio:F2}");
    }

    private static Image LoadImage(string path)
    {
        Require(ResourceLoader.Exists(path), "Missing icon resource: " + path);
        Texture2D texture = ResourceLoader.Load<Texture2D>(path)
            ?? throw new InvalidOperationException(
                "Failed to load icon texture: " + path);
        Image image = texture.GetImage();
        if (image.IsCompressed())
        {
            Require(image.Decompress() == Error.Ok,
                "Failed to decompress icon texture: " + path);
        }

        if (image.GetFormat() != Image.Format.Rgba8)
        {
            image.Convert(Image.Format.Rgba8);
        }

        return image;
    }

    private static IconMetrics MeasureWhiteMask(Image image, string label)
    {
        int count = 0;
        int minX = image.GetWidth();
        int minY = image.GetHeight();
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                Color pixel = image.GetPixel(x, y);
                if (pixel.A <= 1f / 255f)
                {
                    continue;
                }

                Require(pixel.R >= 0.995f
                        && pixel.G >= 0.995f
                        && pixel.B >= 0.995f,
                    label + " contains a colored source pixel; map tinting "
                    + "must remain runtime-owned.");
                count++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        Require(count > 0, label + " is empty.");
        return new IconMetrics(count, minX, minY, maxX, maxY);
    }

    private static bool OutlineCoversMain(Image main, Image outline)
    {
        for (int y = 0; y < main.GetHeight(); y++)
        {
            for (int x = 0; x < main.GetWidth(); x++)
            {
                if (main.GetPixel(x, y).A > 1f / 255f
                    && outline.GetPixel(x, y).A <= 1f / 255f)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record IconMetrics(
        int PixelCount,
        int MinX,
        int MinY,
        int MaxX,
        int MaxY);
}

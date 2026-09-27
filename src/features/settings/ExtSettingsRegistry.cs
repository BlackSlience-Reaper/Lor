using System.Linq;

namespace LibraryOfRuina.features.settings;

internal static class ExtSettingsRegistry
{
    private static readonly Dictionary<string, ExtModSettings> Configs = new();

    public static void Register(string modId, ExtModSettings config)
    {
        if (!config.HasSettings()) return;
        config.ModId = modId;
        Configs[modId] = config;
    }

    public static ExtModSettings? Get(string? modId)
    {
        if (modId == null) return null;
        return Configs.GetValueOrDefault(modId);
    }

    public static T? Get<T>() where T : ExtModSettings
    {
        return Configs.Values.OfType<T>().FirstOrDefault();
    }

    public static List<ExtModSettings> GetAll() =>
        Configs.Values.OrderBy(m => m.ModId).ToList();
}

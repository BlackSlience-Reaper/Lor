using System.Reflection;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.compat;

internal static class LocalContextCompat
{
    public static Player? GetMe(object? playerSource)
    {
        if (!LocalContext.NetId.HasValue || playerSource == null)
        {
            return null;
        }

        if (playerSource is IPlayerCollection playerCollection)
        {
            return LocalContext.GetMe(playerCollection);
        }

        MethodInfo? getPlayer = playerSource.GetType().GetMethod(
            "GetPlayer",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            [typeof(ulong)],
            modifiers: null);

        return getPlayer?.Invoke(playerSource, [LocalContext.NetId.Value]) as Player;
    }
}

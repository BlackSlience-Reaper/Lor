using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.relics.BookShadow;

internal static class BookShadowHostPlayerResolver
{
    internal static Player? Resolve(IRunState runState)
    {
        if (runState.Players.Count == 1)
        {
            return runState.Players[0];
        }

        try
        {
            var netService = RunManager.Instance.NetService;
            ulong hostNetId = netService.Type == NetGameType.Client
                               && netService is NetClientGameService client
                ? client.HostNetId
                : netService.NetId;

            foreach (Player player in runState.Players)
            {
                if (player.NetId == hostNetId)
                {
                    return player;
                }
            }
        }
        catch
        {
            // The resolver is also reached by non-runtime tooling, where a
            // network service may not exist yet.
        }

        return null;
    }
}

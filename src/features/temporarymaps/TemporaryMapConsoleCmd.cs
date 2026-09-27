using System;
using System.Linq;
using LibraryOfRuina.debug;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.features.temporarymaps;

public sealed class TemporaryMapConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "tempmap";

    public override string Args => "<definitionId:string>";

    public override string Description => "Multiplayer-safe debug entry into a registered temporary map definition.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 1)
        {
            return new CmdResult(success: false, "There must be one temporary-map definition id argument.");
        }

        if (!RunManager.Instance.IsInProgress)
        {
            return MultiplayerJumpConsoleCmds.NoRunInProgress();
        }

        if (RunManager.Instance.DebugOnlyGetState() is not { } state)
        {
            return new CmdResult(success: false, "Run state is unavailable.");
        }

        if (TemporaryMapController.IsActive(state))
        {
            return new CmdResult(success: false, "A temporary map is already active.");
        }

        if (!TemporaryMapRegistry.TryGetDefinition(args[0], out TemporaryMapDefinition? definition))
        {
            return new CmdResult(success: false, "Temporary map '" + args[0] + "' not found.");
        }

        Player? player = issuingPlayer ?? state.Players.FirstOrDefault();
        if (player == null)
        {
            return new CmdResult(success: false, "No player is available for the temporary-map debug jump.");
        }

        return new CmdResult(
            TemporaryMapController.EnterFromDebugCommand(player, definition.Id),
            success: true,
            "Entering temporary map: '" + definition.DisplayName + "'");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            List<string> candidates = TemporaryMapRegistry.AllDefinitions
                .Select(static definition => definition.Id)
                .ToList();
            return CompleteArgument(candidates, Array.Empty<string>(), args.FirstOrDefault() ?? string.Empty);
        }

        return MultiplayerJumpConsoleCmds.NoMoreArguments(CmdName);
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Exceptions;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.debug;

public sealed class MultiFightConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "multifight";

    public override string Args => "<id:string>";

    public override string Description => "Multiplayer-safe debug jump to a specific encounter.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 1)
        {
            return new CmdResult(success: false, "There must be one encounter id argument.");
        }

        if (!RunManager.Instance.IsInProgress)
        {
            return MultiplayerJumpConsoleCmds.NoRunInProgress();
        }

        ModelId modelId = new(ModelId.SlugifyCategory<EncounterModel>(), args[0].ToUpperInvariant());
        EncounterModel encounterModel;
        try
        {
            encounterModel = ModelDb.GetById<EncounterModel>(modelId).ToMutable();
        }
        catch (ModelNotFoundException)
        {
            return new CmdResult(success: false, "Encounter '" + modelId.Entry + "' not found");
        }

        encounterModel.DebugRandomizeRng();
        Task task = RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Unassigned, encounterModel);
        return new CmdResult(task, success: true, "Jumped to encounter: '" + encounterModel.Id.Entry + "'");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            List<string> candidates = ModelDb.AllEncounters.Select(static encounter => encounter.Id.Entry).ToList();
            return CompleteArgument(candidates, Array.Empty<string>(), args.FirstOrDefault() ?? "");
        }

        return MultiplayerJumpConsoleCmds.NoMoreArguments(CmdName);
    }
}

public sealed class MultiEventConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "multievent";

    public override string Args => "<id:string>";

    public override string Description => "Multiplayer-safe debug jump to a specific event.";

    public override bool IsNetworked => true;

    private static IEnumerable<EventModel> Events => ModelDb.AllEvents.Concat(ModelDb.AllAncients);

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 1)
        {
            return new CmdResult(success: false, "There must be one event id argument.");
        }

        if (!RunManager.Instance.IsInProgress)
        {
            return MultiplayerJumpConsoleCmds.NoRunInProgress();
        }

        string eventName = args[0].ToUpperInvariant();
        EventModel? eventModel = Events.FirstOrDefault(candidate => candidate.Id.Entry == eventName);
        if (eventModel == null)
        {
            return new CmdResult(success: false, "Event '" + eventName + "' not found");
        }

        MapPointType mapPointType = eventModel is AncientEventModel ? MapPointType.Ancient : MapPointType.Unknown;
        Task task = RunManager.Instance.EnterRoomDebug(RoomType.Event, mapPointType, eventModel);
        return new CmdResult(task, success: true, "Jumped to event: '" + eventModel.Id.Entry + "'");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            List<string> candidates = Events.Select(static eventModel => eventModel.Id.Entry).ToList();
            return CompleteArgument(candidates, Array.Empty<string>(), args.FirstOrDefault() ?? "");
        }

        return MultiplayerJumpConsoleCmds.NoMoreArguments(CmdName);
    }
}

internal static class MultiplayerJumpConsoleCmds
{
    public static CmdResult NoRunInProgress()
    {
        return new CmdResult(success: false, "A run is currently not in progress!");
    }

    public static CompletionResult NoMoreArguments(string cmdName)
    {
        return new CompletionResult
        {
            Type = CompletionType.Argument,
            ArgumentContext = cmdName
        };
    }
}

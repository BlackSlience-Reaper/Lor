using System;
using System.Collections.ObjectModel;
using System.Linq;
using LibraryOfRuina.content.guests.YunOffice;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.features.temporarymaps;

public enum TemporaryMapNodeContentKind
{
    FixedRoom,
    Event,
    Encounter
}

public sealed class TemporaryMapNodeDefinition
{
    public MapCoord Coord { get; }

    public MapPointType PointType { get; }

    public TemporaryMapNodeContentKind ContentKind { get; }

    public ModelId? ModelId { get; }

    public RoomType? FixedRoomType { get; }

    public IReadOnlyList<MapCoord> ChildCoords { get; }

    private TemporaryMapNodeDefinition(
        MapCoord coord,
        MapPointType pointType,
        TemporaryMapNodeContentKind contentKind,
        ModelId? modelId,
        RoomType? fixedRoomType,
        IEnumerable<MapCoord>? childCoords)
    {
        Coord = coord;
        PointType = pointType;
        ContentKind = contentKind;
        ModelId = modelId;
        FixedRoomType = fixedRoomType;
        ChildCoords = new ReadOnlyCollection<MapCoord>((childCoords ?? []).Distinct().ToList());

        if (coord.col < 0 || coord.row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coord), "Temporary map coordinates must be non-negative.");
        }

        switch (contentKind)
        {
            case TemporaryMapNodeContentKind.FixedRoom:
                if (fixedRoomType is not RoomType.Treasure and not RoomType.Shop and not RoomType.RestSite)
                {
                    throw new ArgumentException(
                        "Fixed temporary-map rooms currently support only Treasure, Shop, and RestSite.",
                        nameof(fixedRoomType));
                }

                break;

            case TemporaryMapNodeContentKind.Event:
            case TemporaryMapNodeContentKind.Encounter:
                if (modelId == null || modelId == ModelId.none)
                {
                    throw new ArgumentException("Event and encounter nodes require a model id.", nameof(modelId));
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(contentKind), contentKind, null);
        }
    }

    public static TemporaryMapNodeDefinition Event<TEvent>(
        MapCoord coord,
        IEnumerable<MapCoord>? childCoords = null,
        MapPointType? pointType = null)
        where TEvent : EventModel
    {
        return Event(coord, ModelDb.GetId<TEvent>(), childCoords, pointType ?? MapPointType.Unknown);
    }

    public static TemporaryMapNodeDefinition Event(
        MapCoord coord,
        ModelId eventId,
        IEnumerable<MapCoord>? childCoords = null,
        MapPointType pointType = MapPointType.Unknown)
    {
        return new TemporaryMapNodeDefinition(
            coord,
            pointType,
            TemporaryMapNodeContentKind.Event,
            eventId,
            null,
            childCoords);
    }

    public static TemporaryMapNodeDefinition Ancient<TAncient>(
        MapCoord coord,
        IEnumerable<MapCoord>? childCoords = null,
        MapPointType pointType = MapPointType.Ancient)
        where TAncient : AncientEventModel
    {
        return Event(coord, ModelDb.GetId<TAncient>(), childCoords, pointType);
    }

    public static TemporaryMapNodeDefinition Encounter<TEncounter>(
        MapCoord coord,
        IEnumerable<MapCoord>? childCoords = null,
        MapPointType pointType = MapPointType.Monster)
        where TEncounter : EncounterModel
    {
        return Encounter(coord, ModelDb.GetId<TEncounter>(), childCoords, pointType);
    }

    public static TemporaryMapNodeDefinition Encounter(
        MapCoord coord,
        ModelId encounterId,
        IEnumerable<MapCoord>? childCoords = null,
        MapPointType pointType = MapPointType.Monster)
    {
        return new TemporaryMapNodeDefinition(
            coord,
            pointType,
            TemporaryMapNodeContentKind.Encounter,
            encounterId,
            null,
            childCoords);
    }

    public static TemporaryMapNodeDefinition Room(
        MapCoord coord,
        MapPointType pointType,
        RoomType roomType,
        IEnumerable<MapCoord>? childCoords = null)
    {
        return new TemporaryMapNodeDefinition(
            coord,
            pointType,
            TemporaryMapNodeContentKind.FixedRoom,
            null,
            roomType,
            childCoords);
    }

    public static TemporaryMapNodeDefinition Treasure(
        MapCoord coord,
        IEnumerable<MapCoord>? childCoords = null,
        MapPointType pointType = MapPointType.Treasure)
    {
        return Room(coord, pointType, RoomType.Treasure, childCoords);
    }

    public static TemporaryMapNodeDefinition Shop(
        MapCoord coord,
        IEnumerable<MapCoord>? childCoords = null,
        MapPointType pointType = MapPointType.Shop)
    {
        return Room(coord, pointType, RoomType.Shop, childCoords);
    }

    public static TemporaryMapNodeDefinition RestSite(
        MapCoord coord,
        IEnumerable<MapCoord>? childCoords = null,
        MapPointType pointType = MapPointType.RestSite)
    {
        return Room(coord, pointType, RoomType.RestSite, childCoords);
    }

    internal AbstractRoom CreateRoom(RunState runState)
    {
        return ContentKind switch
        {
            TemporaryMapNodeContentKind.Event => new EventRoom(ModelDb.GetById<EventModel>(ModelId!)),
            TemporaryMapNodeContentKind.Encounter => new CombatRoom(
                ModelDb.GetById<EncounterModel>(ModelId!).ToMutable(),
                runState),
            TemporaryMapNodeContentKind.FixedRoom => CreateFixedRoom(runState),
            _ => throw new ArgumentOutOfRangeException(
                nameof(ContentKind),
                ContentKind,
                "Unsupported temporary-map content kind.")
        };
    }

    private AbstractRoom CreateFixedRoom(RunState runState)
    {
        return FixedRoomType switch
        {
            RoomType.Treasure => new TreasureRoom(runState.CurrentActIndex),
            RoomType.Shop => new MerchantRoom(),
            RoomType.RestSite => new RestSiteRoom(),
            _ => throw new InvalidOperationException(
                "Unsupported fixed temporary-map room type: " + FixedRoomType)
        };
    }
}

public sealed class TemporaryMapDefinition
{
    private readonly Dictionary<MapCoord, TemporaryMapNodeDefinition> _nodesByCoord;

    public string Id { get; }

    public string DisplayName { get; }

    public IReadOnlyList<TemporaryMapNodeDefinition> Nodes { get; }

    public IReadOnlyList<MapCoord> EntryCoords { get; }

    public MapCoord TerminalCoord { get; }

    internal int VisibleGridWidth { get; }

    internal int RuntimeGridHeight { get; }

    internal int HiddenAnchorColumn { get; }

    internal MapCoord RuntimeTerminalCoord => ToRuntimeCoord(TerminalCoord);

    public TemporaryMapDefinition(
        string id,
        string displayName,
        IEnumerable<TemporaryMapNodeDefinition> nodes,
        MapCoord terminalCoord)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Temporary map id cannot be empty.", nameof(id));
        }

        Id = id.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        TerminalCoord = terminalCoord;

        List<TemporaryMapNodeDefinition> materializedNodes = nodes?.ToList()
            ?? throw new ArgumentNullException(nameof(nodes));
        if (materializedNodes.Count == 0)
        {
            throw new ArgumentException("Temporary map definitions require at least one node.", nameof(nodes));
        }

        _nodesByCoord = materializedNodes.ToDictionary(node => node.Coord);
        Nodes = new ReadOnlyCollection<TemporaryMapNodeDefinition>(materializedNodes);

        HashSet<MapCoord> childCoords = [];
        foreach (TemporaryMapNodeDefinition node in materializedNodes)
        {
            foreach (MapCoord childCoord in node.ChildCoords)
            {
                if (!_nodesByCoord.ContainsKey(childCoord))
                {
                    throw new InvalidOperationException(
                        $"Temporary map '{Id}' references missing child coordinate {childCoord} from {node.Coord}.");
                }

                if (childCoord.row <= node.Coord.row)
                {
                    throw new InvalidOperationException(
                        $"Temporary map '{Id}' must progress downward. Child {childCoord} is not below parent {node.Coord}.");
                }

                childCoords.Add(childCoord);
            }
        }

        if (!_nodesByCoord.TryGetValue(terminalCoord, out TemporaryMapNodeDefinition? terminalNode))
        {
            throw new InvalidOperationException(
                $"Temporary map '{Id}' terminal coordinate {terminalCoord} is not defined.");
        }

        if (terminalNode.ChildCoords.Count > 0)
        {
            throw new InvalidOperationException(
                $"Temporary map '{Id}' terminal node {terminalCoord} must not have child nodes.");
        }

        List<MapCoord> entryCoords = materializedNodes
            .Select(node => node.Coord)
            .Where(coord => !childCoords.Contains(coord))
            .OrderBy(coord => coord)
            .ToList();
        if (entryCoords.Count == 0)
        {
            throw new InvalidOperationException(
                $"Temporary map '{Id}' does not have an entry node. At least one node must have no parent.");
        }

        List<MapCoord> leafCoords = materializedNodes
            .Where(node => node.ChildCoords.Count == 0)
            .Select(node => node.Coord)
            .OrderBy(coord => coord)
            .ToList();
        if (leafCoords.Count != 1 || leafCoords[0] != terminalCoord)
        {
            throw new InvalidOperationException(
                $"Temporary map '{Id}' currently supports exactly one leaf node, and it must be the terminal node.");
        }

        EntryCoords = new ReadOnlyCollection<MapCoord>(entryCoords);

        int maxCol = materializedNodes.Max(node => node.Coord.col);
        int maxRow = materializedNodes.Max(node => node.Coord.row);
        VisibleGridWidth = Math.Max(1, maxCol + 1);
        RuntimeGridHeight = maxRow + 2;

        int averageEntryColumn = (int)Math.Round(entryCoords.Average(static coord => coord.col), MidpointRounding.AwayFromZero);
        HiddenAnchorColumn = Math.Clamp(averageEntryColumn, 0, VisibleGridWidth - 1);
    }

    internal bool TryGetNode(MapCoord definitionCoord, out TemporaryMapNodeDefinition node)
    {
        return _nodesByCoord.TryGetValue(definitionCoord, out node!);
    }

    internal MapCoord ToRuntimeCoord(MapCoord definitionCoord)
    {
        return new MapCoord(definitionCoord.col, definitionCoord.row + 1);
    }

    internal MapCoord ToDefinitionCoord(MapCoord runtimeCoord)
    {
        return new MapCoord(runtimeCoord.col, runtimeCoord.row - 1);
    }
}

public static class TemporaryMapRegistry
{
    private static readonly Dictionary<string, TemporaryMapDefinition> Definitions =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool _defaultsRegistered;

    public static IEnumerable<TemporaryMapDefinition> AllDefinitions => Definitions.Values.OrderBy(static definition => definition.Id);

    public static void Register(TemporaryMapDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definitions[definition.Id] = definition;
    }

    public static bool TryGetDefinition(string definitionId, out TemporaryMapDefinition definition)
    {
        return Definitions.TryGetValue(definitionId, out definition!);
    }

    internal static void InitializeDefaults()
    {
        if (_defaultsRegistered)
        {
            return;
        }

        _defaultsRegistered = true;
        Register(new TemporaryMapDefinition(
            "debug_demo",
            "Debug Demo",
            [
                TemporaryMapNodeDefinition.Event<SelfHelpBook>(new MapCoord(1, 0), [new MapCoord(1, 1)]),
                TemporaryMapNodeDefinition.Encounter<YunOfficeNormal>(new MapCoord(1, 1))
            ],
            new MapCoord(1, 1)));
    }
}

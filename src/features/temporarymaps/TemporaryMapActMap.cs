using MegaCrit.Sts2.Core.Map;

namespace LibraryOfRuina.features.temporarymaps;

internal sealed class TemporaryMapActMap : ActMap
{
    private readonly Dictionary<MapCoord, TemporaryMapNodeDefinition> _runtimeNodes = new();

    protected override MapPoint?[,] Grid { get; }

    public override MapPoint BossMapPoint { get; }

    public override MapPoint StartingMapPoint { get; }

    public TemporaryMapDefinition Definition { get; }

    public MapCoord TerminalCoord => Definition.RuntimeTerminalCoord;

    public static TemporaryMapActMap Create(TemporaryMapDefinition definition)
    {
        return new TemporaryMapActMap(definition);
    }

    private TemporaryMapActMap(TemporaryMapDefinition definition)
    {
        Definition = definition;
        Grid = new MapPoint[definition.VisibleGridWidth, definition.RuntimeGridHeight];
        StartingMapPoint = CreateSpecialPoint(definition.HiddenAnchorColumn, 0, MapPointType.Unassigned);
        BossMapPoint = CreateSpecialPoint(definition.HiddenAnchorColumn, definition.RuntimeGridHeight, MapPointType.Boss);

        Dictionary<MapCoord, MapPoint> runtimePoints = new();
        foreach (TemporaryMapNodeDefinition node in definition.Nodes)
        {
            MapCoord runtimeCoord = definition.ToRuntimeCoord(node.Coord);
            MapPoint runtimePoint = CreatePathPoint(runtimeCoord, node.PointType);
            runtimePoints[runtimeCoord] = runtimePoint;
            _runtimeNodes[runtimeCoord] = node;
        }

        foreach (TemporaryMapNodeDefinition node in definition.Nodes)
        {
            MapPoint parent = runtimePoints[definition.ToRuntimeCoord(node.Coord)];
            foreach (MapCoord childCoord in node.ChildCoords)
            {
                parent.AddChildPoint(runtimePoints[definition.ToRuntimeCoord(childCoord)]);
            }
        }

        foreach (MapCoord entryCoord in definition.EntryCoords)
        {
            MapPoint runtimeEntry = runtimePoints[definition.ToRuntimeCoord(entryCoord)];
            StartingMapPoint.AddChildPoint(runtimeEntry);
            startMapPoints.Add(runtimeEntry);
        }

        runtimePoints[TerminalCoord].AddChildPoint(BossMapPoint);
    }

    public bool TryGetNode(MapCoord runtimeCoord, out TemporaryMapNodeDefinition node)
    {
        return _runtimeNodes.TryGetValue(runtimeCoord, out node!);
    }

    private MapPoint CreatePathPoint(MapCoord runtimeCoord, MapPointType pointType)
    {
        MapPoint point = new(runtimeCoord.col, runtimeCoord.row)
        {
            PointType = pointType,
            CanBeModified = false
        };

        Grid[runtimeCoord.col, runtimeCoord.row] = point;
        return point;
    }

    private static MapPoint CreateSpecialPoint(int col, int row, MapPointType pointType)
    {
        return new MapPoint(col, row)
        {
            PointType = pointType,
            CanBeModified = false
        };
    }
}

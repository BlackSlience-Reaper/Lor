using MegaCrit.Sts2.Core.Map;

namespace LibraryOfRuina.acts;

internal sealed class ReverberationEnsembleActMap : ActMap
{
    // 七列网格中的第一、第三、第五列放置三个分支。
    internal const int LeftColumn = 1;
    internal const int MiddleColumn = 3;
    internal const int RightColumn = 5;
    private const int ColumnCount = 7;

    // 先古位于第零行，六个选择层位于第一至第六行，Boss 单独位于第七行。
    internal const int LowerReceptionRow = 1;
    internal const int LowerSupplyRow = 2;
    internal const int MiddleReceptionRow = 3;
    internal const int MiddleSupplyRow = 4;
    internal const int UpperReceptionRow = 5;
    internal const int UpperSupplyRow = 6;
    private const int GridHeight = ReverberationEnsembleAct.InteriorFloorCount + 1;

    public override MapPoint StartingMapPoint { get; }

    public override MapPoint BossMapPoint { get; }

    protected override MapPoint?[,] Grid { get; }

    public ReverberationEnsembleActMap()
    {
        Grid = new MapPoint[ColumnCount, GridHeight];
        StartingMapPoint = CreatePoint(MiddleColumn, 0, MapPointType.Ancient);
        BossMapPoint = CreatePoint(MiddleColumn, GridHeight, MapPointType.Boss);

        AddReceptionRow(LowerReceptionRow);
        AddSupplyRow(LowerSupplyRow);
        AddReceptionRow(MiddleReceptionRow);
        AddSupplyRow(MiddleSupplyRow);
        AddReceptionRow(UpperReceptionRow);
        AddSupplyRow(UpperSupplyRow);

        foreach (MapPoint point in GetPointsInRow(LowerReceptionRow))
        {
            StartingMapPoint.AddChildPoint(point);
            startMapPoints.Add(point);
        }

        for (int row = LowerReceptionRow; row < UpperSupplyRow; row++)
        {
            foreach (MapPoint parent in GetPointsInRow(row))
            {
                foreach (MapPoint child in GetPointsInRow(row + 1))
                {
                    parent.AddChildPoint(child);
                }
            }
        }

        foreach (MapPoint point in GetPointsInRow(UpperSupplyRow))
        {
            point.AddChildPoint(BossMapPoint);
        }
    }

    internal static string? GetReceptionKey(MapCoord coord) =>
        (coord.row, coord.col) switch
        {
            (LowerReceptionRow, LeftColumn) => "HISTORY",
            (LowerReceptionRow, MiddleColumn) => "TECHNOLOGY",
            (LowerReceptionRow, RightColumn) => "LITERATURE",
            (MiddleReceptionRow, LeftColumn) => "ART",
            (MiddleReceptionRow, MiddleColumn) => "LANGUAGE",
            (MiddleReceptionRow, RightColumn) => "NATURAL",
            (UpperReceptionRow, LeftColumn) => "PHILOSOPHY",
            (UpperReceptionRow, MiddleColumn) => "RELIGION",
            (UpperReceptionRow, RightColumn) => "SOCIAL",
            _ => null
        };

    private void AddReceptionRow(int row)
    {
        AddGridPoint(LeftColumn, row, MapPointType.Elite);
        AddGridPoint(MiddleColumn, row, MapPointType.Elite);
        AddGridPoint(RightColumn, row, MapPointType.Elite);
    }

    private void AddSupplyRow(int row)
    {
        AddGridPoint(LeftColumn, row, MapPointType.Shop);
        AddGridPoint(MiddleColumn, row, MapPointType.RestSite);
        AddGridPoint(RightColumn, row, MapPointType.Treasure);
    }

    private void AddGridPoint(int col, int row, MapPointType type)
    {
        Grid[col, row] = CreatePoint(col, row, type);
    }

    private static MapPoint CreatePoint(int col, int row, MapPointType type) =>
        new(col, row)
        {
            PointType = type,
            CanBeModified = false
        };
}

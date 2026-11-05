using SignalSafe.Core.Domain;

namespace SignalSafe.Core.ControlTables;

/// <summary>Where a value came from in the source file, so findings can point back to it.</summary>
public sealed record SourceRef(string Sheet, int Row, string? Column = null)
{
    public SourceRef WithColumn(string column) => this with { Column = column };

    public override string ToString() =>
        Column is null ? $"{Sheet} sheet, row {Row}" : $"{Sheet} sheet, row {Row}, {Column}";
}

/// <summary>A points entry with a required position, such as "P3N" in a flank or overlap column.</summary>
public sealed record PointsCall(string PointsId, PointPosition Position)
{
    public override string ToString() => $"{PointsId}{(Position == PointPosition.Normal ? "N" : "R")}";
}

/// <summary>One route as the designer declared it in the control tables.</summary>
public sealed record ControlTableRow(
    string RouteId,
    string Entrance,
    string Exit,
    IReadOnlyList<string> PointsNormal,
    IReadOnlyList<string> PointsReverse,
    IReadOnlyList<string> SectionsClear,
    IReadOnlyList<string> Overlap,
    IReadOnlyList<PointsCall> OverlapPoints,
    IReadOnlyList<PointsCall> FlankPoints,
    IReadOnlyList<string> ConflictingRoutes,
    SourceRef Source)
{
    /// <summary>Every points position this row calls on the route itself (normal and reverse columns).</summary>
    public IReadOnlyDictionary<string, PointPosition> CalledPoints()
    {
        var result = new Dictionary<string, PointPosition>();
        foreach (var p in PointsNormal)
            result.TryAdd(p, PointPosition.Normal);
        foreach (var p in PointsReverse)
            result.TryAdd(p, PointPosition.Reverse);
        return result;
    }
}

/// <summary>A problem reading the control table file (bad format, missing column, unreadable value).</summary>
public sealed record ImportIssue(SourceRef Source, string Message);

/// <summary>The control tables for one layout, plus any problems found while reading them.</summary>
public sealed record ControlTable(IReadOnlyList<ControlTableRow> Rows, IReadOnlyList<ImportIssue> ImportIssues)
{
    public static ControlTable Empty { get; } = new([], []);
}

/// <summary>The column names of the SignalSafe control table template, in order.</summary>
public static class ControlTableColumns
{
    public const string SheetName = "Routes";
    public const string RouteId = "RouteId";
    public const string Entrance = "Entrance";
    public const string Exit = "Exit";
    public const string PointsNormal = "PointsNormal";
    public const string PointsReverse = "PointsReverse";
    public const string SectionsClear = "SectionsClear";
    public const string Overlap = "Overlap";
    public const string OverlapPoints = "OverlapPoints";
    public const string FlankPoints = "FlankPoints";
    public const string ConflictingRoutes = "ConflictingRoutes";

    public static IReadOnlyList<string> All { get; } =
    [
        RouteId, Entrance, Exit, PointsNormal, PointsReverse, SectionsClear,
        Overlap, OverlapPoints, FlankPoints, ConflictingRoutes,
    ];

    public static IReadOnlyList<string> Required { get; } = [RouteId, Entrance, Exit];

    /// <summary>One-line help shown in the template under each column heading.</summary>
    public static IReadOnlyDictionary<string, string> Help { get; } = new Dictionary<string, string>
    {
        [RouteId] = "Route name, e.g. S1(M)-S3",
        [Entrance] = "Entrance signal, e.g. S1",
        [Exit] = "Exit signal, e.g. S3",
        [PointsNormal] = "Points called and locked Normal, comma separated",
        [PointsReverse] = "Points called and locked Reverse, comma separated",
        [SectionsClear] = "Track sections that must be clear",
        [Overlap] = "Overlap sections beyond the exit signal",
        [OverlapPoints] = "Points locked in the overlap, e.g. P5N",
        [FlankPoints] = "Points held for flank protection, e.g. P3N",
        [ConflictingRoutes] = "Routes that cannot be set at the same time",
    };
}

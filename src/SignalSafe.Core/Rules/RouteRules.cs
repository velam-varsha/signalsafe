using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Domain;

namespace SignalSafe.Core.Rules;

/// <summary>R01: every route the layout allows should appear in the control tables.</summary>
public sealed class MissingRouteRule : IRule
{
    public string Id => "R01";
    public string Name => "Missing route";

    public IEnumerable<Finding> Check(RuleContext ctx)
    {
        foreach (var route in ctx.Routes.Where(r => !ctx.RowFor.ContainsKey(r.Id)))
        {
            var points = route.Points.Count == 0 ? "" : $" with {Describe.Points(route.Points)}";
            yield return new Finding(Id, Name, Severity.Warning, route.Id,
                [route.Entrance.Id, route.Exit.Id, .. route.Sections, .. route.Points.Keys],
                $"The layout allows {route.Id} over {string.Join(", ", route.Sections)}{points}, but it is not in the control tables.",
                null);
        }
    }
}

/// <summary>R02: every route in the control tables must exist in the layout.</summary>
public sealed class ImpossibleRouteRule : IRule
{
    public string Id => "R02";
    public string Name => "Impossible route";

    public IEnumerable<Finding> Check(RuleContext ctx)
    {
        var signals = ctx.Layout.Signals.Select(s => s.Id).ToHashSet();
        var matched = ctx.Matches.Select(m => m.Row).ToHashSet();
        foreach (var row in ctx.Table.Rows.Where(r => !matched.Contains(r)))
        {
            if (!signals.Contains(row.Entrance) || !signals.Contains(row.Exit))
                continue; // unknown signal IDs are reported by R10

            yield return new Finding(Id, Name, Severity.Error, row.RouteId, [row.Entrance, row.Exit],
                $"{row.RouteId}: there is no route from {row.Entrance} to {row.Exit} in the layout.",
                row.Source.WithColumn(ControlTableColumns.Exit));
        }
    }
}

/// <summary>R03: each route must call every points on its path, in the position the path needs.</summary>
public sealed class PointPositionRule : IRule
{
    public string Id => "R03";
    public string Name => "Point position";

    public IEnumerable<Finding> Check(RuleContext ctx)
    {
        foreach (var (row, route) in ctx.Matches)
        {
            var called = row.CalledPoints();
            foreach (var (pointsId, needed) in route.Points)
            {
                if (route.LinkedPoints.Contains(pointsId))
                    continue; // the other end of a crossover is checked by R09

                if (!called.TryGetValue(pointsId, out var declared))
                {
                    yield return new Finding(Id, Name, Severity.Error, row.RouteId, [pointsId],
                        $"{row.RouteId} passes over {pointsId}, which must be {needed}, but the control table does not call {pointsId}.",
                        row.Source.WithColumn(Describe.Column(needed)));
                }
                else if (declared != needed)
                {
                    yield return new Finding(Id, Name, Severity.Error, row.RouteId, [pointsId, .. route.Sections],
                        $"{pointsId} must be {needed} for {row.RouteId}, but the control table calls it {declared}.",
                        row.Source.WithColumn(Describe.Column(declared)));
                }
            }

            foreach (var (pointsId, declared) in called)
            {
                if (!route.Points.ContainsKey(pointsId) && ctx.PointsById.ContainsKey(pointsId))
                {
                    yield return new Finding(Id, Name, Severity.Warning, row.RouteId, [pointsId],
                        $"{row.RouteId} calls {pointsId} {declared}, but {pointsId} is not on the route. If it is for flank protection, move it to FlankPoints.",
                        row.Source.WithColumn(Describe.Column(declared)));
                }
            }
        }
    }
}

/// <summary>R04: each route must require every track section on its path to be clear.</summary>
public sealed class TrackClearanceRule : IRule
{
    public string Id => "R04";
    public string Name => "Track clearance";

    public IEnumerable<Finding> Check(RuleContext ctx)
    {
        var sections = ctx.Layout.Sections.Select(s => s.Id).ToHashSet();
        foreach (var (row, route) in ctx.Matches)
        {
            foreach (var section in route.Sections.Where(s => !row.SectionsClear.Contains(s)))
            {
                yield return new Finding(Id, Name, Severity.Error, row.RouteId, [section],
                    $"{row.RouteId} runs over {section}, but {section} is not listed as a section that must be clear.",
                    row.Source.WithColumn(ControlTableColumns.SectionsClear));
            }

            foreach (var listed in row.SectionsClear)
            {
                if (sections.Contains(listed) && !route.Sections.Contains(listed) && !route.Overlap.Contains(listed))
                {
                    yield return new Finding(Id, Name, Severity.Warning, row.RouteId, [listed],
                        $"{row.RouteId} lists {listed} as must be clear, but {listed} is not on the route or its overlap.",
                        row.Source.WithColumn(ControlTableColumns.SectionsClear));
                }
            }
        }
    }
}

/// <summary>Shared wording helpers for findings.</summary>
internal static class Describe
{
    public static string Points(IReadOnlyDictionary<string, PointPosition> points) =>
        string.Join(", ", points.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}"));

    public static string Column(PointPosition position) =>
        position == PointPosition.Normal ? ControlTableColumns.PointsNormal : ControlTableColumns.PointsReverse;
}

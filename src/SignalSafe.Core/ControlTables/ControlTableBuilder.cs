using SignalSafe.Core.Domain;
using SignalSafe.Core.Routing;
using SignalSafe.Core.Rules;

namespace SignalSafe.Core.ControlTables;

/// <summary>
/// Drafts control table rows from the routes derived from a layout.
/// Useful as a starting point for a designer, and as a known-good table for tests.
/// </summary>
public static class ControlTableBuilder
{
    public static IReadOnlyList<ControlTableRow> Draft(Layout layout)
    {
        var routes = RouteFinder.Derive(layout);
        var conflicts = ConflictFinder.Find(routes);
        var flank = FlankAnalysis.For(layout, routes);

        return routes.Select((r, i) => new ControlTableRow(
            r.Id,
            r.Entrance.Id,
            r.Exit.Id,
            Sorted(r.Points.Where(p => p.Value == PointPosition.Normal).Select(p => p.Key)),
            Sorted(r.Points.Where(p => p.Value == PointPosition.Reverse).Select(p => p.Key)),
            r.Sections.ToList(),
            r.Overlap.ToList(),
            [],
            flank[r.Id].Where(f => f.Required is not null).Select(f => f.Required!).ToList(),
            Sorted(conflicts
                .Where(c => c.RouteA == r.Id || c.RouteB == r.Id)
                .Select(c => c.RouteA == r.Id ? c.RouteB : c.RouteA)),
            new SourceRef(ControlTableColumns.SheetName, i + 2))).ToList();
    }

    private static List<string> Sorted(IEnumerable<string> items) =>
        items.OrderBy(x => x, StringComparer.Ordinal).ToList();
}

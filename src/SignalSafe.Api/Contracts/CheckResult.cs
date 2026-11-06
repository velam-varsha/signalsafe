using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Domain;
using SignalSafe.Core.Layouts;
using SignalSafe.Core.Routing;
using SignalSafe.Core.Rules;

namespace SignalSafe.Api.Contracts;

/// <summary>Everything the web app needs to show one check: the layout to draw, routes, conflicts and findings.</summary>
public sealed record CheckResult(
    LayoutView Layout,
    IReadOnlyList<LayoutIssueView> LayoutIssues,
    IReadOnlyList<RouteView> Routes,
    IReadOnlyList<ConflictView> Conflicts,
    TablesView? Tables,
    IReadOnlyList<FindingView> Findings,
    IReadOnlyList<RuleView> Rules,
    double DurationMs);

public sealed record LayoutView(
    string Name,
    IReadOnlyList<SectionView> Sections,
    IReadOnlyList<PointsView> Points,
    IReadOnlyList<SignalView> Signals,
    bool HasDiagram);

public sealed record SectionView(string Id, string EndA, string EndB, double LengthM, IReadOnlyList<double[]>? Line);

public sealed record PointsView(string Id, string Toe, string Normal, string Reverse, string? LinkedWith);

public sealed record SignalView(string Id, SignalType Type, string AtNode, string Protects);

public sealed record LayoutIssueView(string Code, string ElementId, string Message);

public sealed record RouteView(
    string Id,
    string Entrance,
    string Exit,
    bool IsShunt,
    IReadOnlyList<string> Sections,
    IReadOnlyDictionary<string, PointPosition> Points,
    IReadOnlyList<string> LinkedPoints,
    IReadOnlyList<string> Overlap,
    bool InTables);

public sealed record ConflictView(string RouteA, string RouteB, IReadOnlyList<string> SharedSections, IReadOnlyList<string> OpposingPoints, bool Opposing);

public sealed record TablesView(string FileName, int RowCount);

public sealed record SourceView(string Sheet, int Row, string? Column);

public sealed record FindingView(
    string RuleId,
    string RuleName,
    Severity Severity,
    string? RouteId,
    IReadOnlyList<string> Elements,
    string Message,
    SourceView? Source);

/// <summary>A rule that ran, and how many findings it produced (0 means passed).</summary>
public sealed record RuleView(string Id, string Name, int FindingCount);

/// <summary>Builds a <see cref="CheckResult"/> from a layout and optional control tables.</summary>
public static class CheckRunner
{
    public static CheckResult Run(Layout layout, ControlTable? table, string? tablesFileName)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var issues = LayoutValidator.Validate(layout);

        IReadOnlyList<DerivedRoute> routes = [];
        IReadOnlyList<RouteConflict> conflicts = [];
        IReadOnlyList<Finding> findings = [];
        var rowFor = new HashSet<string>();

        if (issues.Count == 0)
        {
            routes = RouteFinder.Derive(layout);
            conflicts = ConflictFinder.Find(routes);
            if (table is not null)
            {
                findings = RuleEngine.Run(layout, table);
                rowFor = new RuleContext(layout, table).RowFor.Keys.ToHashSet();
            }
        }

        var rules = RuleEngine.DefaultRules
            .Select(r => new RuleView(r.Id, r.Name, findings.Count(f => f.RuleId == r.Id)))
            .ToList();

        timer.Stop();
        return new CheckResult(
            ToView(layout),
            issues.Select(i => new LayoutIssueView(i.Code, i.ElementId, i.Message)).ToList(),
            routes.Select(r => new RouteView(
                r.Id, r.Entrance.Id, r.Exit.Id, r.IsShunt, r.Sections,
                new SortedDictionary<string, PointPosition>(r.Points.ToDictionary(), StringComparer.Ordinal),
                r.LinkedPoints, r.Overlap, rowFor.Contains(r.Id))).ToList(),
            conflicts.Select(c => new ConflictView(c.RouteA, c.RouteB, c.SharedSections, c.OpposingPoints, c.Opposing)).ToList(),
            table is null ? null : new TablesView(tablesFileName ?? "control tables", table.Rows.Count),
            findings.Select(f => new FindingView(
                f.RuleId, f.RuleName, f.Severity, f.RouteId, f.Elements.Distinct().ToList(), f.Message,
                f.Source is null ? null : new SourceView(f.Source.Sheet, f.Source.Row, f.Source.Column))).ToList(),
            table is null ? [] : rules,
            Math.Round(timer.Elapsed.TotalMilliseconds, 1));
    }

    private static LayoutView ToView(Layout layout) => new(
        layout.Name,
        layout.Sections.Select(s => new SectionView(
            s.Id, s.EndA, s.EndB, s.LengthM,
            layout.Diagram is not null && layout.Diagram.TryGetValue(s.Id, out var line)
                ? line.Select(p => new[] { p.X, p.Y }).ToList()
                : null)).ToList(),
        layout.Points.Select(p => new PointsView(p.Id, p.ToeSection, p.NormalSection, p.ReverseSection, p.LinkedWith)).ToList(),
        layout.Signals.Select(s => new SignalView(s.Id, s.Type, s.AtNode, s.Protects)).ToList(),
        layout.Diagram is { Count: > 0 });
}

using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Domain;
using SignalSafe.Core.Routing;

namespace SignalSafe.Core.Rules;

/// <summary>How serious a finding is.</summary>
public enum Severity
{
    /// <summary>A safety-relevant error: the control tables do not protect a route as the layout requires.</summary>
    Error,

    /// <summary>A consistency problem that needs a designer's attention.</summary>
    Warning,
}

/// <summary>One problem found by a rule.</summary>
/// <param name="RouteId">The route the finding is about, if any.</param>
/// <param name="Elements">Layout elements involved (sections, points, signals), for highlighting on a diagram.</param>
/// <param name="Source">Where in the control tables the problem is, if it comes from a row.</param>
public sealed record Finding(
    string RuleId,
    string RuleName,
    Severity Severity,
    string? RouteId,
    IReadOnlyList<string> Elements,
    string Message,
    SourceRef? Source);

/// <summary>A verification rule. Each rule is one class with one job, so it can be tested on its own.</summary>
public interface IRule
{
    string Id { get; }
    string Name { get; }
    IEnumerable<Finding> Check(RuleContext ctx);
}

/// <summary>Everything a rule may need, worked out once per check.</summary>
public sealed class RuleContext
{
    public RuleContext(Layout layout, ControlTable table)
    {
        Layout = layout;
        Table = table;
        Routes = RouteFinder.Derive(layout);
        Conflicts = ConflictFinder.Find(Routes);
        Flank = FlankAnalysis.For(layout, Routes);
        RoutesById = Routes.ToDictionary(r => r.Id);
        PointsById = layout.Points.ToDictionary(p => p.Id);

        // Match declared rows to derived routes by entrance and exit signal: a designer's route name may differ.
        var routeByEnds = Routes.GroupBy(r => (r.Entrance.Id, r.Exit.Id)).ToDictionary(g => g.Key, g => g.First());
        var matches = new List<(ControlTableRow, DerivedRoute)>();
        foreach (var row in table.Rows)
        {
            if (routeByEnds.TryGetValue((row.Entrance, row.Exit), out var route))
                matches.Add((row, route));
        }
        Matches = matches;
        RowFor = matches.GroupBy(m => m.Item2.Id).ToDictionary(g => g.Key, g => g.First().Item1);
        DeclaredIdFor = RowFor.ToDictionary(kv => kv.Key, kv => kv.Value.RouteId);
    }

    public Layout Layout { get; }
    public ControlTable Table { get; }
    public IReadOnlyList<DerivedRoute> Routes { get; }
    public IReadOnlyList<RouteConflict> Conflicts { get; }
    public ILookup<string, FlankProtection> Flank { get; }
    public IReadOnlyDictionary<string, DerivedRoute> RoutesById { get; }
    public IReadOnlyDictionary<string, Points> PointsById { get; }

    /// <summary>Each control table row paired with the derived route it describes.</summary>
    public IReadOnlyList<(ControlTableRow Row, DerivedRoute Route)> Matches { get; }

    /// <summary>The control table row for a derived route ID, if the tables have one.</summary>
    public IReadOnlyDictionary<string, ControlTableRow> RowFor { get; }

    /// <summary>The name the designer used in the tables for a derived route ID.</summary>
    public IReadOnlyDictionary<string, string> DeclaredIdFor { get; }

    /// <summary>Whether a row's ConflictingRoutes column names the given derived route (by derived or declared name).</summary>
    public bool ListsConflict(ControlTableRow row, string derivedRouteId) =>
        row.ConflictingRoutes.Contains(derivedRouteId)
        || (DeclaredIdFor.TryGetValue(derivedRouteId, out var declared) && row.ConflictingRoutes.Contains(declared));
}

/// <summary>Runs a set of rules and returns their findings in a stable order: errors first, then by rule and route.</summary>
public static class RuleEngine
{
    public static IReadOnlyList<IRule> DefaultRules { get; } =
    [
        new MissingRouteRule(),
        new ImpossibleRouteRule(),
        new PointPositionRule(),
        new TrackClearanceRule(),
        new ConflictingRoutesRule(),
        new OpposingRoutesRule(),
        new OverlapRule(),
        new FlankProtectionRule(),
        new CrossoverPairingRule(),
        new DataConsistencyRule(),
    ];

    public static IReadOnlyList<Finding> Run(Layout layout, ControlTable table, IEnumerable<IRule>? rules = null)
    {
        var context = new RuleContext(layout, table);
        return (rules ?? DefaultRules)
            .SelectMany(rule => rule.Check(context))
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.RouteId ?? "", StringComparer.Ordinal)
            .ThenBy(f => f.Source?.Row ?? 0)
            .ThenBy(f => f.Message, StringComparer.Ordinal)
            .ToList();
    }
}

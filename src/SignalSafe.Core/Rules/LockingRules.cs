using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Domain;
using SignalSafe.Core.Routing;

namespace SignalSafe.Core.Rules;

/// <summary>Checks that both rows of a conflicting pair list each other. Shared by R05 and R06.</summary>
public abstract class ConflictListingRule : IRule
{
    public abstract string Id { get; }
    public abstract string Name { get; }

    /// <summary>Which conflicts this rule is responsible for.</summary>
    protected abstract bool Covers(RouteConflict conflict);

    protected abstract string Reason(RouteConflict c);

    public IEnumerable<Finding> Check(RuleContext ctx)
    {
        foreach (var conflict in ctx.Conflicts.Where(Covers))
        {
            foreach (var (self, other) in new[] { (conflict.RouteA, conflict.RouteB), (conflict.RouteB, conflict.RouteA) })
            {
                if (!ctx.RowFor.TryGetValue(self, out var row) || ctx.ListsConflict(row, other))
                    continue;

                var otherName = ctx.DeclaredIdFor.GetValueOrDefault(other, other);
                yield return new Finding(Id, Name, Severity.Error, row.RouteId,
                    [.. conflict.SharedSections, .. conflict.OpposingPoints],
                    $"{row.RouteId} and {otherName} {Reason(conflict)}, but {row.RouteId} does not list {otherName} as a conflicting route.",
                    row.Source.WithColumn(ControlTableColumns.ConflictingRoutes));
            }
        }
    }
}

/// <summary>R05: routes that share track or need points in opposite positions must lock each other out.</summary>
public sealed class ConflictingRoutesRule : ConflictListingRule
{
    public override string Id => "R05";
    public override string Name => "Conflicting routes";

    protected override bool Covers(RouteConflict conflict) => !conflict.Opposing;

    protected override string Reason(RouteConflict c)
    {
        var parts = new List<string>();
        if (c.SharedSections.Count > 0)
            parts.Add($"both use {string.Join(", ", c.SharedSections)}");
        if (c.OpposingPoints.Count > 0)
            parts.Add($"need {string.Join(", ", c.OpposingPoints)} in opposite positions");
        return string.Join(" and ", parts);
    }
}

/// <summary>R06: routes in opposite directions over the same track (head-on) must lock each other out.</summary>
public sealed class OpposingRoutesRule : ConflictListingRule
{
    public override string Id => "R06";
    public override string Name => "Opposing routes";

    protected override bool Covers(RouteConflict conflict) => conflict.Opposing;

    protected override string Reason(RouteConflict c) =>
        $"run head-on over {string.Join(", ", c.SharedSections)}";
}

/// <summary>R07: each route must have the overlap the layout gives it beyond the exit signal.</summary>
public sealed class OverlapRule : IRule
{
    public string Id => "R07";
    public string Name => "Overlap";

    public IEnumerable<Finding> Check(RuleContext ctx)
    {
        foreach (var (row, route) in ctx.Matches)
        {
            var expected = string.Join(", ", route.Overlap);
            if (row.Overlap.Count == 0)
            {
                yield return new Finding(Id, Name, Severity.Error, row.RouteId, [route.Exit.Id, .. route.Overlap],
                    $"No overlap is listed beyond exit signal {route.Exit.Id}. Expected overlap: {expected}.",
                    row.Source.WithColumn(ControlTableColumns.Overlap));
            }
            else if (!row.Overlap.SequenceEqual(route.Overlap))
            {
                yield return new Finding(Id, Name, Severity.Error, row.RouteId, [route.Exit.Id, .. route.Overlap, .. row.Overlap],
                    $"The overlap beyond {route.Exit.Id} is listed as {string.Join(", ", row.Overlap)}. Expected: {expected}.",
                    row.Source.WithColumn(ControlTableColumns.Overlap));
            }
        }
    }
}

/// <summary>R08: points that can protect the side of a route must be held in the protecting position.</summary>
public sealed class FlankProtectionRule : IRule
{
    public string Id => "R08";
    public string Name => "Flank protection";

    public IEnumerable<Finding> Check(RuleContext ctx)
    {
        foreach (var (row, route) in ctx.Matches)
        {
            foreach (var flank in ctx.Flank[route.Id])
            {
                if (flank.Unprotected)
                {
                    yield return new Finding(Id, Name, Severity.Warning, row.RouteId, [flank.PointsId, flank.FlankSection],
                        $"Nothing on {flank.FlankSection} can protect {row.RouteId} at {flank.PointsId}: no signal reads towards the points and no points can be set away. Consider trap points.",
                        null);
                    continue;
                }
                if (flank.Required is not { } required)
                    continue; // protected by a signal held at danger

                var listed = row.FlankPoints.FirstOrDefault(f => f.PointsId == required.PointsId);
                if (listed is null)
                {
                    yield return new Finding(Id, Name, Severity.Error, row.RouteId, [required.PointsId, flank.PointsId, flank.FlankSection],
                        $"{required.PointsId} must be held {required.Position} to stop movements on {flank.FlankSection} running into the side of {row.RouteId} at {flank.PointsId}, but it is not listed as a flank point.",
                        row.Source.WithColumn(ControlTableColumns.FlankPoints));
                }
                else if (listed.Position != required.Position)
                {
                    yield return new Finding(Id, Name, Severity.Error, row.RouteId, [required.PointsId, flank.PointsId, flank.FlankSection],
                        $"Flank point {required.PointsId} is listed {listed.Position}, but it must be {required.Position} to lead away from {row.RouteId}.",
                        row.Source.WithColumn(ControlTableColumns.FlankPoints));
                }
            }
        }
    }
}

/// <summary>R09: the two ends of a crossover are always called together, in the same position.</summary>
public sealed class CrossoverPairingRule : IRule
{
    public string Id => "R09";
    public string Name => "Crossover pairing";

    public IEnumerable<Finding> Check(RuleContext ctx)
    {
        foreach (var row in ctx.Table.Rows)
        {
            var called = row.CalledPoints();
            var reported = new HashSet<string>();
            foreach (var (pointsId, position) in called)
            {
                if (!ctx.PointsById.TryGetValue(pointsId, out var p) || p.LinkedWith is not { } partner || reported.Contains(partner))
                    continue;

                if (!called.TryGetValue(partner, out var partnerPosition))
                {
                    yield return new Finding(Id, Name, Severity.Error, row.RouteId, [pointsId, partner],
                        $"{row.RouteId} calls {pointsId} {position} but not {partner}, the other end of the same crossover. Call {partner} {position} too.",
                        row.Source.WithColumn(Describe.Column(position)));
                }
                else if (partnerPosition != position)
                {
                    yield return new Finding(Id, Name, Severity.Error, row.RouteId, [pointsId, partner],
                        $"{row.RouteId} calls {pointsId} {position} and {partner} {partnerPosition}, but the two ends of a crossover move together.",
                        row.Source.WithColumn(Describe.Column(partnerPosition)));
                }
                reported.Add(pointsId);
            }
        }
    }
}

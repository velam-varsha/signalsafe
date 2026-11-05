using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Domain;
using SignalSafe.Core.Routing;

namespace SignalSafe.Core.Rules;

/// <summary>How the flank of a route is protected where it passes over a set of trailing points.</summary>
/// <param name="PointsId">The trailing points on the route.</param>
/// <param name="FlankSection">The other heel of those points: a movement coming along it would run into the side of the route.</param>
/// <param name="Required">Points that must be held in this position to protect the flank, if that is how it is protected.</param>
/// <param name="ProtectingSignal">A signal reading towards the points that protects the flank by being held at danger.</param>
/// <param name="Unprotected">True when nothing on the flank line can protect the route (for example, points facing the wrong way).</param>
public sealed record FlankProtection(
    string RouteId,
    string PointsId,
    string FlankSection,
    PointsCall? Required,
    string? ProtectingSignal,
    bool Unprotected);

/// <summary>
/// Works out flank protection for each route. For every set of trailing points on a route, it walks
/// back along the other heel (the flank line) to the first thing that can stop a movement reaching the
/// points: a signal reading towards them, or another set of points that can be set away.
/// </summary>
/// <remarks>
/// MVP simplification: only trailing points are analysed; a flank line that runs to the edge of the
/// layout without a signal or points is treated as outside the area being checked.
/// </remarks>
public static class FlankAnalysis
{
    private const int MaxSectionsToWalk = 6;

    public static ILookup<string, FlankProtection> For(Layout layout, IReadOnlyList<DerivedRoute> routes)
    {
        var sections = layout.Sections.ToDictionary(s => s.Id);
        var points = layout.Points.ToDictionary(p => p.Id);
        var signalsAtNode = layout.Signals.ToLookup(s => s.AtNode);
        var sectionsAtNode = layout.Sections
            .SelectMany(s => new[] { (Node: s.EndA, Section: s), (Node: s.EndB, Section: s) })
            .ToLookup(x => x.Node, x => x.Section);

        var results = new List<FlankProtection>();
        foreach (var route in routes)
        {
            foreach (var passage in route.PathPoints.Where(p => p.Trailing))
            {
                if (!points.TryGetValue(passage.PointsId, out var p))
                    continue;
                var flankId = passage.Position == PointPosition.Normal ? p.ReverseSection : p.NormalSection;
                if (!sections.TryGetValue(flankId, out var flank))
                    continue;

                var found = Walk(route, p.Id, flank, points, signalsAtNode, sectionsAtNode);
                if (found is not null)
                    results.Add(found);
            }
        }
        return results.ToLookup(r => r.RouteId);
    }

    private static FlankProtection? Walk(
        DerivedRoute route,
        string pointsId,
        TrackSection flank,
        Dictionary<string, Points> points,
        ILookup<string, Signal> signalsAtNode,
        ILookup<string, TrackSection> sectionsAtNode)
    {
        var current = flank;
        var node = flank.OtherEnd(pointsId);

        for (var step = 0; step < MaxSectionsToWalk; step++)
        {
            // A signal here reading into the flank line (towards the route) protects it when held at danger.
            var signal = signalsAtNode[node].FirstOrDefault(s => s.Protects == current.Id);
            if (signal is not null)
                return new(route.Id, pointsId, flank.Id, null, signal.Id, false);

            if (points.TryGetValue(node, out var q))
            {
                var position = q.PositionFor(current.Id);
                if (position is null)
                {
                    // We reached q on its toe: both heels lead towards the route, so q cannot protect it.
                    return new(route.Id, pointsId, flank.Id, null, null, true);
                }

                var away = position == PointPosition.Normal ? PointPosition.Reverse : PointPosition.Normal;
                if (route.Points.TryGetValue(q.Id, out var onRoute))
                {
                    // q is already called by the route itself.
                    return onRoute == away ? null : new(route.Id, pointsId, flank.Id, null, null, true);
                }
                return new(route.Id, pointsId, flank.Id, new PointsCall(q.Id, away), null, false);
            }

            var next = sectionsAtNode[node].FirstOrDefault(s => s.Id != current.Id);
            if (next is null)
                return null; // edge of the layout
            current = next;
            node = next.OtherEnd(node);
        }
        return null;
    }
}

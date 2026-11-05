using SignalSafe.Core.Domain;

namespace SignalSafe.Core.Routing;

/// <summary>A route the layout allows, worked out from the track topology.</summary>
public sealed record DerivedRoute(
    string Id,
    Signal Entrance,
    Signal Exit,
    IReadOnlyList<string> Sections,
    IReadOnlyDictionary<string, PointPosition> Points,
    IReadOnlyList<string> Overlap)
{
    public bool IsShunt => Entrance.Type == SignalType.Shunt;

    /// <summary>Builds a route name such as "S1(M)-S3" or "S7(S)-S6".</summary>
    public static string MakeId(Signal entrance, Signal exit) =>
        $"{entrance.Id}({(entrance.Type == SignalType.Main ? "M" : "S")})-{exit.Id}";
}

/// <summary>
/// Derives every route in a layout with a depth-first search from each signal.
/// The search follows the track in the signal's direction, takes both legs at facing points,
/// passes through trailing points (recording the position they must be in), and stops at the
/// next signal reading the same way.
/// </summary>
/// <remarks>
/// MVP simplifications: the overlap is the section beyond the exit signal; routes ending at a
/// line end or buffer stop without a signal are not routes.
/// </remarks>
public static class RouteFinder
{
    public static IReadOnlyList<DerivedRoute> Derive(Layout layout)
    {
        var sections = layout.Sections.ToDictionary(s => s.Id);
        var pointsById = layout.Points.ToDictionary(p => p.Id);
        var signalsAtNode = layout.Signals.ToLookup(s => s.AtNode);
        var sectionsAtNode = layout.Sections
            .SelectMany(s => new[] { (Node: s.EndA, Section: s), (Node: s.EndB, Section: s) })
            .ToLookup(x => x.Node, x => x.Section);
        var routes = new List<DerivedRoute>();

        foreach (var entrance in layout.Signals)
        {
            if (!sections.TryGetValue(entrance.Protects, out var first) || !first.Touches(entrance.AtNode))
                continue; // invalid signal; the layout validator reports it

            Search(new SearchContext(entrance, sections, pointsById, signalsAtNode, sectionsAtNode, routes),
                section: first,
                enteredFrom: entrance.AtNode,
                path: [first.Id],
                points: new Dictionary<string, PointPosition>());
        }

        return routes.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
    }

    private sealed record SearchContext(
        Signal Entrance,
        Dictionary<string, TrackSection> Sections,
        Dictionary<string, Points> Points,
        ILookup<string, Signal> SignalsAtNode,
        ILookup<string, TrackSection> SectionsAtNode,
        List<DerivedRoute> Results);

    private static void Search(
        SearchContext ctx,
        TrackSection section,
        string enteredFrom,
        List<string> path,
        Dictionary<string, PointPosition> points)
    {
        var node = section.OtherEnd(enteredFrom);

        // A signal at this node reading onwards (away from the section we came along) ends the route.
        var exit = ctx.SignalsAtNode[node].FirstOrDefault(s => s.Protects != section.Id);
        if (exit is not null)
        {
            ctx.Results.Add(new DerivedRoute(
                DerivedRoute.MakeId(ctx.Entrance, exit),
                ctx.Entrance,
                exit,
                path.ToList(),
                new Dictionary<string, PointPosition>(points),
                [exit.Protects]));
            return;
        }

        foreach (var (next, requiredPosition, pointsId) in NextSections(ctx, node, section))
        {
            if (path.Contains(next.Id))
                continue; // never use a section twice; this also stops loops

            path.Add(next.Id);
            if (pointsId is not null)
                points[pointsId] = requiredPosition!.Value;

            Search(ctx, next, node, path, points);

            path.RemoveAt(path.Count - 1);
            if (pointsId is not null)
                points.Remove(pointsId);
        }
    }

    /// <summary>The sections a train can move on to from <paramref name="node"/>, having arrived on <paramref name="arrivedOn"/>.</summary>
    private static IEnumerable<(TrackSection Next, PointPosition? Position, string? PointsId)> NextSections(
        SearchContext ctx, string node, TrackSection arrivedOn)
    {
        if (ctx.Points.TryGetValue(node, out var p))
        {
            if (arrivedOn.Id == p.ToeSection)
            {
                // Facing points: the train can be sent either way.
                if (ctx.Sections.TryGetValue(p.NormalSection, out var normal))
                    yield return (normal, PointPosition.Normal, p.Id);
                if (ctx.Sections.TryGetValue(p.ReverseSection, out var reverse))
                    yield return (reverse, PointPosition.Reverse, p.Id);
            }
            else if (p.PositionFor(arrivedOn.Id) is { } trailing && ctx.Sections.TryGetValue(p.ToeSection, out var toe))
            {
                // Trailing points: the train must leave by the toe, with the points set for the leg it came from.
                yield return (toe, trailing, p.Id);
            }
            yield break;
        }

        // Plain joint: continue onto the other section, if there is one (otherwise this is a line end).
        foreach (var s in ctx.SectionsAtNode[node])
        {
            if (s.Id != arrivedOn.Id)
                yield return (s, null, null);
        }
    }
}

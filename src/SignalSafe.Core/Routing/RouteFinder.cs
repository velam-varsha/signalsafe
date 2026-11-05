using SignalSafe.Core.Domain;

namespace SignalSafe.Core.Routing;

/// <summary>How a route passes over one set of points.</summary>
/// <param name="Trailing">True when the train arrives on a heel and leaves by the toe.</param>
public sealed record PointsPassage(string PointsId, PointPosition Position, bool Trailing);

/// <summary>A route the layout allows, worked out from the track topology.</summary>
/// <param name="Sections">Track sections on the route path, in the order the train uses them.</param>
/// <param name="EntryNodes">For each section in <paramref name="Sections"/>, the node the train enters it from (gives direction).</param>
/// <param name="PathPoints">Points the train passes over, in order.</param>
/// <param name="Points">Every points position the route needs: those on the path plus the other end of any crossover.</param>
/// <param name="LinkedPoints">Points added because they are the other end of a crossover on the path.</param>
/// <param name="Overlap">Sections beyond the exit signal that are kept clear.</param>
public sealed record DerivedRoute(
    string Id,
    Signal Entrance,
    Signal Exit,
    IReadOnlyList<string> Sections,
    IReadOnlyList<string> EntryNodes,
    IReadOnlyList<PointsPassage> PathPoints,
    IReadOnlyDictionary<string, PointPosition> Points,
    IReadOnlyList<string> LinkedPoints,
    IReadOnlyList<string> Overlap)
{
    public bool IsShunt => Entrance.Type == SignalType.Shunt;

    /// <summary>The node the train enters <paramref name="sectionId"/> from, or null if it is not on the path.</summary>
    public string? EntryNodeOf(string sectionId)
    {
        for (var i = 0; i < Sections.Count; i++)
        {
            if (Sections[i] == sectionId)
                return EntryNodes[i];
        }
        return null;
    }

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
/// The two ends of a crossover are worked together, so a route over one end also calls the other
/// end in the same position. MVP simplifications: the overlap is the section beyond the exit signal;
/// routes ending at a line end or buffer stop without a signal are not routes.
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
                entries: [entrance.AtNode],
                passages: []);
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
        List<string> entries,
        List<PointsPassage> passages)
    {
        var node = section.OtherEnd(enteredFrom);

        // A signal at this node reading onwards (away from the section we came along) ends the route.
        var exit = ctx.SignalsAtNode[node].FirstOrDefault(s => s.Protects != section.Id);
        if (exit is not null)
        {
            ctx.Results.Add(BuildRoute(ctx, exit, path, entries, passages));
            return;
        }

        foreach (var (next, passage) in NextSections(ctx, node, section))
        {
            if (path.Contains(next.Id))
                continue; // never use a section twice; this also stops loops

            path.Add(next.Id);
            entries.Add(node);
            if (passage is not null)
                passages.Add(passage);

            Search(ctx, next, node, path, entries, passages);

            path.RemoveAt(path.Count - 1);
            entries.RemoveAt(entries.Count - 1);
            if (passage is not null)
                passages.RemoveAt(passages.Count - 1);
        }
    }

    private static DerivedRoute BuildRoute(
        SearchContext ctx, Signal exit, List<string> path, List<string> entries, List<PointsPassage> passages)
    {
        var points = new Dictionary<string, PointPosition>();
        foreach (var p in passages)
            points[p.PointsId] = p.Position;

        // The other end of a crossover moves with this end, so the route calls it in the same position.
        var linked = new List<string>();
        foreach (var p in passages)
        {
            if (ctx.Points.TryGetValue(p.PointsId, out var pts) && pts.LinkedWith is { } partner
                && ctx.Points.ContainsKey(partner) && !points.ContainsKey(partner))
            {
                points[partner] = p.Position;
                linked.Add(partner);
            }
        }

        return new DerivedRoute(
            DerivedRoute.MakeId(ctx.Entrance, exit),
            ctx.Entrance,
            exit,
            path.ToList(),
            entries.ToList(),
            passages.ToList(),
            points,
            linked,
            [exit.Protects]);
    }

    /// <summary>The sections a train can move on to from <paramref name="node"/>, having arrived on <paramref name="arrivedOn"/>.</summary>
    private static IEnumerable<(TrackSection Next, PointsPassage? Passage)> NextSections(
        SearchContext ctx, string node, TrackSection arrivedOn)
    {
        if (ctx.Points.TryGetValue(node, out var p))
        {
            if (arrivedOn.Id == p.ToeSection)
            {
                // Facing points: the train can be sent either way.
                if (ctx.Sections.TryGetValue(p.NormalSection, out var normal))
                    yield return (normal, new PointsPassage(p.Id, PointPosition.Normal, Trailing: false));
                if (ctx.Sections.TryGetValue(p.ReverseSection, out var reverse))
                    yield return (reverse, new PointsPassage(p.Id, PointPosition.Reverse, Trailing: false));
            }
            else if (p.PositionFor(arrivedOn.Id) is { } trailing && ctx.Sections.TryGetValue(p.ToeSection, out var toe))
            {
                // Trailing points: the train must leave by the toe, with the points set for the leg it came from.
                yield return (toe, new PointsPassage(p.Id, trailing, Trailing: true));
            }
            yield break;
        }

        // Plain joint: continue onto the other section, if there is one (otherwise this is a line end).
        foreach (var s in ctx.SectionsAtNode[node])
        {
            if (s.Id != arrivedOn.Id)
                yield return (s, null);
        }
    }
}

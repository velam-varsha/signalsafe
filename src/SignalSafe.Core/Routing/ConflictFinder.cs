using SignalSafe.Core.Domain;

namespace SignalSafe.Core.Routing;

/// <summary>Two routes that must never be set at the same time, and why.</summary>
/// <param name="Opposing">True when the routes run over a shared section in opposite directions (head-on).</param>
public sealed record RouteConflict(
    string RouteA,
    string RouteB,
    IReadOnlyList<string> SharedSections,
    IReadOnlyList<string> OpposingPoints,
    bool Opposing = false);

/// <summary>
/// Finds conflicting route pairs: routes whose paths share a track section, or that need
/// the same points in opposite positions. Pairs that share a section in opposite directions
/// are marked as opposing (head-on).
/// </summary>
/// <remarks>
/// MVP simplification: overlaps are not compared.
/// </remarks>
public static class ConflictFinder
{
    public static IReadOnlyList<RouteConflict> Find(IReadOnlyList<DerivedRoute> routes)
    {
        var conflicts = new List<RouteConflict>();
        for (var i = 0; i < routes.Count; i++)
        {
            for (var j = i + 1; j < routes.Count; j++)
            {
                var a = routes[i];
                var b = routes[j];
                var shared = a.Sections.Intersect(b.Sections).OrderBy(s => s, StringComparer.Ordinal).ToList();
                var opposing = a.Points
                    .Where(kv => b.Points.TryGetValue(kv.Key, out var other) && other != kv.Value)
                    .Select(kv => kv.Key)
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToList();

                var headOn = shared.Any(sec => a.EntryNodeOf(sec) != b.EntryNodeOf(sec));

                if (shared.Count > 0 || opposing.Count > 0)
                    conflicts.Add(new RouteConflict(a.Id, b.Id, shared, opposing, headOn));
            }
        }
        return conflicts;
    }
}

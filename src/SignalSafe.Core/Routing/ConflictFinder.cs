using SignalSafe.Core.Domain;

namespace SignalSafe.Core.Routing;

/// <summary>Two routes that must never be set at the same time, and why.</summary>
public sealed record RouteConflict(
    string RouteA,
    string RouteB,
    IReadOnlyList<string> SharedSections,
    IReadOnlyList<string> OpposingPoints);

/// <summary>
/// Finds conflicting route pairs: routes whose paths share a track section, or that need
/// the same points in opposite positions.
/// </summary>
/// <remarks>
/// MVP simplification: overlaps are not compared. Opposing-direction overlap conflicts come with rule R06.
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

                if (shared.Count > 0 || opposing.Count > 0)
                    conflicts.Add(new RouteConflict(a.Id, b.Id, shared, opposing));
            }
        }
        return conflicts;
    }
}

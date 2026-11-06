using SignalSafe.Core.ControlTables;

namespace SignalSafe.Core.Rules;

/// <summary>
/// R10: the control tables must only refer to things that exist, with no duplicate routes and no
/// points called both Normal and Reverse. Problems found while reading the file are reported here too.
/// </summary>
public sealed class DataConsistencyRule : IRule
{
    public string Id => "R10";
    public string Name => "Data consistency";

    public IEnumerable<Finding> Check(RuleContext ctx)
    {
        foreach (var issue in ctx.Table.ImportIssues)
            yield return new Finding(Id, Name, Severity.Warning, null, [], issue.Message, issue.Source);

        var sections = ctx.Layout.Sections.Select(s => s.Id).ToHashSet();
        var points = ctx.PointsById.Keys.ToHashSet();
        var signals = ctx.Layout.Signals.Select(s => s.Id).ToHashSet();
        var knownRoutes = ctx.Table.Rows.Select(r => r.RouteId).Concat(ctx.Routes.Select(r => r.Id)).ToHashSet();

        foreach (var dup in ctx.Table.Rows.GroupBy(r => r.RouteId).Where(g => g.Count() > 1))
        {
            foreach (var row in dup.Skip(1))
            {
                yield return Warn(row, ControlTableColumns.RouteId,
                    $"Route {row.RouteId} appears more than once (first on row {dup.First().Source.Row}).");
            }
        }

        foreach (var row in ctx.Table.Rows)
        {
            foreach (var f in Unknown(row, ControlTableColumns.Entrance, [row.Entrance], signals, "signal"))
                yield return f;
            foreach (var f in Unknown(row, ControlTableColumns.Exit, [row.Exit], signals, "signal"))
                yield return f;
            foreach (var f in Unknown(row, ControlTableColumns.PointsNormal, row.PointsNormal, points, "points"))
                yield return f;
            foreach (var f in Unknown(row, ControlTableColumns.PointsReverse, row.PointsReverse, points, "points"))
                yield return f;
            foreach (var f in Unknown(row, ControlTableColumns.SectionsClear, row.SectionsClear, sections, "section"))
                yield return f;
            foreach (var f in Unknown(row, ControlTableColumns.Overlap, row.Overlap, sections, "section"))
                yield return f;
            foreach (var f in Unknown(row, ControlTableColumns.OverlapPoints, row.OverlapPoints.Select(p => p.PointsId), points, "points"))
                yield return f;
            foreach (var f in Unknown(row, ControlTableColumns.FlankPoints, row.FlankPoints.Select(p => p.PointsId), points, "points"))
                yield return f;
            foreach (var f in Unknown(row, ControlTableColumns.ConflictingRoutes, row.ConflictingRoutes, knownRoutes, "route"))
                yield return f;

            foreach (var both in row.PointsNormal.Intersect(row.PointsReverse))
            {
                yield return Warn(row, ControlTableColumns.PointsReverse,
                    $"{row.RouteId} calls {both} both Normal and Reverse.");
            }
        }
    }

    private IEnumerable<Finding> Unknown(ControlTableRow row, string column, IEnumerable<string> ids, HashSet<string> known, string kind)
    {
        foreach (var id in ids.Where(id => !known.Contains(id)))
        {
            var hint = SuggestionFor(id, known);
            yield return Warn(row, column,
                $"{column} lists \"{id}\", which is not a {kind} in the layout.{(hint is null ? "" : $" Did you mean {hint}?")}",
                hint is null ? [] : [hint]);
        }
    }

    private Finding Warn(ControlTableRow row, string column, string message, IReadOnlyList<string>? elements = null) =>
        new(Id, Name, Severity.Warning, row.RouteId, elements ?? [], message, row.Source.WithColumn(column));

    /// <summary>Suggests a known ID one edit away (a typo or swapped characters), if there is exactly one.</summary>
    private static string? SuggestionFor(string id, HashSet<string> known)
    {
        var close = known.Where(k => IsOneEditAway(id, k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        // Prefer two swapped characters (T21 for T12), then one wrong character, then one added or missing.
        foreach (var group in new[] { close.Where(k => IsSwap(id, k)), close.Where(k => k.Length == id.Length), close })
        {
            var candidates = group.ToList();
            if (candidates.Count == 1)
                return candidates[0];
            if (candidates.Count > 1)
                return null;
        }
        return null;
    }

    private static bool IsSwap(string a, string b)
    {
        if (a.Length != b.Length)
            return false;
        var diffs = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
        return diffs.Count == 2 && diffs[1] == diffs[0] + 1 && a[diffs[0]] == b[diffs[1]] && a[diffs[1]] == b[diffs[0]];
    }

    private static bool IsOneEditAway(string a, string b)
    {
        if (a == b || Math.Abs(a.Length - b.Length) > 1)
            return false;
        if (a.Length == b.Length)
        {
            var diffs = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
            return diffs.Count == 1 || IsSwap(a, b);
        }
        var (shorter, longer) = a.Length < b.Length ? (a, b) : (b, a);
        for (var i = 0; i < longer.Length; i++)
        {
            if (longer.Remove(i, 1) == shorter)
                return true;
        }
        return false;
    }
}

using SignalSafe.Core.Domain;

namespace SignalSafe.Core.Layouts;

/// <summary>A problem with the layout data itself, found before any route is derived.</summary>
public sealed record LayoutIssue(string Code, string ElementId, string Message);

/// <summary>
/// Checks that a layout is well formed: unique IDs, connections that exist,
/// points wired correctly and signals placed on real joints.
/// </summary>
public static class LayoutValidator
{
    public static IReadOnlyList<LayoutIssue> Validate(Layout layout)
    {
        var issues = new List<LayoutIssue>();
        var sections = new Dictionary<string, TrackSection>();
        var pointsById = layout.Points.ToDictionary(p => p.Id, p => p, StringComparer.Ordinal);

        CheckUniqueIds(layout, issues);

        foreach (var s in layout.Sections)
        {
            sections.TryAdd(s.Id, s);
            if (string.IsNullOrWhiteSpace(s.EndA) || string.IsNullOrWhiteSpace(s.EndB))
                issues.Add(new("L02", s.Id, $"Section {s.Id} must have two ends."));
            else if (s.EndA == s.EndB)
                issues.Add(new("L02", s.Id, $"Section {s.Id} starts and ends at the same node {s.EndA}."));
            if (s.LengthM <= 0)
                issues.Add(new("L03", s.Id, $"Section {s.Id} must have a length greater than 0 m."));
        }

        foreach (var p in layout.Points)
        {
            var legs = new[] { ("toe", p.ToeSection), ("normal", p.NormalSection), ("reverse", p.ReverseSection) };
            foreach (var (name, sectionId) in legs)
            {
                if (!sections.TryGetValue(sectionId, out var section))
                    issues.Add(new("L04", p.Id, $"Points {p.Id}: {name} section '{sectionId}' does not exist."));
                else if (!section.Touches(p.Id))
                    issues.Add(new("L05", p.Id, $"Points {p.Id}: {name} section {sectionId} does not connect to {p.Id}."));
            }
            if (legs.Select(l => l.Item2).Distinct().Count() != 3)
                issues.Add(new("L06", p.Id, $"Points {p.Id}: toe, normal and reverse must be three different sections."));
        }

        // Every node that is not a set of points is a plain joint or line end: at most two sections may meet there.
        var sectionsAtNode = layout.Sections
            .SelectMany(s => new[] { (Node: s.EndA, Section: s), (Node: s.EndB, Section: s) })
            .GroupBy(x => x.Node);
        foreach (var node in sectionsAtNode)
        {
            if (pointsById.TryGetValue(node.Key, out var p))
            {
                foreach (var (_, section) in node)
                {
                    if (section.Id != p.ToeSection && section.Id != p.NormalSection && section.Id != p.ReverseSection)
                        issues.Add(new("L07", p.Id, $"Section {section.Id} connects to points {p.Id} but is not its toe, normal or reverse section."));
                }
            }
            else if (node.Count() > 2)
            {
                var ids = string.Join(", ", node.Select(x => x.Section.Id));
                issues.Add(new("L08", node.Key, $"Node {node.Key} joins {node.Count()} sections ({ids}) but is not a set of points."));
            }
        }

        foreach (var sig in layout.Signals)
        {
            if (pointsById.ContainsKey(sig.AtNode))
                issues.Add(new("L09", sig.Id, $"Signal {sig.Id} cannot stand on points {sig.AtNode}."));
            if (!sections.TryGetValue(sig.Protects, out var protects))
                issues.Add(new("L10", sig.Id, $"Signal {sig.Id} protects section '{sig.Protects}', which does not exist."));
            else if (!protects.Touches(sig.AtNode))
                issues.Add(new("L11", sig.Id, $"Signal {sig.Id} stands at {sig.AtNode}, but section {sig.Protects} does not start there."));
        }

        return issues;
    }

    private static void CheckUniqueIds(Layout layout, List<LayoutIssue> issues)
    {
        var all = layout.Sections.Select(s => s.Id)
            .Concat(layout.Points.Select(p => p.Id))
            .Concat(layout.Signals.Select(s => s.Id));
        if (all.Any(string.IsNullOrWhiteSpace))
            issues.Add(new("L01", "", "Every section, points and signal needs an ID."));
        foreach (var dup in all.Where(id => !string.IsNullOrWhiteSpace(id)).GroupBy(id => id).Where(g => g.Count() > 1))
            issues.Add(new("L01", dup.Key, $"ID {dup.Key} is used {dup.Count()} times. IDs must be unique."));
    }
}

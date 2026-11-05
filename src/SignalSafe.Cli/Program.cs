using SignalSafe.Core.Domain;
using SignalSafe.Core.Layouts;
using SignalSafe.Core.Routing;

// SignalSafe command line: checks a layout file and prints its routes and conflicts.
// Usage: dotnet run --project src/SignalSafe.Cli -- samples/westbrook-junction.json

if (args.Length != 1)
{
    Console.WriteLine("Usage: dotnet run --project src/SignalSafe.Cli -- <layout.json>");
    Console.WriteLine("Example: dotnet run --project src/SignalSafe.Cli -- samples/westbrook-junction.json");
    return 2;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.WriteLine($"File not found: {path}");
    return 2;
}

Layout layout;
try
{
    layout = LayoutLoader.FromFile(path);
}
catch (LayoutFormatException ex)
{
    Console.WriteLine(ex.Message);
    return 1;
}

Console.WriteLine($"Layout: {layout.Name}");
Console.WriteLine($"{layout.Sections.Count} sections, {layout.Points.Count} points, {layout.Signals.Count} signals");
Console.WriteLine();

var issues = LayoutValidator.Validate(layout);
if (issues.Count > 0)
{
    Console.WriteLine($"LAYOUT PROBLEMS ({issues.Count})");
    foreach (var issue in issues)
        Console.WriteLine($"  [{issue.Code}] {issue.Message}");
    Console.WriteLine();
    Console.WriteLine("Fix these before routes can be derived reliably.");
    return 1;
}

Console.WriteLine("Layout check: OK");
Console.WriteLine();

var routes = RouteFinder.Derive(layout);
Console.WriteLine($"ROUTES ({routes.Count})");
foreach (var r in routes)
{
    var points = r.Points.Count == 0
        ? "none"
        : string.Join(", ", r.Points.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}"));
    Console.WriteLine($"  {r.Id}");
    Console.WriteLine($"      sections: {string.Join(", ", r.Sections)}");
    Console.WriteLine($"      points:   {points}");
    Console.WriteLine($"      overlap:  {string.Join(", ", r.Overlap)}");
}
Console.WriteLine();

var conflicts = ConflictFinder.Find(routes);
Console.WriteLine($"CONFLICTING ROUTES ({conflicts.Count})");
foreach (var c in conflicts)
{
    var reasons = new List<string>();
    if (c.SharedSections.Count > 0)
        reasons.Add($"share {string.Join(", ", c.SharedSections)}");
    if (c.OpposingPoints.Count > 0)
        reasons.Add($"need {string.Join(", ", c.OpposingPoints)} in opposite positions");
    Console.WriteLine($"  {c.RouteA}  x  {c.RouteB}   ({string.Join("; ", reasons)})");
}

return 0;

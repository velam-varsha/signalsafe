namespace SignalSafe.Api.Endpoints;

/// <summary>A built-in example: a layout and its control tables, shipped with the API.</summary>
public sealed record Sample(string Id, string Name, string Description, string LayoutFile, string TablesFile);

public static class Samples
{
    public static IReadOnlyList<Sample> All { get; } =
    [
        new("westbrook", "Westbrook Junction",
            "Junction to a branch and a siding with a shunt signal. The control tables have 7 planted mistakes.",
            "westbrook-junction.json", "westbrook-control-tables.xlsx"),
        new("kestrel", "Kestrel Lane Crossover",
            "Double line with a crossover and wrong-direction moves. The control tables have 1 planted mistake.",
            "kestrel-lane-crossover.json", "kestrel-control-tables.xlsx"),
    ];

    public static string Folder => Path.Combine(AppContext.BaseDirectory, "samples");

    public static Sample? Find(string id) => All.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static string PathOf(string fileName) => Path.Combine(Folder, fileName);
}

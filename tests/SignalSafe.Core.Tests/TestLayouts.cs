using SignalSafe.Core.Layouts;

namespace SignalSafe.Core.Tests;

/// <summary>Layouts used across tests.</summary>
internal static class TestLayouts
{
    public static Layout Westbrook() =>
        LayoutLoader.FromFile(Path.Combine(AppContext.BaseDirectory, "Samples", "westbrook-junction.json"));

    public static Layout KestrelLane() =>
        LayoutLoader.FromFile(Path.Combine(AppContext.BaseDirectory, "Samples", "kestrel-lane-crossover.json"));

    /// <summary>A plain line: W -T1- A -T2- B -T3- E, with signal SA at A and SB at B, both reading east.</summary>
    public static Layout StraightLine() => new(
        "Straight line",
        [
            new TrackSection("T1", "W", "A", 200),
            new TrackSection("T2", "A", "B", 300),
            new TrackSection("T3", "B", "E", 200),
        ],
        [],
        [
            new Signal("SA", SignalType.Main, "A", "T2"),
            new Signal("SB", SignalType.Main, "B", "T3"),
        ]);
}

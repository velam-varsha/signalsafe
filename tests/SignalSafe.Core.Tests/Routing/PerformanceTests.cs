using System.Diagnostics;

using SignalSafe.Core.Routing;

namespace SignalSafe.Core.Tests.Routing;

/// <summary>Sanity check that route search stays fast on a large layout (target NFR-01: 500 routes well under 10 s).</summary>
public class PerformanceTests
{
    [Fact]
    public void Derives_500_routes_and_their_conflicts_quickly()
    {
        var layout = LongLineWithJunctions(junctions: 250);

        var timer = Stopwatch.StartNew();
        var routes = RouteFinder.Derive(layout);
        var conflicts = ConflictFinder.Find(routes);
        timer.Stop();

        routes.Should().HaveCount(500);
        conflicts.Should().HaveCount(250);
        timer.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// A long line where each block has a signal, then points to a short loop that rejoins the main line.
    /// Each block gives 2 routes (main and loop) that conflict with each other.
    /// </summary>
    private static Layout LongLineWithJunctions(int junctions)
    {
        var sections = new List<TrackSection>();
        var points = new List<Points>();
        var signals = new List<Signal>();

        for (var i = 0; i < junctions; i++)
        {
            // J{i} --A{i}-- PA{i} ==(normal M{i} / reverse L{i})== PB{i} --B{i}-- J{i+1}
            sections.Add(new($"A{i}", $"J{i}", $"PA{i}", 100));
            sections.Add(new($"M{i}", $"PA{i}", $"PB{i}", 200));
            sections.Add(new($"L{i}", $"PA{i}", $"PB{i}", 220));
            sections.Add(new($"B{i}", $"PB{i}", $"J{i + 1}", 100));
            points.Add(new($"PA{i}", $"A{i}", $"M{i}", $"L{i}"));
            points.Add(new($"PB{i}", $"B{i}", $"M{i}", $"L{i}"));
            signals.Add(new($"S{i}", SignalType.Main, $"J{i}", $"A{i}"));
        }
        sections.Add(new("END", $"J{junctions}", "LINE-END", 100));
        signals.Add(new($"S{junctions}", SignalType.Main, $"J{junctions}", "END"));

        return new Layout("Generated long line", sections, points, signals);
    }
}

using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Routing;
using SignalSafe.Core.Rules;

namespace SignalSafe.Core.Tests.Rules;

public class FlankProtectionTests
{
    /// <summary>
    /// S1 reads east over T2 and trailing points PA (from their normal leg) onto T3.
    /// PA's reverse leg F runs to points Q. Q must lie Reverse so nothing from G can run along F into the route.
    ///
    ///   W --T1-- S1> --T2--\
    ///                        PA --T3-- S3> --T4-- E
    ///   G --(toe)-- Q --F---/
    ///                \--H-- end
    /// </summary>
    private static Layout FlankLayout(bool qFacesTheFlank = false) => new(
        "Flank example",
        [
            new TrackSection("T1", "W", "J1", 200),
            new TrackSection("T2", "J1", "PA", 200),
            new TrackSection("T3", "PA", "J3", 200),
            new TrackSection("T4", "J3", "E", 200),
            new TrackSection("F", "PA", "Q", 100),
            new TrackSection("G", "Q", "GEND", 200),
            new TrackSection("H", "Q", "HEND", 200),
        ],
        [
            new Points("PA", ToeSection: "T3", NormalSection: "T2", ReverseSection: "F"),
            qFacesTheFlank
                ? new Points("Q", ToeSection: "F", NormalSection: "G", ReverseSection: "H")
                : new Points("Q", ToeSection: "G", NormalSection: "F", ReverseSection: "H"),
        ],
        [
            new Signal("S1", SignalType.Main, "J1", "T2"),
            new Signal("S3", SignalType.Main, "J3", "T4"),
        ]);

    [Fact]
    public void Finds_the_points_that_protect_the_flank()
    {
        var layout = FlankLayout();
        var flank = FlankAnalysis.For(layout, RouteFinder.Derive(layout));

        flank["S1(M)-S3"].Should().ContainSingle()
            .Which.Required.Should().Be(new PointsCall("Q", PointPosition.Reverse));
    }

    [Fact]
    public void Drafted_tables_include_the_flank_points()
    {
        ControlTableBuilder.Draft(FlankLayout()).Single().FlankPoints
            .Should().Equal(new PointsCall("Q", PointPosition.Reverse));
    }

    [Fact]
    public void R08_reports_a_missing_flank_point()
    {
        var layout = FlankLayout();
        var rows = ControlTableBuilder.Draft(layout).Select(r => r with { FlankPoints = [] }).ToList();

        RuleEngine.Run(layout, new ControlTable(rows, []))
            .Should().ContainSingle(f => f.RuleId == "R08" && f.Severity == Severity.Error && f.Message.Contains("Q must be held Reverse"));
    }

    [Fact]
    public void R08_reports_a_flank_point_in_the_wrong_position()
    {
        var layout = FlankLayout();
        var rows = ControlTableBuilder.Draft(layout)
            .Select(r => r with { FlankPoints = [new PointsCall("Q", PointPosition.Normal)] }).ToList();

        RuleEngine.Run(layout, new ControlTable(rows, []))
            .Should().ContainSingle(f => f.RuleId == "R08" && f.Message.Contains("listed Normal"));
    }

    [Fact]
    public void R08_warns_when_nothing_can_protect_the_flank()
    {
        var layout = FlankLayout(qFacesTheFlank: true);
        var rows = ControlTableBuilder.Draft(layout);

        RuleEngine.Run(layout, new ControlTable(rows, []))
            .Should().ContainSingle(f => f.RuleId == "R08" && f.Severity == Severity.Warning && f.Message.Contains("trap points"));
    }

    [Fact]
    public void A_signal_reading_towards_the_points_protects_the_flank()
    {
        // Westbrook: S4(M)-S6 trails through P4; the siding signal S7 reads towards P4 and protects the flank.
        var layout = TestLayouts.Westbrook();
        var flank = FlankAnalysis.For(layout, RouteFinder.Derive(layout));

        flank["S4(M)-S6"].Should().ContainSingle().Which.ProtectingSignal.Should().Be("S7");
        flank["S7(S)-S6"].Should().ContainSingle().Which.ProtectingSignal.Should().Be("S4");
    }
}

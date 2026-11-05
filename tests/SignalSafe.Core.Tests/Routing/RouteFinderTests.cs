using SignalSafe.Core.Routing;

namespace SignalSafe.Core.Tests.Routing;

public class RouteFinderTests
{
    private static readonly IReadOnlyList<DerivedRoute> Westbrook = RouteFinder.Derive(TestLayouts.Westbrook());

    private static DerivedRoute Route(string id) => Westbrook.Single(r => r.Id == id);

    [Fact]
    public void Derives_exactly_the_five_Westbrook_routes()
    {
        Westbrook.Select(r => r.Id).Should().Equal(
            "S1(M)-S3", "S1(M)-S5", "S2(M)-S4", "S4(M)-S6", "S7(S)-S6");
    }

    [Fact]
    public void Main_line_route_needs_P1_normal()
    {
        var route = Route("S1(M)-S3");

        route.Sections.Should().Equal("T2", "T3");
        route.Points.Should().Equal(new Dictionary<string, PointPosition> { ["P1"] = PointPosition.Normal });
        route.Overlap.Should().Equal("T4");
    }

    [Fact]
    public void Branch_route_needs_P1_reverse()
    {
        var route = Route("S1(M)-S5");

        route.Sections.Should().Equal("T2", "T5");
        route.Points.Should().Equal(new Dictionary<string, PointPosition> { ["P1"] = PointPosition.Reverse });
    }

    [Fact]
    public void Route_with_no_points_has_none()
    {
        var route = Route("S2(M)-S4");

        route.Sections.Should().Equal("T13");
        route.Points.Should().BeEmpty();
        route.Overlap.Should().Equal("T12");
    }

    [Fact]
    public void Trailing_points_are_called_for_the_leg_the_train_comes_from()
    {
        Route("S4(M)-S6").Points.Should().Equal(new Dictionary<string, PointPosition> { ["P4"] = PointPosition.Normal });
        Route("S7(S)-S6").Points.Should().Equal(new Dictionary<string, PointPosition> { ["P4"] = PointPosition.Reverse });
    }

    [Fact]
    public void Shunt_signal_starts_a_shunt_route()
    {
        var route = Route("S7(S)-S6");

        route.IsShunt.Should().BeTrue();
        route.Sections.Should().Equal("T20", "T10");
    }

    [Fact]
    public void Signals_reading_towards_a_line_end_start_no_route()
    {
        Westbrook.Should().NotContain(r => r.Entrance.Id == "S3" || r.Entrance.Id == "S5" || r.Entrance.Id == "S6");
    }

    [Fact]
    public void Straight_line_has_one_route()
    {
        var routes = RouteFinder.Derive(TestLayouts.StraightLine());

        routes.Should().ContainSingle().Which.Id.Should().Be("SA(M)-SB");
    }

    [Fact]
    public void A_loop_without_an_exit_signal_ends_without_hanging()
    {
        // Balloon loop: from P1's toe, normal and reverse join up again at X.
        var layout = new Layout("Loop",
            [
                new TrackSection("T1", "A", "P1", 100),
                new TrackSection("T2", "P1", "X", 300),
                new TrackSection("T3", "X", "P1", 300),
                new TrackSection("T0", "W", "A", 100),
            ],
            [new Points("P1", "T1", "T2", "T3")],
            [new Signal("SA", SignalType.Main, "A", "T1")]);

        RouteFinder.Derive(layout).Should().BeEmpty();
    }

    [Fact]
    public void Same_input_always_gives_the_same_routes()
    {
        var again = RouteFinder.Derive(TestLayouts.Westbrook());

        again.Select(r => r.Id).Should().Equal(Westbrook.Select(r => r.Id));
    }
}

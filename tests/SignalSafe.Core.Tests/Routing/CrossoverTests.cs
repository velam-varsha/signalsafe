using SignalSafe.Core.Layouts;
using SignalSafe.Core.Routing;

namespace SignalSafe.Core.Tests.Routing;

/// <summary>Route search and conflicts on the Kestrel Lane crossover sample.</summary>
public class CrossoverTests
{
    private static readonly Layout Kestrel = TestLayouts.KestrelLane();
    private static readonly IReadOnlyList<DerivedRoute> Routes = RouteFinder.Derive(Kestrel);

    private static DerivedRoute Route(string id) => Routes.Single(r => r.Id == id);

    [Fact]
    public void Kestrel_sample_is_valid()
    {
        LayoutValidator.Validate(Kestrel).Should().BeEmpty();
    }

    [Fact]
    public void Derives_the_five_Kestrel_routes()
    {
        Routes.Select(r => r.Id).Should().Equal(
            "S31(M)-S33", "S31(M)-S43", "S42(M)-S30", "S42(M)-S40", "S44(M)-S42");
    }

    [Fact]
    public void Crossing_over_needs_both_ends_of_the_crossover_reverse()
    {
        var route = Route("S31(M)-S43");

        route.Sections.Should().Equal("T32", "TX", "T42");
        route.Points.Should().Equal(new Dictionary<string, PointPosition>
        {
            ["P11"] = PointPosition.Reverse,
            ["P12"] = PointPosition.Reverse,
        });
    }

    [Fact]
    public void Straight_route_calls_the_other_crossover_end_too()
    {
        var route = Route("S31(M)-S33");

        route.Points.Should().Equal(new Dictionary<string, PointPosition>
        {
            ["P11"] = PointPosition.Normal,
            ["P12"] = PointPosition.Normal,
        });
        route.LinkedPoints.Should().Equal("P12");
        Route("S42(M)-S40").LinkedPoints.Should().Equal("P11");
    }

    [Fact]
    public void Head_on_conflict_is_marked_as_opposing()
    {
        var conflicts = ConflictFinder.Find(Routes);

        // Each of these pairs uses a section in opposite directions (T32 or T42).
        conflicts.Where(c => c.Opposing).Select(c => $"{c.RouteA} x {c.RouteB}").Should().BeEquivalentTo(
            "S31(M)-S33 x S42(M)-S30",
            "S31(M)-S43 x S42(M)-S30",
            "S31(M)-S43 x S42(M)-S40");
        conflicts.Single(c => c.RouteA == "S31(M)-S33" && c.RouteB == "S31(M)-S43").Opposing.Should().BeFalse();
    }

    [Fact]
    public void Wrong_direction_move_crosses_from_the_Down_to_the_Up()
    {
        var route = Route("S42(M)-S30");

        route.Sections.Should().Equal("T42", "TX", "T32");
        route.Overlap.Should().Equal("T31");
    }

    [Fact]
    public void Two_signals_at_one_joint_are_told_apart_by_direction()
    {
        // J42 has S42 (reading west) and S43 (reading east). Arriving from T43, S42 is the exit, not S43.
        Route("S44(M)-S42").Exit.Id.Should().Be("S42");
    }

    [Fact]
    public void Finds_every_conflict_through_the_crossover()
    {
        var conflicts = ConflictFinder.Find(Routes).Select(c => $"{c.RouteA} x {c.RouteB}");

        conflicts.Should().BeEquivalentTo(
            "S31(M)-S33 x S31(M)-S43",
            "S31(M)-S33 x S42(M)-S30",
            "S31(M)-S43 x S42(M)-S30",
            "S31(M)-S43 x S42(M)-S40",
            "S42(M)-S30 x S42(M)-S40");
    }

    [Fact]
    public void Reports_crossover_points_linked_to_a_missing_partner()
    {
        var layout = Kestrel with
        {
            Points = [Kestrel.Points[0] with { LinkedWith = "P99" }, Kestrel.Points[1]],
        };

        LayoutValidator.Validate(layout).Should().Contain(i => i.Code == "L12" && i.ElementId == "P11");
    }

    [Fact]
    public void Reports_crossover_points_not_linked_both_ways()
    {
        var layout = Kestrel with
        {
            Points = [Kestrel.Points[0], Kestrel.Points[1] with { LinkedWith = null }],
        };

        LayoutValidator.Validate(layout).Should().ContainSingle(i => i.Code == "L13" && i.ElementId == "P11");
    }
}

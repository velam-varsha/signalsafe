using SignalSafe.Core.Routing;

namespace SignalSafe.Core.Tests.Routing;

public class ConflictFinderTests
{
    [Fact]
    public void Finds_the_two_Westbrook_conflicts()
    {
        var conflicts = ConflictFinder.Find(RouteFinder.Derive(TestLayouts.Westbrook()));

        conflicts.Should().HaveCount(2);
        conflicts.Should().ContainEquivalentOf(new
        {
            RouteA = "S1(M)-S3",
            RouteB = "S1(M)-S5",
            SharedSections = new[] { "T2" },
            OpposingPoints = new[] { "P1" },
        });
        conflicts.Should().ContainEquivalentOf(new
        {
            RouteA = "S4(M)-S6",
            RouteB = "S7(S)-S6",
            SharedSections = new[] { "T10" },
            OpposingPoints = new[] { "P4" },
        });
    }

    [Fact]
    public void Consecutive_routes_do_not_conflict()
    {
        var conflicts = ConflictFinder.Find(RouteFinder.Derive(TestLayouts.Westbrook()));

        conflicts.Should().NotContain(c => c.RouteA == "S2(M)-S4" || c.RouteB == "S2(M)-S4");
    }
}

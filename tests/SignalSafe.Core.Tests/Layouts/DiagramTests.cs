namespace SignalSafe.Core.Tests.Layouts;

public class DiagramTests
{
    public static TheoryData<string> Samples => new() { "westbrook", "kestrel" };

    private static Layout Load(string sample) => sample == "westbrook" ? TestLayouts.Westbrook() : TestLayouts.KestrelLane();

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_section_has_a_drawing(string sample)
    {
        var layout = Load(sample);

        layout.Diagram.Should().NotBeNull();
        layout.Diagram!.Keys.Should().BeEquivalentTo(layout.Sections.Select(s => s.Id));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Sections_meeting_at_a_node_meet_on_the_drawing(string sample)
    {
        var layout = Load(sample);
        var ends = layout.Sections.SelectMany(s => new[]
        {
            (Node: s.EndA, Point: layout.Diagram![s.Id][0], Section: s.Id),
            (Node: s.EndB, Point: layout.Diagram![s.Id][^1], Section: s.Id),
        });

        foreach (var node in ends.GroupBy(e => e.Node))
        {
            node.Select(e => e.Point).Distinct().Should().ContainSingle(
                $"every section ending at {node.Key} ({string.Join(", ", node.Select(e => e.Section))}) should end at the same drawing point");
        }
    }

    [Fact]
    public void Layouts_without_a_diagram_still_load()
    {
        TestLayouts.StraightLine().Diagram.Should().BeNull();
    }
}

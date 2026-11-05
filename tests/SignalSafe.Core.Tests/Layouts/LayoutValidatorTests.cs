using SignalSafe.Core.Layouts;

namespace SignalSafe.Core.Tests.Layouts;

public class LayoutValidatorTests
{
    [Fact]
    public void Westbrook_sample_has_no_issues()
    {
        LayoutValidator.Validate(TestLayouts.Westbrook()).Should().BeEmpty();
    }

    [Fact]
    public void Reports_duplicate_ids()
    {
        var layout = TestLayouts.StraightLine() with
        {
            Signals = [new Signal("T2", SignalType.Main, "A", "T2")],
        };

        LayoutValidator.Validate(layout).Should().ContainSingle(i => i.Code == "L01" && i.ElementId == "T2");
    }

    [Fact]
    public void Reports_a_section_with_no_length()
    {
        var layout = TestLayouts.StraightLine() with
        {
            Sections = [new TrackSection("T1", "W", "A", 0)],
            Signals = [],
        };

        LayoutValidator.Validate(layout).Should().Contain(i => i.Code == "L03" && i.ElementId == "T1");
    }

    [Fact]
    public void Reports_points_whose_leg_does_not_connect_to_them()
    {
        var layout = new Layout("Bad points",
            [
                new TrackSection("T1", "W", "P1", 100),
                new TrackSection("T2", "P1", "E1", 100),
                new TrackSection("T3", "X", "E2", 100),
            ],
            [new Points("P1", "T1", "T2", "T3")],
            []);

        LayoutValidator.Validate(layout).Should().Contain(i => i.Code == "L05" && i.ElementId == "P1");
    }

    [Fact]
    public void Reports_three_sections_meeting_without_points()
    {
        var layout = new Layout("Missing points",
            [
                new TrackSection("T1", "W", "N", 100),
                new TrackSection("T2", "N", "E1", 100),
                new TrackSection("T3", "N", "E2", 100),
            ],
            [],
            []);

        LayoutValidator.Validate(layout).Should().ContainSingle(i => i.Code == "L08" && i.ElementId == "N");
    }

    [Fact]
    public void Reports_a_signal_that_protects_a_section_not_starting_at_it()
    {
        var layout = TestLayouts.StraightLine() with
        {
            Signals = [new Signal("SX", SignalType.Main, "A", "T3")],
        };

        LayoutValidator.Validate(layout).Should().ContainSingle(i => i.Code == "L11" && i.ElementId == "SX");
    }

    [Fact]
    public void Reports_a_signal_on_points()
    {
        var layout = TestLayouts.Westbrook();
        layout = layout with { Signals = [.. layout.Signals, new Signal("S9", SignalType.Main, "P1", "T3")] };

        LayoutValidator.Validate(layout).Should().Contain(i => i.Code == "L09" && i.ElementId == "S9");
    }
}

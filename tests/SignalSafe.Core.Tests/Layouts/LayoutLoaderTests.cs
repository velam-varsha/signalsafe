using SignalSafe.Core.Layouts;

namespace SignalSafe.Core.Tests.Layouts;

public class LayoutLoaderTests
{
    [Fact]
    public void Loads_the_Westbrook_sample()
    {
        var layout = TestLayouts.Westbrook();

        layout.Name.Should().Be("Westbrook Junction (example)");
        layout.Sections.Should().HaveCount(13);
        layout.Points.Should().HaveCount(2);
        layout.Signals.Should().HaveCount(7);
        layout.Signals.Single(s => s.Id == "S7").Type.Should().Be(SignalType.Shunt);
    }

    [Fact]
    public void Rejects_invalid_json_with_a_clear_message()
    {
        var act = () => LayoutLoader.FromJson("{ \"sections\": [ ");

        act.Should().Throw<LayoutFormatException>().WithMessage("*not valid JSON*");
    }

    [Fact]
    public void Treats_missing_lists_as_empty()
    {
        var layout = LayoutLoader.FromJson("{ \"name\": \"Empty\" }");

        layout.Sections.Should().BeEmpty();
        layout.Points.Should().BeEmpty();
        layout.Signals.Should().BeEmpty();
    }
}

using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Rules;

namespace SignalSafe.Core.Tests.Rules;

/// <summary>
/// Each rule is tested the same way: start from control tables drafted from the layout (which must
/// give no findings), plant one mistake, and check that exactly the expected rule reports it.
/// </summary>
public class RuleTests
{
    private static readonly Layout Westbrook = TestLayouts.Westbrook();
    private static readonly Layout Kestrel = TestLayouts.KestrelLane();

    private static IReadOnlyList<Finding> Check(Layout layout, IEnumerable<ControlTableRow> rows) =>
        RuleEngine.Run(layout, new ControlTable(rows.ToList(), []));

    private static List<ControlTableRow> Draft(Layout layout) => ControlTableBuilder.Draft(layout).ToList();

    private static List<ControlTableRow> With(Layout layout, string routeId, Func<ControlTableRow, ControlTableRow> change) =>
        Draft(layout).Select(r => r.RouteId == routeId ? change(r) : r).ToList();

    [Fact]
    public void Drafted_tables_have_no_findings_for_either_sample()
    {
        Check(Westbrook, Draft(Westbrook)).Should().BeEmpty();
        Check(Kestrel, Draft(Kestrel)).Should().BeEmpty();
    }

    [Fact]
    public void R01_reports_a_route_missing_from_the_tables()
    {
        var findings = Check(Westbrook, Draft(Westbrook).Where(r => r.RouteId != "S7(S)-S6"));

        findings.Should().ContainSingle(f => f.RuleId == "R01")
            .Which.Should().Match<Finding>(f => f.RouteId == "S7(S)-S6" && f.Severity == Severity.Warning);
    }

    [Fact]
    public void R02_reports_a_route_that_does_not_exist_in_the_layout()
    {
        var impossible = Draft(Westbrook)[0] with { RouteId = "S1(M)-S6", Exit = "S6" };
        var findings = Check(Westbrook, [.. Draft(Westbrook), impossible]);

        findings.Should().ContainSingle(f => f.RuleId == "R02")
            .Which.Severity.Should().Be(Severity.Error);
    }

    [Fact]
    public void R03_reports_points_called_in_the_wrong_position()
    {
        var findings = Check(Westbrook, With(Westbrook, "S1(M)-S5", r => r with { PointsNormal = ["P1"], PointsReverse = [] }));

        findings.Where(f => f.RuleId == "R03").Should().ContainSingle()
            .Which.Message.Should().Contain("P1 must be Reverse");
    }

    [Fact]
    public void R03_reports_points_not_called_at_all()
    {
        var findings = Check(Westbrook, With(Westbrook, "S4(M)-S6", r => r with { PointsNormal = [] }));

        findings.Should().ContainSingle(f => f.RuleId == "R03" && f.Message.Contains("does not call P4"));
    }

    [Fact]
    public void R03_warns_about_points_that_are_not_on_the_route()
    {
        var findings = Check(Westbrook, With(Westbrook, "S2(M)-S4", r => r with { PointsNormal = ["P1"] }));

        findings.Should().ContainSingle(f => f.RuleId == "R03" && f.Severity == Severity.Warning);
    }

    [Fact]
    public void R04_reports_a_path_section_not_required_clear()
    {
        var findings = Check(Westbrook, With(Westbrook, "S1(M)-S3", r => r with { SectionsClear = ["T2"] }));

        findings.Should().ContainSingle(f => f.RuleId == "R04" && f.Elements.Contains("T3"));
    }

    [Fact]
    public void R05_reports_a_conflict_listed_on_one_side_only()
    {
        var findings = Check(Westbrook, With(Westbrook, "S4(M)-S6", r => r with { ConflictingRoutes = [] }));

        findings.Should().ContainSingle(f => f.RuleId == "R05")
            .Which.RouteId.Should().Be("S4(M)-S6");
    }

    [Fact]
    public void R05_accepts_the_designers_own_route_names()
    {
        // The designer calls S1(M)-S5 "S1B"; S1(M)-S3 lists "S1B" as conflicting. That must count.
        var rows = Draft(Westbrook).Select(r => r.RouteId switch
        {
            "S1(M)-S5" => r with { RouteId = "S1B" },
            "S1(M)-S3" => r with { ConflictingRoutes = ["S1B"] },
            _ => r,
        });

        Check(Westbrook, rows).Should().BeEmpty();
    }

    [Fact]
    public void R06_reports_an_unlisted_head_on_conflict()
    {
        var findings = Check(Kestrel, With(Kestrel, "S31(M)-S43", r => r with
        {
            ConflictingRoutes = r.ConflictingRoutes.Where(c => c != "S42(M)-S30").ToList(),
        }));

        findings.Should().ContainSingle(f => f.RuleId == "R06")
            .Which.Message.Should().Contain("head-on");
    }

    [Fact]
    public void R07_reports_a_missing_overlap()
    {
        var findings = Check(Westbrook, With(Westbrook, "S2(M)-S4", r => r with { Overlap = [] }));

        findings.Should().ContainSingle(f => f.RuleId == "R07")
            .Which.Message.Should().Contain("Expected overlap: T12");
    }

    [Fact]
    public void R07_reports_the_wrong_overlap()
    {
        var findings = Check(Westbrook, With(Westbrook, "S2(M)-S4", r => r with { Overlap = ["T10"] }));

        findings.Should().ContainSingle(f => f.RuleId == "R07" && f.Message.Contains("listed as T10"));
    }

    [Fact]
    public void R09_reports_a_crossover_called_on_one_end_only()
    {
        var findings = Check(Kestrel, With(Kestrel, "S31(M)-S33", r => r with { PointsNormal = ["P11"] }));

        findings.Should().ContainSingle(f => f.RuleId == "R09")
            .Which.Message.Should().Contain("Call P12 Normal too");
    }

    [Fact]
    public void R09_reports_crossover_ends_in_different_positions()
    {
        var findings = Check(Kestrel, With(Kestrel, "S31(M)-S33", r => r with { PointsNormal = ["P11"], PointsReverse = ["P12"] }));

        findings.Should().Contain(f => f.RuleId == "R09" && f.Message.Contains("move together"));
    }

    [Fact]
    public void R10_reports_unknown_ids_and_suggests_the_likely_one()
    {
        var findings = Check(Westbrook, With(Westbrook, "S4(M)-S6", r => r with { SectionsClear = ["T21", "T10"] }));

        var finding = findings.Should().ContainSingle(f => f.RuleId == "R10").Which;
        finding.Message.Should().Contain("Did you mean T12?");
        finding.Elements.Should().Equal("T12"); // so the likely section is highlighted on the diagram
    }

    [Fact]
    public void R10_reports_duplicate_routes_and_points_called_both_ways()
    {
        var draft = Draft(Westbrook);
        var findings = Check(Westbrook,
        [
            .. draft.Select(r => r.RouteId == "S1(M)-S3" ? r with { PointsReverse = ["P1"] } : r),
            draft[0],
        ]);

        findings.Should().Contain(f => f.RuleId == "R10" && f.Message.Contains("more than once"));
        findings.Should().Contain(f => f.RuleId == "R10" && f.Message.Contains("both Normal and Reverse"));
    }

    [Fact]
    public void R10_reports_problems_found_while_reading_the_file()
    {
        var table = new ControlTable([], [new ImportIssue(new SourceRef("Routes", 7, "Exit"), "Row 7 has no Exit, so it was skipped.")]);

        RuleEngine.Run(Westbrook, table).Should().Contain(f => f.RuleId == "R10" && f.Source!.Row == 7);
    }

    [Fact]
    public void Errors_are_listed_before_warnings()
    {
        var findings = Check(Westbrook, Draft(Westbrook)
            .Where(r => r.RouteId != "S7(S)-S6")
            .Select(r => r.RouteId == "S2(M)-S4" ? r with { Overlap = [] } : r));

        findings.Select(f => f.Severity).Should().BeInAscendingOrder();
    }
}

using ClosedXML.Excel;

using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Rules;
using SignalSafe.Excel;

namespace SignalSafe.Core.Tests.Excel;

public sealed class ControlTableExcelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("signalsafe-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string TempFile(string name) => Path.Combine(_dir, name);

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "Samples", name);

    [Fact]
    public void Writing_then_reading_gives_back_the_same_rows()
    {
        var rows = ControlTableBuilder.Draft(TestLayouts.KestrelLane());
        var path = TempFile("round-trip.xlsx");

        ControlTableExcel.Write(path, rows);
        var table = ControlTableExcel.Read(path);

        table.ImportIssues.Should().BeEmpty();
        table.Rows.Should().BeEquivalentTo(rows);
    }

    [Fact]
    public void Blank_template_has_every_column_and_no_rows()
    {
        var table = ControlTableExcel.Read(Sample("control-table-template.xlsx"));

        table.Rows.Should().BeEmpty();
        table.ImportIssues.Should().BeEmpty();
        using var workbook = new XLWorkbook(Sample("control-table-template.xlsx"));
        workbook.Worksheet("Routes").Row(1).CellsUsed().Select(c => c.GetString())
            .Should().Equal(ControlTableColumns.All);
    }

    [Fact]
    public void Westbrook_sample_tables_give_the_seven_planted_findings()
    {
        var table = ControlTableExcel.Read(Sample("westbrook-control-tables.xlsx"));
        var findings = RuleEngine.Run(TestLayouts.Westbrook(), table);

        findings.Select(f => $"{f.RuleId} {f.RouteId}").Should().Equal(
            "R03 S1(M)-S5",
            "R04 S4(M)-S6",
            "R05 S1(M)-S3",
            "R05 S1(M)-S5",
            "R07 S2(M)-S4",
            "R01 S7(S)-S6",
            "R10 S4(M)-S6");
    }

    [Fact]
    public void Kestrel_sample_tables_give_the_one_planted_finding()
    {
        var table = ControlTableExcel.Read(Sample("kestrel-control-tables.xlsx"));

        RuleEngine.Run(TestLayouts.KestrelLane(), table)
            .Should().ContainSingle().Which.RuleId.Should().Be("R09");
    }

    [Fact]
    public void Headings_are_matched_ignoring_case_and_spaces()
    {
        var path = TempFile("headings.xlsx");
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Routes");
            ws.Cell(1, 1).Value = "route id";
            ws.Cell(1, 2).Value = "ENTRANCE";
            ws.Cell(1, 3).Value = "Exit";
            ws.Cell(1, 4).Value = "Flank Points";
            ws.Cell(2, 1).Value = "S1(M)-S3";
            ws.Cell(2, 2).Value = "S1";
            ws.Cell(2, 3).Value = "S3";
            ws.Cell(2, 4).Value = "P3 n, P4(R), P5-N";
            wb.SaveAs(path);
        }

        var table = ControlTableExcel.Read(path);

        table.ImportIssues.Should().BeEmpty();
        table.Rows.Single().FlankPoints.Should().Equal(
            new PointsCall("P3", PointPosition.Normal),
            new PointsCall("P4", PointPosition.Reverse),
            new PointsCall("P5", PointPosition.Normal));
    }

    [Fact]
    public void Reports_missing_required_columns()
    {
        var path = TempFile("missing-columns.xlsx");
        using (var wb = new XLWorkbook())
        {
            wb.AddWorksheet("Routes").Cell(1, 1).Value = "RouteId";
            wb.SaveAs(path);
        }

        var table = ControlTableExcel.Read(path);

        table.Rows.Should().BeEmpty();
        table.ImportIssues.Should().ContainSingle().Which.Message.Should().Contain("Entrance, Exit");
    }

    [Fact]
    public void Skips_a_row_without_an_exit_and_says_where_it_is()
    {
        var path = TempFile("no-exit.xlsx");
        ControlTableExcel.Write(path, ControlTableBuilder.Draft(TestLayouts.Westbrook()));
        using (var wb = new XLWorkbook(path))
        {
            wb.Worksheet("Routes").Cell(3, 3).Value = "";
            wb.Save();
        }

        var table = ControlTableExcel.Read(path);

        table.Rows.Should().HaveCount(4);
        table.ImportIssues.Should().ContainSingle().Which.Source.Should().Be(new SourceRef("Routes", 3, "Exit"));
    }

    [Fact]
    public void Reports_a_points_position_it_cannot_read()
    {
        var path = TempFile("bad-flank.xlsx");
        var rows = ControlTableBuilder.Draft(TestLayouts.Westbrook());
        ControlTableExcel.Write(path, rows);
        using (var wb = new XLWorkbook(path))
        {
            wb.Worksheet("Routes").Cell(2, 9).Value = "P3";
            wb.Save();
        }

        ControlTableExcel.Read(path).ImportIssues
            .Should().ContainSingle().Which.Message.Should().Contain("followed by N or R");
    }

    [Fact]
    public void A_file_that_is_not_a_workbook_gives_an_import_issue_not_a_crash()
    {
        var path = TempFile("not-excel.xlsx");
        File.WriteAllText(path, "this is not a spreadsheet");

        ControlTableExcel.Read(path).ImportIssues.Should().ContainSingle()
            .Which.Message.Should().Contain("could not be opened");
    }

    [Fact]
    public void Findings_report_can_be_written_and_opened()
    {
        var layout = TestLayouts.Westbrook();
        var table = ControlTableExcel.Read(Sample("westbrook-control-tables.xlsx"));
        var findings = RuleEngine.Run(layout, table);
        var path = TempFile("report.xlsx");

        FindingsReport.Write(path, layout, findings, SignalSafe.Core.Routing.RouteFinder.Derive(layout), DateTimeOffset.UnixEpoch);

        using var wb = new XLWorkbook(path);
        wb.Worksheets.Select(w => w.Name).Should().Equal("Summary", "Findings", "Derived routes");
        wb.Worksheet("Findings").RowsUsed().Count().Should().Be(findings.Count + 1);
    }
}

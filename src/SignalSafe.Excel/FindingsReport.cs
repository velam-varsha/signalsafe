using ClosedXML.Excel;

using SignalSafe.Core.Domain;
using SignalSafe.Core.Routing;
using SignalSafe.Core.Rules;

namespace SignalSafe.Excel;

/// <summary>Writes the results of a check to an Excel workbook: summary, findings and derived routes.</summary>
public static class FindingsReport
{
    public static void Write(
        string path,
        Layout layout,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<DerivedRoute> routes,
        DateTimeOffset checkedAt)
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.AddWorksheet("Summary");
        var errors = findings.Count(f => f.Severity == Severity.Error);
        var warnings = findings.Count(f => f.Severity == Severity.Warning);
        object[][] lines =
        [
            ["SignalSafe findings report"],
            [],
            ["Layout", layout.Name],
            ["Checked", checkedAt.ToString("yyyy-MM-dd HH:mm 'UTC'zzz", System.Globalization.CultureInfo.InvariantCulture)],
            ["Routes derived", routes.Count],
            ["Errors", errors],
            ["Warnings", warnings],
            [],
            ["SignalSafe is an advisory design-checking aid, not a certified safety system."],
        ];
        for (var r = 0; r < lines.Length; r++)
        {
            for (var c = 0; c < lines[r].Length; c++)
                summary.Cell(r + 1, c + 1).Value = XLCellValue.FromObject(lines[r][c], System.Globalization.CultureInfo.InvariantCulture);
        }
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 14;
        summary.Column(1).Width = 18;
        summary.Column(2).Width = 40;

        var sheet = workbook.AddWorksheet("Findings");
        string[] headings = ["Severity", "Rule", "Rule name", "Route", "Message", "Location", "Elements"];
        WriteHeadings(sheet, headings);
        var row = 2;
        foreach (var f in findings)
        {
            sheet.Cell(row, 1).Value = f.Severity.ToString();
            sheet.Cell(row, 2).Value = f.RuleId;
            sheet.Cell(row, 3).Value = f.RuleName;
            sheet.Cell(row, 4).Value = f.RouteId ?? "";
            sheet.Cell(row, 5).Value = f.Message;
            sheet.Cell(row, 6).Value = f.Source?.ToString() ?? "Derived from layout";
            sheet.Cell(row, 7).Value = string.Join(", ", f.Elements.Distinct());
            sheet.Cell(row, 1).Style.Font.FontColor = f.Severity == Severity.Error ? XLColor.FromHtml("#B3261E") : XLColor.FromHtml("#8A5300");
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;
        }
        Finish(sheet, row);
        sheet.Column(5).Width = 90;
        sheet.Column(5).Style.Alignment.WrapText = true;

        var routeSheet = workbook.AddWorksheet("Derived routes");
        WriteHeadings(routeSheet, ["Route", "Type", "Sections", "Points", "Overlap"]);
        row = 2;
        foreach (var r in routes)
        {
            routeSheet.Cell(row, 1).Value = r.Id;
            routeSheet.Cell(row, 2).Value = r.IsShunt ? "Shunt" : "Main";
            routeSheet.Cell(row, 3).Value = string.Join(", ", r.Sections);
            routeSheet.Cell(row, 4).Value = string.Join(", ", r.Points.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}"));
            routeSheet.Cell(row, 5).Value = string.Join(", ", r.Overlap);
            row++;
        }
        Finish(routeSheet, row);

        workbook.SaveAs(path);
    }

    private static void WriteHeadings(IXLWorksheet sheet, string[] headings)
    {
        for (var c = 0; c < headings.Length; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = headings[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F2");
        }
    }

    private static void Finish(IXLWorksheet sheet, int nextRow)
    {
        sheet.SheetView.FreezeRows(1);
        if (nextRow > 2)
            sheet.Range(1, 1, nextRow - 1, sheet.LastColumnUsed()!.ColumnNumber()).SetAutoFilter();
        sheet.Columns().AdjustToContents();
    }
}

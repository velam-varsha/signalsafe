using System.Text.RegularExpressions;

using ClosedXML.Excel;

using SignalSafe.Core.ControlTables;
using SignalSafe.Core.Domain;

namespace SignalSafe.Excel;

/// <summary>Reads and writes control tables in the SignalSafe Excel template.</summary>
public static partial class ControlTableExcel
{
    private const string Sheet = ControlTableColumns.SheetName;

    /// <summary>Reads control tables from an .xlsx file. Problems are returned as import issues, not exceptions.</summary>
    public static ControlTable Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static ControlTable Read(Stream stream)
    {
        var issues = new List<ImportIssue>();
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or IOException or ArgumentException or InvalidOperationException
            || ex.GetType().Name == "OpenXmlPackageException")
        {
            return new ControlTable([], [new ImportIssue(new SourceRef("(file)", 0), $"The file could not be opened as an Excel workbook (.xlsx): {ex.Message}")]);
        }

        using (workbook)
        {
            if (!workbook.TryGetWorksheet(Sheet, out var sheet))
            {
                sheet = workbook.Worksheets.First();
                issues.Add(new ImportIssue(new SourceRef(sheet.Name, 0), $"No sheet named \"{Sheet}\" was found, so the first sheet \"{sheet.Name}\" was read."));
            }

            var columns = ReadHeader(sheet, issues);
            if (columns is null)
                return new ControlTable([], issues);

            var rows = new List<ControlTableRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastRow; r++)
            {
                var row = ReadRow(sheet, r, columns, issues);
                if (row is not null)
                    rows.Add(row);
            }
            return new ControlTable(rows, issues);
        }
    }

    /// <summary>Writes the template with the given rows (none for a blank template).</summary>
    public static void Write(string path, IEnumerable<ControlTableRow> rows, string title = "SignalSafe control tables")
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(Sheet);

        for (var c = 0; c < ControlTableColumns.All.Count; c++)
        {
            var name = ControlTableColumns.All[c];
            var cell = sheet.Cell(1, c + 1);
            cell.Value = name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F2");
            cell.CreateComment().AddText(ControlTableColumns.Help[name]);
        }

        var r = 2;
        foreach (var row in rows)
        {
            string[] values =
            [
                row.RouteId, row.Entrance, row.Exit,
                Join(row.PointsNormal), Join(row.PointsReverse), Join(row.SectionsClear), Join(row.Overlap),
                Join(row.OverlapPoints.Select(p => p.ToString())), Join(row.FlankPoints.Select(p => p.ToString())),
                Join(row.ConflictingRoutes),
            ];
            for (var c = 0; c < values.Length; c++)
                sheet.Cell(r, c + 1).Value = values[c];
            r++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, Math.Max(r, 2));
        foreach (var column in sheet.ColumnsUsed())
            column.Width = Math.Max(column.Width, 14);

        var about = workbook.AddWorksheet("About");
        string[] lines =
        [
            title,
            "",
            "One row per route on the Routes sheet. Hover over a column heading for help.",
            "Separate several values in one cell with commas, e.g. T1, T2, T3.",
            "Points in OverlapPoints and FlankPoints carry their position: P5N (Normal) or P5R (Reverse).",
            "RouteId, Entrance and Exit are required; other columns can be left blank.",
            "",
            "SignalSafe is an advisory design-checking aid, not a certified safety system.",
        ];
        for (var i = 0; i < lines.Length; i++)
            about.Cell(i + 1, 1).Value = lines[i];
        about.Cell(1, 1).Style.Font.Bold = true;
        about.Column(1).Width = 100;

        workbook.SaveAs(path);
    }

    private static Dictionary<string, int>? ReadHeader(IXLWorksheet sheet, List<ImportIssue> issues)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastColumn = sheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var c = 1; c <= lastColumn; c++)
        {
            var name = Normalise(sheet.Cell(1, c).GetString());
            if (name.Length > 0)
                columns.TryAdd(name, c);
        }

        var missing = ControlTableColumns.Required.Where(req => !columns.ContainsKey(req)).ToList();
        if (missing.Count > 0)
        {
            issues.Add(new ImportIssue(new SourceRef(sheet.Name, 1),
                $"Required column(s) missing from the heading row: {string.Join(", ", missing)}. Download the template to see the expected headings."));
            return null;
        }
        return columns;
    }

    private static ControlTableRow? ReadRow(IXLWorksheet sheet, int r, Dictionary<string, int> columns, List<ImportIssue> issues)
    {
        string Text(string column) =>
            columns.TryGetValue(column, out var c) ? sheet.Cell(r, c).GetFormattedString().Trim() : "";

        if (ControlTableColumns.All.All(c => Text(c).Length == 0))
            return null; // blank row

        var source = new SourceRef(sheet.Name, r);
        var routeId = Text(ControlTableColumns.RouteId);
        var entrance = Text(ControlTableColumns.Entrance);
        var exit = Text(ControlTableColumns.Exit);
        foreach (var (column, value) in new[] { (ControlTableColumns.RouteId, routeId), (ControlTableColumns.Entrance, entrance), (ControlTableColumns.Exit, exit) })
        {
            if (value.Length == 0)
            {
                issues.Add(new ImportIssue(source.WithColumn(column), $"Row {r} has no {column}, so it was skipped."));
                return null;
            }
        }

        return new ControlTableRow(
            routeId,
            entrance,
            exit,
            List(Text(ControlTableColumns.PointsNormal)),
            List(Text(ControlTableColumns.PointsReverse)),
            List(Text(ControlTableColumns.SectionsClear)),
            List(Text(ControlTableColumns.Overlap)),
            Calls(Text(ControlTableColumns.OverlapPoints), source.WithColumn(ControlTableColumns.OverlapPoints), issues),
            Calls(Text(ControlTableColumns.FlankPoints), source.WithColumn(ControlTableColumns.FlankPoints), issues),
            List(Text(ControlTableColumns.ConflictingRoutes)),
            source);
    }

    private static List<string> List(string cell) =>
        cell.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static List<PointsCall> Calls(string cell, SourceRef source, List<ImportIssue> issues)
    {
        var calls = new List<PointsCall>();
        foreach (var item in List(cell))
        {
            var m = PointsCallPattern().Match(item);
            if (!m.Success)
            {
                issues.Add(new ImportIssue(source, $"\"{item}\" is not a points position. Use the points ID followed by N or R, e.g. P5N."));
                continue;
            }
            var position = char.ToUpperInvariant(m.Groups[2].Value[0]) == 'N' ? PointPosition.Normal : PointPosition.Reverse;
            calls.Add(new PointsCall(m.Groups[1].Value, position));
        }
        return calls;
    }

    private static string Normalise(string heading) => heading.Replace(" ", "", StringComparison.Ordinal).Trim();

    private static string Join(IEnumerable<string> items) => string.Join(", ", items);

    /// <summary>Accepts P5N, P5 N, P5-N, P5(N) and lower case.</summary>
    [GeneratedRegex(@"^(.+?)[\s\-\(]*([NnRr])\)?$")]
    private static partial Regex PointsCallPattern();
}

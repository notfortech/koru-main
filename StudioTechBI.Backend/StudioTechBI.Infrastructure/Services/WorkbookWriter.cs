using ClosedXML.Excel;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

public sealed class WorkbookWriter : IWorkbookWriter
{
    public Task<Stream> WriteAsync(List<BlendedTable> tables, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook();
        var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sheetName = SanitizeSheetName(table.TableName, usedSheetNames);
            var sheet = workbook.Worksheets.Add(sheetName);

            for (var c = 0; c < table.Columns.Count; c++)
                sheet.Cell(1, c + 1).Value = table.Columns[c];

            for (var r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                for (var c = 0; c < table.Columns.Count; c++)
                    sheet.Cell(r + 2, c + 1).Value = row.TryGetValue(table.Columns[c], out var v) ? v : "";
            }

            sheet.Row(1).Style.Font.Bold = true;
            sheet.Columns().AdjustToContents();
        }

        if (tables.Count == 0)
            workbook.Worksheets.Add("Empty");

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
    }

    private static string SanitizeSheetName(string name, HashSet<string> used)
    {
        var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        if (cleaned.Length > 31) cleaned = cleaned[..31];
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "Sheet";

        var candidate = cleaned;
        var suffix = 2;
        while (!used.Add(candidate))
        {
            var baseName = cleaned.Length > 27 ? cleaned[..27] : cleaned;
            candidate = $"{baseName}_{suffix++}";
        }
        return candidate;
    }
}

using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace StudioTechBI.Application.Utilities;

/// <summary>Reads CSV header + up to <paramref name="maxDataRows"/> data rows for preview (no full materialization of file in memory beyond cap).</summary>
public static class CsvSampleExtractor
{
    public const int DefaultMaxRows = 100;

    public static async Task<(List<string> Columns, List<Dictionary<string, string>> Rows)> ExtractAsync(
        Stream stream,
        int maxDataRows = DefaultMaxRows,
        CancellationToken cancellationToken = default)
    {
        if (maxDataRows < 1)
            maxDataRows = DefaultMaxRows;

        if (stream.CanSeek)
            stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };
        using var csv = new CsvReader(reader, config);

        if (!await csv.ReadAsync())
            throw new InvalidOperationException("CSV is empty.");

        csv.ReadHeader();
        var header = csv.HeaderRecord;
        if (header == null || header.Length == 0)
            throw new InvalidOperationException("CSV has no header row.");

        var columns = header.Select(h => (h ?? "").Trim()).Where(h => h.Length > 0).ToList();
        if (columns.Count == 0)
            throw new InvalidOperationException("CSV header contains no column names.");

        var rows = new List<Dictionary<string, string>>();
        var count = 0;
        while (count < maxDataRows && await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columns)
            {
                var raw = csv.GetField(col);
                row[col] = raw ?? "";
            }

            rows.Add(row);
            count++;
        }

        return (columns, rows);
    }
}

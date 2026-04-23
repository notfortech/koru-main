using ExcelDataReader;

namespace StudioTechBI.Application.Utilities;

/// <summary>Reads Excel header + up to <paramref name="maxDataRows"/> rows for preview.</summary>
public static class ExcelSampleExtractor
{
    public const int DefaultMaxRows = 100;

    public static Task<(List<string> Columns, List<Dictionary<string, string>> Rows)> ExtractAsync(
        Stream stream,
        int maxDataRows = DefaultMaxRows,
        CancellationToken cancellationToken = default)
    {
        if (maxDataRows < 1)
            maxDataRows = DefaultMaxRows;

        if (stream.CanSeek)
            stream.Position = 0;

        // ExcelDataReader reads XLS/XLSX from a forward-only stream.
        using var reader = ExcelReaderFactory.CreateReader(stream);

        if (!reader.Read())
            throw new InvalidOperationException("Excel is empty.");

        var rawHeaders = new List<string>();
        for (var i = 0; i < reader.FieldCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var h = reader.GetValue(i)?.ToString()?.Trim() ?? "";
            rawHeaders.Add(h);
        }

        var columns = NormalizeHeaders(rawHeaders);
        if (columns.Count == 0)
            throw new InvalidOperationException("Excel header contains no column names.");

        var rows = new List<Dictionary<string, string>>();
        var count = 0;
        while (count < maxDataRows && reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                var v = i < reader.FieldCount ? reader.GetValue(i) : null;
                row[col] = v?.ToString() ?? "";
            }

            rows.Add(row);
            count++;
        }

        return Task.FromResult((columns, rows));
    }

    private static List<string> NormalizeHeaders(List<string> raw)
    {
        var result = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < raw.Count; i++)
        {
            var baseName = raw[i];
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = $"Column{i + 1}";

            var name = baseName;
            var suffix = 2;
            while (!used.Add(name))
            {
                name = $"{baseName}_{suffix}";
                suffix++;
            }

            result.Add(name);
        }
        return result;
    }
}

